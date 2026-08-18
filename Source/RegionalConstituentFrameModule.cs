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
    // The constituent-local spatial execution substrate.
    //
    // Native map generation assumes one world tile occupies one whole map, so
    // its workers read map.Size, map.Center, map.Tile and map.AllCells as if
    // those described the feature's own ground. On a stitched region they
    // describe the aggregate instead, which is why a mountainous root filled
    // 19-24% of the region with rock (F-126) and why cave dispatch both lost
    // and spread (F-129).
    //
    // A frame is one constituent's answer to those questions. It deliberately
    // exposes SEVERAL named spatial operations rather than one substitute
    // "size": a warped Voronoi member has no unique width, and native code
    // uses map width for at least four different meanings - a characteristic
    // length, a placement anchor, a count density over area, and a proportion
    // of area. An adapter picks the operation whose meaning matches the call
    // site it is replacing, and that choice is recorded rather than implied.
    //
    // What this substrate does NOT do: it does not intercept native reads.
    // Map.Size resolves to MapInfo.Size, whose getter is AggressiveInlining
    // over a backing field, so the read cannot be reliably patched; and the
    // field cannot be swapped per frame because map.cellIndices and every grid
    // are built against the real size. Adapters therefore either compose a
    // predicate (placement-driven workers) or re-derive the worker's own maths
    // against frame operations (analytic workers).
    internal sealed class CAConstituentFrame
    {
        internal int Index;
        internal int SourceTileId;

        // Actual centroid of the cells this constituent owns, NOT the
        // projected tile centre. Ownership is noise-warped, so the two differ.
        internal IntVec3 Centroid;

        // Owned cell count. The basis for every density and proportion
        // operation, and the only unambiguous measure an irregular region has.
        internal int Area;

        internal CellRect BoundingExtent;

        // Radius of the circle with the same area. Named as what it is: a
        // candidate stand-in for a native radius, not a canonical extent.
        internal float EquivalentRadius;

        // The honest analogue of native `map.Size.x` for an irregular member -
        // the diameter of the equal-area circle. Adapters that are replacing a
        // width-proportional constant use THIS and say so; adapters needing a
        // bound use BoundingExtent instead.
        internal float CharacteristicWidth;

        internal float PlacementCount(float perThousandCells)
        {
            return Area * perThousandCells / 1000f;
        }

        internal int ProportionalFillTarget(float fraction)
        {
            return Mathf.RoundToInt(Area * Mathf.Clamp01(fraction));
        }

        // Frame-local offset of a cell from this constituent's centroid.
        internal Vector2 LocalOffset(float x, float z)
        {
            return new Vector2(x - Centroid.x, z - Centroid.z);
        }

        // x-component of the frame-local offset rotated by `degrees` about Y.
        // Matches Verse.Noise.Rotate's x-row for a pure Y rotation, which is
        // cos(theta)*x + sin(theta)*z, so an analytic worker re-derived here
        // keeps the engine's own orientation convention.
        internal float RotatedLocalX(float x, float z, float cos, float sin)
        {
            return cos * (x - Centroid.x) + sin * (z - Centroid.z);
        }
    }

    internal sealed class CAConstituentFrameSet
    {
        private readonly int[] ownerByCell;
        private readonly CAConstituentFrame[] frames;

        internal int Count => frames.Length;
        internal CAConstituentFrame this[int index] => frames[index];

        // A frame set is a pure function of the ownership grid and the grid's
        // dimensions - it reads map.cellIndices and map.Center and nothing
        // else - so the pre-generation projection kernel builds the same
        // frames from its own backing size. Verse.Map defines Center as
        // (Size.x / 2, 0, Size.z / 2), which is what `center` carries here.
        internal CAConstituentFrameSet(Map map, int[] ownerByCell,
            IReadOnlyList<PlanetTile> members)
            : this(map.Size, map.cellIndices, ownerByCell, members)
        {
        }

        internal CAConstituentFrameSet(IntVec3 size, CellIndices indices,
            int[] ownerByCell, IReadOnlyList<PlanetTile> members)
        {
            IntVec3 center = new IntVec3(size.x / 2, 0, size.z / 2);
            this.ownerByCell = ownerByCell;
            int count = members.Count;
            var sumX = new long[count];
            var sumZ = new long[count];
            var area = new int[count];
            var minX = new int[count];
            var maxX = new int[count];
            var minZ = new int[count];
            var maxZ = new int[count];
            for (int i = 0; i < count; i++)
            {
                minX[i] = int.MaxValue; minZ[i] = int.MaxValue;
                maxX[i] = int.MinValue; maxZ[i] = int.MinValue;
            }
            for (int index = 0; index < ownerByCell.Length; index++)
            {
                int owner = ownerByCell[index];
                if (owner < 0 || owner >= count) continue;
                IntVec3 cell = indices.IndexToCell(index);
                sumX[owner] += cell.x;
                sumZ[owner] += cell.z;
                area[owner]++;
                if (cell.x < minX[owner]) minX[owner] = cell.x;
                if (cell.x > maxX[owner]) maxX[owner] = cell.x;
                if (cell.z < minZ[owner]) minZ[owner] = cell.z;
                if (cell.z > maxZ[owner]) maxZ[owner] = cell.z;
            }
            frames = new CAConstituentFrame[count];
            for (int i = 0; i < count; i++)
            {
                var frame = new CAConstituentFrame
                {
                    Index = i,
                    SourceTileId = members[i].tileId,
                    Area = area[i]
                };
                if (area[i] > 0)
                {
                    frame.Centroid = new IntVec3(
                        (int)(sumX[i] / area[i]), 0, (int)(sumZ[i] / area[i]));
                    frame.BoundingExtent = CellRect.FromLimits(
                        minX[i], minZ[i], maxX[i], maxZ[i]);
                    frame.EquivalentRadius = Mathf.Sqrt(area[i] / Mathf.PI);
                }
                else
                {
                    frame.Centroid = center;
                    frame.BoundingExtent = CellRect.SingleCell(center);
                    frame.EquivalentRadius = 1f;
                }
                frame.CharacteristicWidth = 2f * frame.EquivalentRadius;
                frames[i] = frame;
            }
        }

        internal int OwnerAtIndex(int cellIndex)
        {
            return cellIndex >= 0 && cellIndex < ownerByCell.Length
                ? ownerByCell[cellIndex] : -1;
        }

        internal bool Owns(int frameIndex, int cellIndex)
        {
            return OwnerAtIndex(cellIndex) == frameIndex;
        }

        internal string Summary()
        {
            return frames.Length + " constituent frames; area "
                + frames.Min(f => f.Area) + "-" + frames.Max(f => f.Area)
                + " cells, characteristic width "
                + frames.Min(f => f.CharacteristicWidth).ToString("F0") + "-"
                + frames.Max(f => f.CharacteristicWidth).ToString("F0");
        }
    }

    // One Mountain carrier's measured spatial behaviour. Recorded per carrier
    // rather than aggregated, because the F-131 defect was a carrier reaching
    // outside its own ownership - an aggregate total would have looked healthy
    // while the field was flooding a neighbor.
    internal sealed class CAMountainCarrierMeasure
    {
        internal int FrameIndex;
        internal int SourceTileId;
        internal int OwnedCells;
        internal float AxisDegrees;

        // The carrier's true reach along its own rotated Mountain axis, over
        // the cells it actually owns. Native uses map.Size.x, which on a
        // one-tile map IS this span; CharacteristicWidth is only an equal-area
        // surrogate for it and is kept alongside so the two can be compared.
        internal float AxisSpan;
        internal float CharacteristicWidth;

        internal int SeamWidth;
        internal int AddCellsInside;
        internal int AddCellsOutside;
        internal float PeakAdd;
    }

    // A projected feature occurrence. Identity is stable across preview and
    // generation because it is derived from persisted plan facts - the source
    // tile, the def, and the occurrence ordinal on that tile - never from
    // enumeration order. The candidate-plan lifecycle (P2) scribes these; until
    // then they are derived deterministically from the same inputs it will
    // persist, so the identity does not move when persistence lands.
    // PERSISTENCE-READY, NOT YET PERSISTED. P2's candidate plan scribes these;
    // until then they are derived from exactly the inputs it will persist, so
    // the identity does not move when persistence lands.
    //
    // Ordinal is an AUTHORED occurrence number for a def on a source tile - the
    // first Caves on tile 17506 is ordinal 0 and stays ordinal 0 whatever order
    // anything is enumerated in. It is never a collection index. FrameIndex is
    // an enumeration-derived convenience for reaching the frame and is
    // deliberately excluded from Id for that reason.
    internal sealed class CAFeatureInstance
    {
        internal readonly string CandidateId;
        internal readonly int SourceTileId;
        internal readonly string DefName;
        internal readonly int Ordinal;
        internal readonly int FrameIndex;

        internal CAFeatureInstance(string candidateId, int frameIndex,
            int sourceTileId, string defName, int ordinal)
        {
            CandidateId = candidateId ?? "";
            FrameIndex = frameIndex;
            SourceTileId = sourceTileId;
            DefName = defName;
            Ordinal = ordinal;
        }

        // Scoped to the candidate, so rerolling yields genuinely different
        // features rather than the same ones re-seeded, and confirming yields
        // exactly what the preview showed.
        internal string Id => CandidateId + "/" + SourceTileId + ":"
            + DefName + "#" + Ordinal;

        internal int SeedFor(int salt)
        {
            int seed = Gen.HashCombineInt(CandidateId.GetHashCode(),
                SourceTileId);
            seed = Gen.HashCombineInt(seed, DefName.GetHashCode());
            seed = Gen.HashCombineInt(seed, Ordinal);
            return Gen.HashCombineInt(seed, salt);
        }
    }

    internal static class CAFeatureRandom
    {
        // Deterministic per instance and per purpose. Native workers consume an
        // ambient, order-dependent stream; anything re-derived or driven by CA
        // takes its randomness from here instead, so a preview and the
        // generated map agree without depending on invocation order.
        internal static void Push(CAFeatureInstance instance, int salt)
        {
            Rand.PushState(instance.SeedFor(salt));
        }

        internal static void Pop()
        {
            Rand.PopState();
        }
    }

    internal static class CAConstituentMask
    {
        // The placement-driven execution path. Native scatter workers place
        // through predicates - CellFinder.TryFindRandomCell(map, validator,
        // out cell), TileMutatorWorker_Caves.ShouldCarve - and the predicate is
        // a parameter, so composing constituent ownership into it localises
        // placement without touching the worker. This is the same mechanism
        // already proven on the cave family.
        internal static Predicate<IntVec3> OwnedBy(Map map,
            CAConstituentFrameSet frames, int frameIndex,
            Predicate<IntVec3> inner)
        {
            return cell =>
            {
                if (!cell.InBounds(map)) return false;
                if (!frames.Owns(frameIndex,
                    map.cellIndices.CellToIndex(cell))) return false;
                return inner == null || inner(cell);
            };
        }

        internal static Predicate<IntVec3> OwnedByAny(Map map,
            CAConstituentFrameSet frames, HashSet<int> frameIndices,
            Predicate<IntVec3> inner)
        {
            return cell =>
            {
                if (!cell.InBounds(map)) return false;
                int owner = frames.OwnerAtIndex(
                    map.cellIndices.CellToIndex(cell));
                if (owner < 0 || !frameIndices.Contains(owner)) return false;
                return inner == null || inner(cell);
            };
        }
    }

    // Generation-local worker instances.
    //
    // TileMutatorDef.Worker is a per-Def singleton, so driving one def for
    // several constituents makes its fields last-writer-wins - and
    // TileMutatorWorker_AncientStructure genuinely depends on a field surviving
    // between two different gensteps. Constructing a private instance per
    // feature removes the collision instead of managing it. Verified safe:
    // across all 69 decompiled TileMutatorWorker_* classes, zero have a
    // non-trivial constructor; each is an empty pass-through to base(def).
    //
    // CAVEAT, deliberately not solved here. Native RUNTIME code reaches the
    // singleton as its access path - Tile.cs resolves secondary biome through
    // (def.Worker as TileMutatorWorker_MixedBiome), AggressiveAnimalIncident-
    // Utility casts TileMutatorDefOf.AnimalHabitat.Worker, and the wild animal
    // and plant spawners call mutator.Worker for commonality. Private instances
    // are therefore for GENERATION-LOCAL state only. Any worker whose state is
    // read later through def.Worker must additionally update the singleton, and
    // that is a per-worker decision at the point of use, not a blanket rule.
    internal static class CAPrivateWorkers
    {
        private static readonly System.Reflection.FieldInfo WorkerClassField =
            AccessTools.Field(typeof(TileMutatorDef), "workerClass");

        private static readonly Dictionary<string, TileMutatorWorker> Cache =
            new Dictionary<string, TileMutatorWorker>();

        // The cache must span one map's WHOLE generation, because
        // TileMutatorWorker_AncientStructure writes structurePerimeterRect in
        // GenerateCriticalStructures and reads it in
        // GenerateNonCriticalStructures - two different gensteps. It must NOT
        // span maps, or a private instance carries an earlier map's state and
        // reintroduces exactly the staleness it exists to remove. Callers
        // clear once per generated map, keyed on map.uniqueID.
        private static int cachedForMap = -1;

        internal static void BeginMap(int mapUniqueId)
        {
            if (cachedForMap == mapUniqueId) return;
            cachedForMap = mapUniqueId;
            Cache.Clear();
        }

        internal static TileMutatorWorker For(TileMutatorDef def,
            CAFeatureInstance instance)
        {
            if (def == null || instance == null) return null;
            string key = instance.Id;
            TileMutatorWorker cached;
            if (Cache.TryGetValue(key, out cached)) return cached;
            TileMutatorWorker worker = Construct(def);
            if (worker == null) worker = def.Worker;
            Cache[key] = worker;
            return worker;
        }

        private static TileMutatorWorker Construct(TileMutatorDef def)
        {
            try
            {
                var type = WorkerClassField?.GetValue(def) as Type;
                if (type == null) return null;
                return (TileMutatorWorker)Activator.CreateInstance(type, def);
            }
            catch (Exception exception)
            {
                Log.Warning("[CA][Regional] private worker construction failed "
                    + "for " + def.defName + ", falling back to the shared "
                    + "singleton: " + exception.Message);
                return null;
            }
        }
    }
}
