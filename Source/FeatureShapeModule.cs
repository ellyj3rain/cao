using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // CANONICAL AUTHORED FEATURE SHAPE.
    //
    // Chain: native/source feature identity -> deterministic
    // identity-seeded default realization -> canonical CA shape state
    // (per-family admissible degrees of freedom, below) -> coarse
    // authoring controls -> optional direct spatial editing. Every
    // interaction surface consumes this one state; none owns it. Absent
    // state, or an absent key, means the deterministic default -- an
    // unauthored feature is byte-identical to today's realization.
    //
    // Families keep their own degrees of freedom because the native
    // workers parameterize differently: an atoll has a lagoon and an
    // elongation, a fjord has only a width and a bearing, a wetland has
    // only coverage and a variant. One universal shape vector would
    // destroy exactly the semantic identity the projection preserves.
    public sealed class CAAuthoredFeatureShape : IExposable
    {
        public int tileId = -1;
        // The carried mutator's defName. One tile can carry several
        // shapeable features; each keys its own record.
        public string feature;
        public List<string> keys = new List<string>();
        public List<float> values = new List<float>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref tileId, "tileId", -1);
            Scribe_Values.Look(ref feature, "feature");
            Scribe_Collections.Look(ref keys, "keys", LookMode.Value);
            Scribe_Collections.Look(ref values, "values", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                keys = keys ?? new List<string>();
                values = values ?? new List<float>();
                while (values.Count < keys.Count) values.Add(0f);
                while (keys.Count < values.Count)
                    values.RemoveAt(values.Count - 1);
            }
        }

        internal bool Has(string key)
        {
            return keys != null && keys.Contains(key);
        }

        internal float Value(string key, float fallback)
        {
            int index = keys == null ? -1 : keys.IndexOf(key);
            return index < 0 ? fallback : values[index];
        }

        internal void Set(string key, float value)
        {
            int index = keys.IndexOf(key);
            if (index < 0)
            {
                keys.Add(key);
                values.Add(value);
            }
            else values[index] = value;
        }

        internal void Clear(string key)
        {
            int index = keys.IndexOf(key);
            if (index < 0) return;
            keys.RemoveAt(index);
            values.RemoveAt(index);
        }
    }

    internal sealed class CAFeatureDegree
    {
        internal string Key;
        internal string Label;
        internal float Min;
        internal float Max;
        internal bool Integer;
        internal string LeftWord;
        internal string RightWord;
        internal string Effect;
    }

    internal static class CAFeatureShapeModel
    {
        // Shared degree builders. "span"-family values are multipliers of
        // the native extent (1 = native); the rest are the native
        // parameter's own units within an identity-preserving band.
        private static CAFeatureDegree Span(float min = 0.7f,
            float max = 1.3f)
        {
            return new CAFeatureDegree
            {
                Key = "span", Label = "Extent", Min = min, Max = max,
                LeftWord = "smaller", RightWord = "larger",
                Effect = "How much ground the feature occupies."
            };
        }

        private static CAFeatureDegree Width()
        {
            return new CAFeatureDegree
            {
                Key = "span", Label = "Width", Min = 0.6f, Max = 1.6f,
                LeftWord = "narrower", RightWord = "wider",
                Effect = "How wide the feature's channel runs."
            };
        }

        private static CAFeatureDegree Orientation(float max = 360f)
        {
            return new CAFeatureDegree
            {
                Key = "orientation", Label = "Bearing", Min = 0f, Max = max,
                LeftWord = "0", RightWord = max.ToString("0"),
                Effect = "The compass direction the feature runs or faces."
            };
        }

        private static CAFeatureDegree Stretch(float min, float max)
        {
            // The squash factor multiplies one axis of the distance. Above
            // 1 a larger value elongates; below 1 (the coast shapes) a
            // SMALLER value elongates, so the anchor words flip with the
            // band.
            bool inverted = max <= 1f;
            return new CAFeatureDegree
            {
                Key = "stretch", Label = "Stretch", Min = min, Max = max,
                LeftWord = inverted ? "longer" : "rounder",
                RightWord = inverted ? "rounder" : "longer",
                Effect = "How drawn-out the feature runs along its axis."
            };
        }

        private static CAFeatureDegree Variant()
        {
            return new CAFeatureDegree
            {
                Key = "variant", Label = "Variation", Min = 0f, Max = 9f,
                Integer = true, LeftWord = "", RightWord = "",
                Effect = "A different realization of the same feature: "
                    + "same kind, same scale, different lie of the land."
            };
        }

        private static CAFeatureDegree WanderX()
        {
            return new CAFeatureDegree
            {
                Key = "wanderX", Label = "Position east-west", Min = -0.4f,
                Max = 0.4f, LeftWord = "west", RightWord = "east",
                Effect = "Where the feature sits within its area."
            };
        }

        private static CAFeatureDegree WanderZ()
        {
            return new CAFeatureDegree
            {
                Key = "wanderZ", Label = "Position north-south",
                Min = -0.4f, Max = 0.4f, LeftWord = "south",
                RightWord = "north",
                Effect = "Where the feature sits within its area."
            };
        }

        // Family key per worker type; null = not shapeable (either CA
        // topology owns it wholesale, or it has no projected geometry).
        internal static string FamilyOf(TileMutatorWorker worker)
        {
            if (worker is TileMutatorWorker_Basin) return "basin";
            if (worker is TileMutatorWorker_Oasis) return "oasis";
            if (worker is TileMutatorWorker_LavaCrater
                || worker is TileMutatorWorker_LavaLake) return "lava-basin";
            if (worker is TileMutatorWorker_LakeWithIslands
                || worker is TileMutatorWorker_LakeWithIsland
                || worker is TileMutatorWorker_ToxicLake
                || worker is TileMutatorWorker_DryLake
                || worker is TileMutatorWorker_Pond
                || worker is TileMutatorWorker_Lake) return "water";
            if (worker is TileMutatorWorker_CoastalAtoll) return "atoll";
            if (worker is TileMutatorWorker_Cove) return "cove";
            if (worker is TileMutatorWorker_Archipelago)
                return "archipelago";
            if (worker is TileMutatorWorker_Bay) return "bay";
            if (worker is TileMutatorWorker_Fjord) return "fjord";
            if (worker is TileMutatorWorker_Peninsula) return "peninsula";
            if (worker is TileMutatorWorker_CoastalIsland)
                return "coastal-island";
            if (worker is TileMutatorWorker_Iceberg) return "iceberg";
            if (worker is TileMutatorWorker_Valley) return "valley";
            if (worker is TileMutatorWorker_Cliffs) return "cliffs";
            if (worker is TileMutatorWorker_Plateau) return "plateau";
            if (worker is TileMutatorWorker_Crevasse) return "crevasse";
            if (worker is TileMutatorWorker_Hollow) return "hollow";
            if (worker is TileMutatorWorker_Chasm) return "chasm";
            if (worker is TileMutatorWorker_HotSprings) return "springs";
            if (worker is TileMutatorWorker_Wetland) return "wetland";
            if (worker is TileMutatorWorker_LavaFlow) return "lava-flow";
            if (worker is TileMutatorWorker_IceDunes) return "ice-dunes";
            if (worker is TileMutatorWorker_Dunes) return "dunes";
            if (worker is TileMutatorWorker_TerraformingScar)
                return "scar";
            return null;
        }

        internal static CAFeatureDegree[] DegreesFor(string family)
        {
            switch (family)
            {
                case "water":
                    return new[] { Span(0.6f, 1.4f),
                        Stretch(1f, 1.5f), Orientation(), WanderX(),
                        WanderZ(), Variant() };
                case "oasis":
                case "lava-basin":
                    return new[] { Span(0.6f, 1.4f), Stretch(1f, 1.5f),
                        Orientation(), WanderX(), WanderZ(), Variant() };
                case "basin":
                    return new[] { Span(0.6f, 1.4f),
                        new CAFeatureDegree
                        {
                            Key = "orientation", Label = "Entrance bearing",
                            Min = 0f, Max = 360f, LeftWord = "0",
                            RightWord = "360",
                            Effect = "The direction the basin's wide "
                                + "entrance opens."
                        },
                        new CAFeatureDegree
                        {
                            Key = "corridors", Label = "Extra corridors",
                            Min = 1f, Max = 2f, Integer = true,
                            Effect = "How many narrow side entrances cut "
                                + "the rim."
                        }, Variant() };
                case "atoll":
                    return new[] { Span(0.7f, 1.4f),
                        new CAFeatureDegree
                        {
                            Key = "elongation", Label = "Ring shape",
                            Min = 0.45f, Max = 1f, LeftWord = "drawn-out",
                            RightWord = "round",
                            Effect = "A drawn-out ring reads as a chain; "
                                + "a round one as a classic atoll."
                        },
                        new CAFeatureDegree
                        {
                            Key = "lagoon", Label = "Lagoon",
                            Min = 0.35f, Max = 0.65f, LeftWord = "tighter",
                            RightWord = "wider",
                            Effect = "How much open water the ring holds."
                        }, Orientation(), Variant() };
                case "cove":
                    return new[] { Span(0.7f, 1.3f),
                        new CAFeatureDegree
                        {
                            Key = "mouth", Label = "Mouth offset",
                            Min = -0.15f, Max = 0.15f, LeftWord = "one way",
                            RightWord = "the other",
                            Effect = "Slides the cove's entrance along "
                                + "the shore."
                        }, Orientation(), Variant() };
                case "archipelago":
                    return new[] { Span(0.7f, 1.3f),
                        new CAFeatureDegree
                        {
                            Key = "density", Label = "Island density",
                            Min = -0.12f, Max = 0.06f, LeftWord = "sparser",
                            RightWord = "denser",
                            Effect = "How much land stands out of the "
                                + "island field."
                        }, Variant() };
                case "bay":
                    return new[] { Span(0.7f, 1.3f), Stretch(0.6f, 0.95f),
                        Orientation(), Variant() };
                case "fjord":
                case "valley":
                case "crevasse":
                    return new[] { Width(),
                        Orientation(family == "crevasse" ? 180f : 360f),
                        Variant() };
                case "peninsula":
                    return new[] { Span(0.7f, 1.3f),
                        new CAFeatureDegree
                        {
                            Key = "length", Label = "Reach", Min = 0.7f,
                            Max = 1.1f, LeftWord = "shorter",
                            RightWord = "farther",
                            Effect = "How far the tongue runs into open "
                                + "water."
                        }, Orientation(), Variant() };
                case "coastal-island":
                    return new[] { Span(0.7f, 1.3f), Stretch(0.6f, 1f),
                        Orientation(), Variant() };
                case "iceberg":
                    return new[] { Span(0.7f, 1.3f), Stretch(0.6f, 0.95f),
                        Orientation(), Variant() };
                case "cliffs":
                case "hollow":
                    return new[] { Span(0.7f, 1.3f), Orientation(),
                        Variant() };
                case "plateau":
                    return new[] { Span(0.7f, 1.3f), WanderX(), WanderZ(),
                        Variant() };
                case "chasm":
                    return new[] { Span(0.7f, 1.3f), Stretch(1f, 1.5f),
                        new CAFeatureDegree
                        {
                            Key = "corridors", Label = "Entrances",
                            Min = 3f, Max = 6f, Integer = true,
                            Effect = "How many passages cut the enclosure."
                        }, Orientation(), Variant() };
                case "springs":
                    return new[] { Span(0.7f, 1.3f),
                        new CAFeatureDegree
                        {
                            Key = "pools", Label = "Pools", Min = -0.1f,
                            Max = 0.1f, LeftWord = "fewer",
                            RightWord = "more",
                            Effect = "How much of the area breaks into "
                                + "spring water."
                        }, WanderX(), WanderZ(), Variant() };
                case "wetland":
                    return new[]
                    {
                        new CAFeatureDegree
                        {
                            Key = "coverage", Label = "Coverage",
                            Min = -0.15f, Max = 0.15f, LeftWord = "drier",
                            RightWord = "wetter",
                            Effect = "How much of the area turns to mud "
                                + "and water."
                        }, Variant()
                    };
                case "lava-flow":
                    return new[]
                    {
                        new CAFeatureDegree
                        {
                            Key = "veins", Label = "Rock veins",
                            Min = -0.05f, Max = 0.05f, LeftWord = "fewer",
                            RightWord = "more",
                            Effect = "How much standing rock survives the "
                                + "flow."
                        }, Variant()
                    };
                case "dunes":
                case "ice-dunes":
                case "scar":
                    return new[] { Orientation(180f), Variant() };
                default:
                    return null;
            }
        }

        internal static bool Shapeable(TileMutatorDef def)
        {
            return def?.Worker != null && FamilyOf(def.Worker) != null;
        }

        // The single resolution seam every projection pass uses. Unset =
        // the deterministic default the caller computed. A stored value
        // is clamped to its family's admissible band on the way out, so a
        // hand-edited or stale save can never push a basin past what the
        // feature can naturally be.
        internal static float Value(List<CAAuthoredFeatureShape> shapes,
            int tileId, string feature, string key, float fallback)
        {
            if (shapes == null) return fallback;
            for (int i = 0; i < shapes.Count; i++)
            {
                CAAuthoredFeatureShape shape = shapes[i];
                if (shape == null || shape.tileId != tileId
                    || shape.feature != feature) continue;
                if (!shape.Has(key)) return fallback;
                float stored = shape.Value(key, fallback);
                CAFeatureDegree degree = DegreeOf(feature, key);
                return degree == null ? stored
                    : Mathf.Clamp(stored, degree.Min, degree.Max);
            }
            return fallback;
        }

        private static CAFeatureDegree DegreeOf(string feature, string key)
        {
            TileMutatorDef def = DefDatabase<TileMutatorDef>.GetNamedSilentFail(
                feature);
            CAFeatureDegree[] degrees = def?.Worker == null ? null
                : DegreesFor(FamilyOf(def.Worker));
            if (degrees == null) return null;
            for (int i = 0; i < degrees.Length; i++)
                if (degrees[i].Key == key) return degrees[i];
            return null;
        }

        internal static int VariantOf(List<CAAuthoredFeatureShape> shapes,
            int tileId, string feature)
        {
            return Mathf.RoundToInt(Value(shapes, tileId, feature,
                "variant", 0f));
        }

        // Variant folds into the feature's identity salt, so variant 0 is
        // byte-identical to the unauthored realization.
        internal static int SaltWithVariant(int salt,
            List<CAAuthoredFeatureShape> shapes, int tileId, string feature)
        {
            int variant = VariantOf(shapes, tileId, feature);
            return variant == 0 ? salt
                : Gen.HashCombineInt(salt, 0x56415249 + variant);
        }

        // COPY-ON-WRITE. The generated preview reads authored shapes on
        // its worker thread (projection requests capture the reference,
        // and the composition contract enumerates it mid-generation)
        // while the editor writes on the main thread. Every mutation
        // therefore rebuilds the whole list and swaps the plan's
        // reference atomically -- a reader holding the previous
        // reference keeps a consistent snapshot, and no shape object is
        // ever mutated after publication.
        internal static void Mutate(CARegionalPlan plan,
            Action<List<CAAuthoredFeatureShape>> edit)
        {
            var next = (plan.featureShapes
                    ?? new List<CAAuthoredFeatureShape>())
                .Where(shape => shape != null)
                .Select(shape => new CAAuthoredFeatureShape
                {
                    tileId = shape.tileId,
                    feature = shape.feature,
                    keys = shape.keys?.ToList() ?? new List<string>(),
                    values = shape.values?.ToList() ?? new List<float>()
                }).ToList();
            edit(next);
            next.RemoveAll(shape => shape == null || shape.keys == null
                || shape.keys.Count == 0);
            plan.featureShapes = next;
        }

        internal static void Write(CARegionalPlan plan, int tileId,
            string feature, string key, float value)
        {
            Mutate(plan, shapes =>
            {
                CAAuthoredFeatureShape shape = shapes.FirstOrDefault(
                    item => item.tileId == tileId
                        && item.feature == feature);
                if (shape == null)
                {
                    shape = new CAAuthoredFeatureShape
                    {
                        tileId = tileId,
                        feature = feature
                    };
                    shapes.Add(shape);
                }
                shape.Set(key, value);
            });
        }

        // key null clears the whole feature back to as-found.
        internal static void Erase(CARegionalPlan plan, int tileId,
            string feature, string key)
        {
            Mutate(plan, shapes =>
            {
                CAAuthoredFeatureShape shape = shapes.FirstOrDefault(
                    item => item.tileId == tileId
                        && item.feature == feature);
                if (shape == null) return;
                if (key == null)
                {
                    shape.keys.Clear();
                    shape.values.Clear();
                }
                else shape.Clear(key);
            });
        }

        // Deterministic identity segment for composition signatures: any
        // authored value changes the generated geography, so it changes
        // the composition's identity everywhere identity is compared.
        internal static IEnumerable<string> CanonicalSegments(
            CARegionalPlan plan)
        {
            if (plan?.featureShapes == null) yield break;
            foreach (CAAuthoredFeatureShape shape in plan.featureShapes
                .Where(item => item != null && item.keys != null
                    && item.keys.Count > 0)
                .OrderBy(item => item.tileId)
                .ThenBy(item => item.feature, StringComparer.Ordinal))
                for (int i = 0; i < shape.keys.Count; i++)
                    yield return "shape=" + shape.tileId + ":"
                        + shape.feature + ":" + shape.keys[i] + "="
                        + shape.values[Math.Min(i, shape.values.Count - 1)]
                            .ToString("F3");
        }

        internal static long FoldSignature(long signature,
            CARegionalPlan plan)
        {
            unchecked
            {
                foreach (string segment in CanonicalSegments(plan))
                    signature = signature * 31L + segment.GetHashCode();
            }
            return signature;
        }
    }

    // Coarse authoring controls over the canonical state: one editor for
    // every shapeable feature one selected area carries. Values write the
    // shared state; the diagram, the generated preview, and generation all
    // re-derive from it through the composition identity.
    internal sealed class Dialog_CAFeatureShapeEditor : Window
    {
        private readonly CARegionalPlan plan;
        private readonly int tileId;
        private readonly List<TileMutatorDef> features;
        private Vector2 scroll;
        private float viewHeight;
        private bool changed;
        // A slider writes the canonical state only on release: every
        // authored value changes the composition's identity, and identity
        // changes regenerate the diagram kernel and the Map Preview --
        // per-tick writes would thrash both while dragging.
        private string pendingFeature;
        private string pendingKey;
        private float pendingValue;
        private static float lastPreviewNudge;

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(700f, UI.screenWidth - 48f),
            Mathf.Min(680f, UI.screenHeight - 48f));

        protected override float Margin => 0f;

        internal Dialog_CAFeatureShapeEditor(CARegionalPlan plan,
            int tileId)
        {
            this.plan = plan;
            this.tileId = tileId;
            PlanetTile tile = CARegionalPlanUtility.SurfaceTile(tileId);
            features = (tile.Valid ? tile.Tile?.Mutators : null)
                ?.Where(CAFeatureShapeModel.Shapeable).ToList()
                ?? new List<TileMutatorDef>();
            doCloseX = false;
            doCloseButton = false;
            doWindowBackground = false;
            // The editor coexists with the generated preview so spatial
            // handles stay draggable while it is open; the window itself
            // moves out of the way.
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            draggable = true;
        }

        internal CARegionalPlan Plan => plan;
        internal int TileId => tileId;

        // Direct spatial editing commits through the same seam the rows
        // use: one canonical state, several hands on it.
        internal void CommitWander(string featureDef, float wanderX,
            float wanderZ)
        {
            CAFeatureShapeModel.Mutate(plan, shapes =>
            {
                CAAuthoredFeatureShape shape = shapes.FirstOrDefault(
                    item => item.tileId == tileId
                        && item.feature == featureDef);
                if (shape == null)
                {
                    shape = new CAAuthoredFeatureShape
                    {
                        tileId = tileId,
                        feature = featureDef
                    };
                    shapes.Add(shape);
                }
                shape.Set("wanderX", Mathf.Clamp(wanderX, -0.4f, 0.4f));
                shape.Set("wanderZ", Mathf.Clamp(wanderZ, -0.4f, 0.4f));
            });
            MarkChanged();
        }

        public override void DoWindowContents(Rect inRect)
        {
            inRect = CAOpeningTheme.BeginWindowSurface(inRect);
            try
            {
                DoEditorContents(inRect);
            }
            finally
            {
                CAOpeningTheme.EndWindowSurface();
            }
        }

        private void DoEditorContents(Rect inRect)
        {
            CAOpeningTheme.Heading(0f, 0f, inRect.width,
                "Shape the land's features");
            float introductionHeight = CAOpeningTheme.Fine(0f, 36f,
                inRect.width, CARegionalPlanUtility.TileWords(tileId)
                + ". Each control bends this feature within what it can "
                + "naturally be; untouched controls keep the land exactly "
                + "as found. The preview regenerates from the same shape "
                + "the real map will use.");
            float y = 44f + introductionHeight;

            Rect outRect = new Rect(0f, y, inRect.width,
                inRect.height - y - 46f);
            Rect view = new Rect(0f, 0f, outRect.width - 18f,
                Mathf.Max(outRect.height, viewHeight));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float rowY = 0f;
            foreach (TileMutatorDef feature in features)
                DrawFeature(ref rowY, view.width, feature);
            viewHeight = rowY + 8f;
            Widgets.EndScrollView();

            CAOpeningTheme.Divider(0f, inRect.height - 42f, inRect.width);
            if (CAOpeningTheme.PrimaryButton(new Rect(inRect.width - 150f,
                    inRect.height - 36f, 150f, 34f), "Done"))
                Close();
        }

        private void DrawFeature(ref float y, float width,
            TileMutatorDef feature)
        {
            string family = CAFeatureShapeModel.FamilyOf(feature.Worker);
            CAFeatureDegree[] degrees =
                CAFeatureShapeModel.DegreesFor(family);
            if (degrees == null) return;
            CAAuthoredFeatureShape shape = plan.featureShapes
                ?.FirstOrDefault(item => item != null
                    && item.tileId == tileId
                    && item.feature == feature.defName);

            y += CAOpeningTheme.SectionLabel(0f, y, width,
                (feature.label ?? feature.defName).CapitalizeFirst());
            bool anyAuthored = shape != null && shape.keys.Count > 0;
            if (anyAuthored && CAOpeningTheme.GhostButton(new Rect(
                    width - 130f, y - 26f, 130f, 24f), "As found",
                    "Forget every authored value; the feature returns to "
                    + "exactly the land as generated."))
            {
                CAFeatureShapeModel.Erase(plan, tileId, feature.defName,
                    null);
                MarkChanged();
            }

            foreach (CAFeatureDegree degree in degrees)
            {
                bool authored = shape != null && shape.Has(degree.Key);
                float middle = degree.Min + (degree.Max - degree.Min) / 2f;
                float current = authored
                    ? shape.Value(degree.Key, middle) : middle;
                var label = new Rect(0f, y, 170f, 26f);
                GUI.color = authored ? Color.white : CAOpeningTheme.TextLo;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(label, degree.Label);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                TooltipHandler.TipRegion(label, degree.Effect
                    + (authored ? "" : "\n\nCurrently as found."));

                if (degree.Integer)
                {
                    var minus = new Rect(180f, y, 26f, 24f);
                    var plus = new Rect(276f, y, 26f, 24f);
                    var value = new Rect(208f, y, 66f, 24f);
                    int shown = Mathf.RoundToInt(current);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = authored ? Color.white
                        : CAOpeningTheme.TextLo;
                    Widgets.Label(value, authored
                        ? shown.ToString() : "as found");
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                    if (CAOpeningTheme.GhostButton(minus, "-"))
                    {
                        SetValue(feature, degree, Mathf.Clamp(
                            (authored ? shown : Mathf.RoundToInt(middle))
                            - 1, (int)degree.Min, (int)degree.Max));
                    }
                    if (CAOpeningTheme.GhostButton(plus, "+"))
                    {
                        SetValue(feature, degree, Mathf.Clamp(
                            (authored ? shown : Mathf.RoundToInt(middle))
                            + 1, (int)degree.Min, (int)degree.Max));
                    }
                }
                else
                {
                    var sliderRect = new Rect(180f, y + 2f, width - 320f,
                        24f);
                    float roundTo = (degree.Max - degree.Min) / 40f;
                    bool thisPending = pendingFeature == feature.defName
                        && pendingKey == degree.Key;
                    float shown = thisPending ? pendingValue : current;
                    float slid = Widgets.HorizontalSlider(sliderRect,
                        shown, degree.Min, degree.Max, true,
                        authored || thisPending ? null : "as found",
                        degree.LeftWord, degree.RightWord, roundTo);
                    if (!Mathf.Approximately(slid, shown))
                    {
                        pendingFeature = feature.defName;
                        pendingKey = degree.Key;
                        pendingValue = slid;
                    }
                    if (thisPending && !Input.GetMouseButton(0))
                    {
                        SetValue(feature, degree, pendingValue);
                        pendingFeature = null;
                        pendingKey = null;
                    }
                }
                var clear = new Rect(width - 120f, y, 110f, 24f);
                if (authored && CAOpeningTheme.GhostButton(clear,
                        "as found"))
                {
                    CAFeatureShapeModel.Erase(plan, tileId,
                        feature.defName, degree.Key);
                    MarkChanged();
                }
                y += 30f;
            }
            y += 8f;
        }

        private void SetValue(TileMutatorDef feature,
            CAFeatureDegree degree, float value)
        {
            CAFeatureShapeModel.Write(plan, tileId, feature.defName,
                degree.Key, value);
            MarkChanged();
        }

        private void MarkChanged()
        {
            changed = true;
            // Identity changed: the diagram's composition cache and the
            // generated preview both re-key from the plan's signature; the
            // preview window additionally needs a refresh nudge, bounded
            // so rapid successive commits queue one regeneration, not one
            // per adjustment.
            if (Time.realtimeSinceStartup - lastPreviewNudge > 0.6f)
            {
                lastPreviewNudge = Time.realtimeSinceStartup;
                CARegionalCompatibility.NotifyPreviewChanged();
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            if (!changed) return;
            CARegionalSetupSession.SavePending();
            CARegionalCompatibility.NotifyPreviewChanged();
        }
    }
}
