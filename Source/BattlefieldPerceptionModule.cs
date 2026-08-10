using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // This profile is the resolved observer, not a semantic guess based on a trait
    // name. Sight already folds in damaged and missing eyes, added parts, hediff
    // capacity modifiers, implants, and gene capacity modifiers. The two native
    // distance-accuracy stats then carry traits, genes, gear, and modded stat sources
    // that alter long-range visual acuity without pretending that detection is a shot.
    internal readonly struct CAVisionProfile
    {
        public readonly bool CanUseVision;
        public readonly float SightCapacity;
        public readonly float MediumAccuracyFactor;
        public readonly float LongAccuracyFactor;
        public readonly float DistanceAcuity;
        public readonly float DarkAdaptation;
        public readonly int Shooting;
        public readonly int Intellectual;
        public readonly string SourceSummary;

        public CAVisionProfile(bool canUseVision, float sightCapacity,
            float mediumAccuracyFactor, float longAccuracyFactor,
            float distanceAcuity, float darkAdaptation, int shooting,
            int intellectual, string sourceSummary)
        {
            CanUseVision = canUseVision;
            SightCapacity = sightCapacity;
            MediumAccuracyFactor = mediumAccuracyFactor;
            LongAccuracyFactor = longAccuracyFactor;
            DistanceAcuity = distanceAcuity;
            DarkAdaptation = darkAdaptation;
            Shooting = shooting;
            Intellectual = intellectual;
            SourceSummary = sourceSummary;
        }

        public string TraceText()
        {
            return "Sight " + SightCapacity.ToString("F2")
                + ", medium acuity stat "
                + MediumAccuracyFactor.ToString("F2")
                + ", long acuity stat " + LongAccuracyFactor.ToString("F2")
                + ", moderated distance acuity "
                + DistanceAcuity.ToString("F2") + ", dark adaptation "
                + DarkAdaptation.ToString("F2") + ", Shooting " + Shooting
                + ", Intellectual " + Intellectual + ", vision usable "
                + CanUseVision + ", resolved sources: " + SourceSummary;
        }
    }

    // Visual awareness is not weapon range. This result describes whether an actor
    // can notice and resolve one body through the current map, while the combat
    // consumer remains responsible for deciding whether its weapon can engage it.
    internal readonly struct CAVisualPerception
    {
        public readonly bool Detected;
        public readonly bool Identified;
        public readonly float Distance;
        public readonly float MapDiagonal;
        public readonly float ClearRange;
        public readonly float EffectiveRange;
        public readonly float Confidence;
        public readonly float UncertaintyCells;
        public readonly float SoftOcclusion;
        public readonly int PlantCells;
        public readonly int TreeCells;
        public readonly int PartialCoverCells;
        public readonly int SmokeCells;
        public readonly float TargetGlow;
        public readonly float SightCapacity;
        public readonly float DistanceAcuity;
        public readonly float MediumAccuracyFactor;
        public readonly float LongAccuracyFactor;
        public readonly float DarkAdaptation;
        public readonly float SightRangeFactor;
        public readonly float LightFactor;
        public readonly float WeatherFactor;
        public readonly float SkillFactor;
        public readonly int Shooting;
        public readonly int Intellectual;
        public readonly string LimitingFactor;
        public readonly string VisionSources;

        public CAVisualPerception(bool detected, bool identified,
            float distance, float mapDiagonal, float clearRange,
            float effectiveRange, float confidence, float uncertaintyCells,
            float softOcclusion, int plantCells, int treeCells,
            int partialCoverCells, int smokeCells, float targetGlow,
            CAVisionProfile profile, float sightRangeFactor,
            float lightFactor, float weatherFactor, float skillFactor,
            string limitingFactor)
        {
            Detected = detected;
            Identified = identified;
            Distance = distance;
            MapDiagonal = mapDiagonal;
            ClearRange = clearRange;
            EffectiveRange = effectiveRange;
            Confidence = confidence;
            UncertaintyCells = uncertaintyCells;
            SoftOcclusion = softOcclusion;
            PlantCells = plantCells;
            TreeCells = treeCells;
            PartialCoverCells = partialCoverCells;
            SmokeCells = smokeCells;
            TargetGlow = targetGlow;
            SightCapacity = profile.SightCapacity;
            DistanceAcuity = profile.DistanceAcuity;
            MediumAccuracyFactor = profile.MediumAccuracyFactor;
            LongAccuracyFactor = profile.LongAccuracyFactor;
            DarkAdaptation = profile.DarkAdaptation;
            SightRangeFactor = sightRangeFactor;
            LightFactor = lightFactor;
            WeatherFactor = weatherFactor;
            SkillFactor = skillFactor;
            Shooting = profile.Shooting;
            Intellectual = profile.Intellectual;
            LimitingFactor = limitingFactor;
            VisionSources = profile.SourceSummary;
        }

        public string TraceText()
        {
            return "distance " + Distance.ToString("F1")
                + " / map diagonal " + MapDiagonal.ToString("F1")
                + ", clear horizon " + ClearRange.ToString("F1")
                + ", effective range " + EffectiveRange.ToString("F1")
                + ", confidence " + Confidence.ToString("F2")
                + ", uncertainty " + UncertaintyCells.ToString("F1")
                + " cells, soft occlusion " + SoftOcclusion.ToString("F2")
                + " [plants " + PlantCells + ", trees " + TreeCells
                + ", partial cover " + PartialCoverCells + ", smoke "
                + SmokeCells + "], target glow " + TargetGlow.ToString("F2")
                + ", Sight " + SightCapacity.ToString("F2")
                + " (range factor " + SightRangeFactor.ToString("F2") + ")"
                + ", medium/long acuity "
                + MediumAccuracyFactor.ToString("F2") + "/"
                + LongAccuracyFactor.ToString("F2")
                + " -> distance factor " + DistanceAcuity.ToString("F2")
                + ", dark adaptation " + DarkAdaptation.ToString("F2")
                + " -> light factor " + LightFactor.ToString("F2")
                + ", weather factor " + WeatherFactor.ToString("F2")
                + ", skill factor " + SkillFactor.ToString("F2")
                + " [Shooting " + Shooting + ", Intellectual "
                + Intellectual + "], limiting factor " + LimitingFactor
                + ", identified " + Identified + ", sources "
                + VisionSources;
        }
    }

    internal static class CABattlefieldPerception
    {
        private const float MinimumClearRange = 180f;
        private const float DetectionThreshold = 0.18f;
        private const float IdentificationThreshold = 0.42f;
        private const int PairCacheTicks = 15;
        private const int ProfileCacheTicks = 30;

        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            private readonly int mapId;
            private readonly int observerId;
            private readonly int subjectId;
            private readonly int maximumRangeTenths;

            public CacheKey(int mapId, int observerId, int subjectId,
                float maximumRange)
            {
                this.mapId = mapId;
                this.observerId = observerId;
                this.subjectId = subjectId;
                maximumRangeTenths = float.IsPositiveInfinity(maximumRange)
                    ? int.MaxValue : Mathf.RoundToInt(maximumRange * 10f);
            }

            public bool Equals(CacheKey other)
            {
                return mapId == other.mapId && observerId == other.observerId
                    && subjectId == other.subjectId
                    && maximumRangeTenths == other.maximumRangeTenths;
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey && Equals((CacheKey)obj);
            }

            public override int GetHashCode()
            {
                return Gen.HashCombineInt(mapId, observerId, subjectId,
                    maximumRangeTenths);
            }
        }

        private sealed class CacheEntry
        {
            public int Tick;
            public IntVec3 ObserverCell;
            public IntVec3 SubjectCell;
            public CAVisualPerception Result;
        }

        private sealed class ProfileCacheEntry
        {
            public int Tick;
            public int MapId;
            public CAVisionProfile Profile;
        }

        private sealed class OcclusionSample
        {
            public float Penalty;
            public int PlantCells;
            public int TreeCells;
            public int PartialCoverCells;
            public int SmokeCells;
        }

        private static readonly Dictionary<CacheKey, CacheEntry> cache =
            new Dictionary<CacheKey, CacheEntry>();
        private static readonly Dictionary<int, ProfileCacheEntry> profileCache =
            new Dictionary<int, ProfileCacheEntry>();

        internal static void ClearTransient()
        {
            cache.Clear();
            profileCache.Clear();
        }

        internal static float ClearRangeFor(Map map)
        {
            if (map == null) return MinimumClearRange;
            // The horizon grows with the materialized theater. It is intentionally not
            // capped at the old 420-cell prototype value; hard LOS and the map boundary
            // remain the physical limits.
            return Mathf.Max(MinimumClearRange,
                Mathf.Max(map.Size.x, map.Size.z) * 0.80f);
        }

        internal static CAVisionProfile VisionProfileFor(Pawn pawn)
        {
            if (pawn == null)
                return new CAVisionProfile(false, 0f, 0f, 0f, 0f, 0f,
                    0, 0, "no pawn");
            int now = Find.TickManager?.TicksGame ?? 0;
            int mapId = pawn.Map?.uniqueID ?? -1;
            ProfileCacheEntry cached;
            if (profileCache.TryGetValue(pawn.thingIDNumber, out cached)
                && cached.MapId == mapId && now - cached.Tick >= 0
                && now - cached.Tick <= ProfileCacheTicks)
                return cached.Profile;

            CAVisionProfile profile = BuildVisionProfile(pawn);
            profileCache[pawn.thingIDNumber] = new ProfileCacheEntry
            {
                Tick = now,
                MapId = mapId,
                Profile = profile
            };
            return profile;
        }

        internal static bool TryObserveHostile(Pawn observer, Pawn hostile,
            out CAVisualPerception result)
        {
            result = default(CAVisualPerception);
            if (hostile == null || hostile == observer || !hostile.Spawned
                || hostile.Destroyed || hostile.Dead || hostile.Downed
                || hostile.IsPrisoner || observer == null
                || hostile.Map != observer.Map || !hostile.HostileTo(observer)
                || !AnimalThreatRelevance.IsRelevantContact(observer, hostile)
                || HiddenRegistry.IsHidden(hostile)) return false;
            return TryObserveVisibleThing(observer, hostile, hostile,
                float.PositiveInfinity, out result);
        }

        internal static bool TryObserveHostile(Pawn observer, Pawn hostile,
            float maximumRange, out CAVisualPerception result)
        {
            result = default(CAVisualPerception);
            if (hostile == null || hostile == observer || !hostile.Spawned
                || hostile.Destroyed || hostile.Dead || hostile.Downed
                || hostile.IsPrisoner || observer == null
                || hostile.Map != observer.Map || !hostile.HostileTo(observer)
                || !AnimalThreatRelevance.IsRelevantContact(observer, hostile)
                || HiddenRegistry.IsHidden(hostile)) return false;
            return TryObserveVisibleThing(observer, hostile, hostile,
                maximumRange, out result);
        }

        internal static bool TryObserveVisibleThing(Pawn observer,
            Thing visible, Pawn representedPawn, float maximumRange,
            out CAVisualPerception result)
        {
            result = default(CAVisualPerception);
            CAVisionProfile profile = VisionProfileFor(observer);
            if (!profile.CanUseVision || visible == null || !visible.Spawned
                || visible.Map != observer.Map || representedPawn == null
                || (HiddenRegistry.IsHidden(representedPawn)
                    && representedPawn.HostileTo(observer))) return false;

            Map map = observer.Map;
            IntVec3 targetCell = visible.Position;
            if (!targetCell.IsValid || !targetCell.InBounds(map)) return false;
            CacheKey key = new CacheKey(map.uniqueID, observer.thingIDNumber,
                representedPawn.thingIDNumber, maximumRange);
            CacheEntry prior;
            int now = Find.TickManager.TicksGame;
            if (cache.TryGetValue(key, out prior)
                && now - prior.Tick >= 0 && now - prior.Tick <= PairCacheTicks
                && prior.ObserverCell == observer.Position
                && prior.SubjectCell == targetCell)
            {
                result = prior.Result;
                return result.Detected;
            }

            float distance = observer.Position.DistanceTo(targetCell);
            float mapDiagonal = Mathf.Sqrt(map.Size.x * map.Size.x
                + map.Size.z * map.Size.z);
            float clearRange = ClearRangeFor(map);
            if (!float.IsPositiveInfinity(maximumRange))
                clearRange = Mathf.Min(clearRange, Mathf.Max(0f, maximumRange));

            float skill = Mathf.Clamp01((profile.Shooting * 0.65f
                + profile.Intellectual * 0.35f) / 20f);
            float glow = 1f;
            try { glow = map.glowGrid.GroundGlowAt(targetCell); }
            catch { }
            float unaidedLight = Mathf.Lerp(0.52f, 1f,
                Mathf.Clamp01(glow));
            float lightFactor = Mathf.Lerp(unaidedLight, 1f,
                profile.DarkAdaptation);
            float sightRangeFactor = Mathf.Lerp(0.45f, 1.18f,
                Mathf.InverseLerp(0.10f, 1.50f,
                    Mathf.Clamp(profile.SightCapacity, 0.10f, 1.50f)));
            float skillRangeFactor = Mathf.Lerp(0.94f, 1.10f, skill);
            float weatherFactor = WeatherRangeFactor(map, clearRange);
            float effectiveRange = clearRange * lightFactor
                * sightRangeFactor * profile.DistanceAcuity
                * skillRangeFactor * weatherFactor;

            bool hardLine = distance <= effectiveRange
                && GenSight.LineOfSightToThing(observer.Position, visible,
                    map, true);
            OcclusionSample occlusion = hardLine
                ? SampleSoftOcclusion(observer.Position, targetCell, map)
                : new OcclusionSample();
            float distanceClarity = effectiveRange <= 0.1f ? 0f
                : Mathf.Clamp01(1f - distance / effectiveRange);
            float bodyFactor = Mathf.Clamp(representedPawn.BodySize,
                0.65f, 1.35f);
            float movementFactor = representedPawn.pather != null
                && representedPawn.pather.Moving ? 1.06f : 1f;
            // RETUNE: occlusion weighs half again harder - foliage and
            // cover cost real certainty (the b5-b7 walk-past receipts).
            float confidence = hardLine
                ? (0.28f + 0.72f * Mathf.Sqrt(distanceClarity))
                    * Mathf.Exp(-occlusion.Penalty * 1.5f) * bodyFactor
                    * movementFactor
                : 0f;
            confidence = Mathf.Clamp01(confidence);
            bool detected = hardLine && confidence >= DetectionThreshold;
            // RETUNE: the identification band - beyond 60% of effective
            // range, movement may be DETECTED but never IDENTIFIED. Five
            // raiders naming Maria at 112 cells was the defect; distant
            // shapes are shapes.
            bool identified = detected
                && confidence >= IdentificationThreshold
                && distance <= 0.60f * effectiveRange;
            float uncertainty = detected
                ? Mathf.Clamp((1f - confidence) * 8f
                    + occlusion.Penalty * 2.5f, 0f, 14f)
                : 0f;
            string limiting = LimitingFactor(sightRangeFactor,
                profile.DistanceAcuity, lightFactor, weatherFactor,
                skillRangeFactor, Mathf.Exp(-occlusion.Penalty));

            result = new CAVisualPerception(detected, identified, distance,
                mapDiagonal, clearRange, effectiveRange, confidence,
                uncertainty, occlusion.Penalty, occlusion.PlantCells,
                occlusion.TreeCells, occlusion.PartialCoverCells,
                occlusion.SmokeCells, glow, profile, sightRangeFactor,
                lightFactor, weatherFactor, skillRangeFactor, limiting);
            cache[key] = new CacheEntry
            {
                Tick = now,
                ObserverCell = observer.Position,
                SubjectCell = targetCell,
                Result = result
            };
            if (cache.Count > 4096 || profileCache.Count > 512) Prune(now);
            return detected;
        }

        private static CAVisionProfile BuildVisionProfile(Pawn pawn)
        {
            bool usable = CanUseVision(pawn);
            float sight = 0f;
            var impactors = new List<PawnCapacityUtility.CapacityImpactor>();
            if (pawn.health?.hediffSet != null)
            {
                try
                {
                    sight = PawnCapacityUtility.CalculateCapacityLevel(
                        pawn.health.hediffSet, PawnCapacityDefOf.Sight,
                        impactors);
                }
                catch
                {
                    sight = pawn.health.capacities?.GetLevel(
                        PawnCapacityDefOf.Sight) ?? 0f;
                }
            }

            float medium = ResolvedStat(pawn,
                StatDefOf.ShootingAccuracyFactor_Medium, 1f);
            float longRange = ResolvedStat(pawn,
                StatDefOf.ShootingAccuracyFactor_Long, 1f);
            float geometric = Mathf.Sqrt(Mathf.Max(0.01f, medium)
                * Mathf.Max(0.01f, longRange));
            // Accuracy and identification are related but not identical. A fourth-root
            // moderation lets nearsightedness and modded distance-acuity penalties
            // matter without turning a 25% long-shot stat into effective blindness.
            float distanceAcuity = Mathf.Clamp(Mathf.Pow(geometric, 0.25f),
                0.55f, 1.25f);

            float darkAdaptation = 0f;
            try
            {
                if (pawn.genes != null && !pawn.genes.AffectedByDarkness)
                    darkAdaptation = 1f;
            }
            catch { }
            darkAdaptation = Mathf.Max(darkAdaptation, ResolvedStat(pawn,
                CARegionalDefOf.CA_DarkVisionEfficiency, 0f));
            darkAdaptation = Mathf.Clamp01(darkAdaptation);

            int shooting = SkillLevel(pawn, SkillDefOf.Shooting);
            int intellectual = SkillLevel(pawn, SkillDefOf.Intellectual);
            string sources = SourceSummary(pawn, impactors,
                StatDefOf.ShootingAccuracyFactor_Medium,
                StatDefOf.ShootingAccuracyFactor_Long,
                CARegionalDefOf.CA_DarkVisionEfficiency);
            return new CAVisionProfile(usable, sight, medium, longRange,
                distanceAcuity, darkAdaptation, shooting, intellectual,
                sources);
        }

        private static float ResolvedStat(Pawn pawn, StatDef stat,
            float fallback)
        {
            if (pawn == null || stat == null) return fallback;
            try { return pawn.GetStatValue(stat); }
            catch { return fallback; }
        }

        private static string SourceSummary(Pawn pawn,
            List<PawnCapacityUtility.CapacityImpactor> impactors,
            params StatDef[] relevantStats)
        {
            var sources = new List<string>();
            for (int i = 0; i < impactors.Count; i++)
            {
                try
                {
                    string readable = impactors[i].Readable(pawn);
                    if (!readable.NullOrEmpty()) sources.Add(readable);
                }
                catch { }
            }

            HashSet<StatDef> relevant = new HashSet<StatDef>(
                relevantStats.Where(stat => stat != null));
            if (pawn.story?.traits?.allTraits != null)
            {
                List<Trait> traits = pawn.story.traits.allTraits;
                for (int i = 0; i < traits.Count; i++)
                {
                    Trait trait = traits[i];
                    if (trait == null || trait.Suppressed) continue;
                    if (Touches(trait.CurrentData.statOffsets, relevant)
                        || Touches(trait.CurrentData.statFactors, relevant))
                        sources.Add("trait " + trait.Label);
                }
            }
            if (pawn.genes?.GenesListForReading != null)
            {
                List<Gene> genes = pawn.genes.GenesListForReading;
                for (int i = 0; i < genes.Count; i++)
                {
                    Gene gene = genes[i];
                    if (gene == null || !gene.Active) continue;
                    if (gene.def.ignoreDarkness
                        || Touches(gene.def.statOffsets, relevant)
                        || Touches(gene.def.statFactors, relevant))
                        sources.Add("gene " + gene.Label);
                }
            }
            if (pawn.apparel?.WornApparel != null)
            {
                List<Apparel> worn = pawn.apparel.WornApparel;
                for (int i = 0; i < worn.Count; i++)
                {
                    Apparel apparel = worn[i];
                    if (Touches(apparel.def.equippedStatOffsets, relevant))
                        sources.Add("apparel " + apparel.LabelShort);
                }
            }
            List<string> distinct = sources.Where(s => !s.NullOrEmpty())
                .Distinct().ToList();
            int omitted = Math.Max(0, distinct.Count - 8);
            string summary = distinct.Take(8).ToCommaList();
            if (summary.NullOrEmpty()) summary = "native resolved values";
            if (omitted > 0) summary += " (and " + omitted + " more)";
            return summary;
        }

        private static bool Touches(List<StatModifier> modifiers,
            HashSet<StatDef> relevant)
        {
            if (modifiers == null) return false;
            for (int i = 0; i < modifiers.Count; i++)
                if (relevant.Contains(modifiers[i].stat)) return true;
            return false;
        }

        private static bool CanUseVision(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && pawn.Map != null
                && !pawn.Dead && !pawn.Downed && pawn.Awake()
                && pawn.RaceProps != null && pawn.RaceProps.Humanlike
                && !PawnUtility.IsBiologicallyOrArtificiallyBlind(pawn);
        }

        private static int SkillLevel(Pawn pawn, SkillDef skill)
        {
            if (pawn?.skills == null || skill == null) return 0;
            SkillRecord record = pawn.skills.GetSkill(skill);
            return record == null || record.TotallyDisabled ? 0 : record.Level;
        }

        private static float WeatherRangeFactor(Map map, float clearRange)
        {
            if (map?.weatherManager == null) return 1f;
            float cap = map.weatherManager.CurWeatherMaxRangeCap;
            if (cap <= 0f || cap >= 999f) return 1f;
            // Native weather caps reliable weapon range. Visual notice is broader, so
            // it attenuates at three times that cap instead of silently becoming the
            // equipped weapon's engagement range.
            float visualCap = Mathf.Clamp(cap * 3f, 55f, clearRange);
            return Mathf.Clamp01(visualCap / Mathf.Max(1f, clearRange));
        }

        private static OcclusionSample SampleSoftOcclusion(IntVec3 start,
            IntVec3 end, Map map)
        {
            var sample = new OcclusionSample();
            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(start, end))
            {
                if (cell == start || cell == end || !cell.InBounds(map))
                    continue;
                bool plantCounted = false;
                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    Plant plant = thing as Plant;
                    if (plant != null)
                    {
                        if (!plantCounted)
                        {
                            sample.PlantCells++;
                            plantCounted = true;
                        }
                        float growth = Mathf.Clamp01(plant.Growth);
                        bool tree = plant.def.plant?.IsTree == true;
                        if (tree)
                        {
                            sample.TreeCells++;
                            sample.Penalty += Mathf.Lerp(0.025f, 0.075f,
                                growth);
                        }
                        else
                            sample.Penalty += Mathf.Lerp(0.004f, 0.022f,
                                growth) * Mathf.Clamp01(
                                    0.35f + plant.def.fillPercent);
                        continue;
                    }
                    // [apertures] Sight THROUGH a wall opening is graded
                    // by the opening's own transparency, not by its
                    // cover fill: a firing slit hides what is behind it
                    // far harder than its cover value suggests, glazing
                    // blurs a little, an open hole costs nothing. The
                    // hard states (closed shutter, bare wall) never
                    // reach this sampler - engine line-of-sight already
                    // stops at full fillage.
                    var aperture = thing as Building_CAAperture;
                    if (aperture != null)
                    {
                        sample.PartialCoverCells++;
                        sample.Penalty +=
                            (1f - aperture.AwarenessTransparency) * 0.30f;
                        continue;
                    }
                    float fill = thing.def != null
                        ? thing.def.fillPercent : 0f;
                    if (fill >= 0.10f && fill < 1f
                        && thing.def.category != ThingCategory.Pawn)
                    {
                        sample.PartialCoverCells++;
                        sample.Penalty += Mathf.Lerp(0.008f, 0.045f,
                            Mathf.Clamp01(fill));
                    }
                }
                if (map.gasGrid != null)
                {
                    float smoke = map.gasGrid.DensityPercentAt(cell,
                        GasType.BlindSmoke);
                    if (smoke > 0.02f)
                    {
                        sample.SmokeCells++;
                        sample.Penalty += smoke * 0.35f;
                    }
                }
            }
            return sample;
        }

        private static string LimitingFactor(float sight, float acuity,
            float light, float weather, float skill, float occlusion)
        {
            string name = "none";
            float lowest = 1f;
            Compare("Sight", sight, ref name, ref lowest);
            Compare("distance acuity", acuity, ref name, ref lowest);
            Compare("light", light, ref name, ref lowest);
            Compare("weather", weather, ref name, ref lowest);
            Compare("observer skill", skill, ref name, ref lowest);
            Compare("flora/cover/smoke", occlusion, ref name, ref lowest);
            return name + " " + lowest.ToString("F2");
        }

        private static void Compare(string candidate, float factor,
            ref string name, ref float lowest)
        {
            if (factor < lowest)
            {
                lowest = factor;
                name = candidate;
            }
        }

        private static void Prune(int now)
        {
            var stale = new List<CacheKey>();
            foreach (KeyValuePair<CacheKey, CacheEntry> pair in cache)
                if (now - pair.Value.Tick > 60) stale.Add(pair.Key);
            for (int i = 0; i < stale.Count; i++) cache.Remove(stale[i]);

            var staleProfiles = new List<int>();
            foreach (KeyValuePair<int, ProfileCacheEntry> pair in profileCache)
                if (now - pair.Value.Tick > 120) staleProfiles.Add(pair.Key);
            for (int i = 0; i < staleProfiles.Count; i++)
                profileCache.Remove(staleProfiles[i]);
        }
    }
}
