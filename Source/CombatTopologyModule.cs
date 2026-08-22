using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Persisted, read-only map evidence for combat replay. Raw game definitions and
    // engine state are retained; this layer does not label a ruin, choke point,
    // defended side, beachhead, or other tactical interpretation.
    internal sealed class CACombatTopologyThingState : IExposable
    {
        internal string thingId;
        internal int thingIdNumber = -1;
        internal string defName;
        internal string stuffDefName;
        internal string label;
        internal string runtimeClass;
        internal string category;
        internal string thingCategories;
        internal int x;
        internal int z;
        internal int rotation;
        internal int minX;
        internal int minZ;
        internal int maxX;
        internal int maxZ;
        internal int hitPoints = -1;
        internal int maxHitPoints = -1;
        internal string factionId;
        internal string factionName;
        internal string passability;
        internal int pathCost;
        internal string fillCategory;
        internal float fillPercent;
        internal float baseBlockChance;
        internal bool edifice;
        internal bool wall;
        internal bool naturalRock;
        internal bool fence;
        internal bool door;
        internal bool doorOpen;
        internal bool doorHoldOpen;
        internal bool turret;
        internal bool chunk;
        internal bool plant;
        internal bool fire;
        internal float fireSize = -1f;
        internal bool affectsRegions;
        internal bool affectsReachability;
        internal bool blocksSight;

        public void ExposeData()
        {
            Scribe_Values.Look(ref thingId, "thingId");
            Scribe_Values.Look(ref thingIdNumber, "thingIdNumber", -1);
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref stuffDefName, "stuffDefName");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref runtimeClass, "runtimeClass");
            Scribe_Values.Look(ref category, "category");
            Scribe_Values.Look(ref thingCategories, "thingCategories");
            Scribe_Values.Look(ref x, "x", 0);
            Scribe_Values.Look(ref z, "z", 0);
            Scribe_Values.Look(ref rotation, "rotation", 0);
            Scribe_Values.Look(ref minX, "minX", 0);
            Scribe_Values.Look(ref minZ, "minZ", 0);
            Scribe_Values.Look(ref maxX, "maxX", 0);
            Scribe_Values.Look(ref maxZ, "maxZ", 0);
            Scribe_Values.Look(ref hitPoints, "hitPoints", -1);
            Scribe_Values.Look(ref maxHitPoints, "maxHitPoints", -1);
            Scribe_Values.Look(ref factionId, "factionId");
            Scribe_Values.Look(ref factionName, "factionName");
            Scribe_Values.Look(ref passability, "passability");
            Scribe_Values.Look(ref pathCost, "pathCost", 0);
            Scribe_Values.Look(ref fillCategory, "fillCategory");
            Scribe_Values.Look(ref fillPercent, "fillPercent", 0f);
            Scribe_Values.Look(ref baseBlockChance, "baseBlockChance", 0f);
            Scribe_Values.Look(ref edifice, "edifice", false);
            Scribe_Values.Look(ref wall, "wall", false);
            Scribe_Values.Look(ref naturalRock, "naturalRock", false);
            Scribe_Values.Look(ref fence, "fence", false);
            Scribe_Values.Look(ref door, "door", false);
            Scribe_Values.Look(ref doorOpen, "doorOpen", false);
            Scribe_Values.Look(ref doorHoldOpen, "doorHoldOpen", false);
            Scribe_Values.Look(ref turret, "turret", false);
            Scribe_Values.Look(ref chunk, "chunk", false);
            Scribe_Values.Look(ref plant, "plant", false);
            Scribe_Values.Look(ref fire, "fire", false);
            Scribe_Values.Look(ref fireSize, "fireSize", -1f);
            Scribe_Values.Look(ref affectsRegions, "affectsRegions", false);
            Scribe_Values.Look(ref affectsReachability,
                "affectsReachability", false);
            Scribe_Values.Look(ref blocksSight, "blocksSight", false);
        }

        internal int EstimateBytes()
        {
            return 224 + StringBytes(thingId) + StringBytes(defName)
                + StringBytes(stuffDefName)
                + StringBytes(label) + StringBytes(runtimeClass)
                + StringBytes(category) + StringBytes(thingCategories)
                + StringBytes(factionId) + StringBytes(factionName)
                + StringBytes(passability) + StringBytes(fillCategory);
        }

        private static int StringBytes(string value)
        {
            return value == null ? 0 : value.Length * 2;
        }
    }

    internal sealed class CACombatTopologyCellState : IExposable
    {
        internal int index = -1;
        internal int x;
        internal int z;
        internal string effectiveTerrain;
        internal string topTerrain;
        internal string underTerrain;
        internal string roof;
        internal int pathCost;
        internal bool walkable;
        internal bool canBeSeenOver;
        internal bool polluted;
        internal bool water;
        internal bool shoreline;
        internal bool naturalRoof;
        internal bool thickRoof;
        internal uint packedGas;
        internal int edificeThingId = -1;
        internal int coverThingId = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref index, "index", -1);
            Scribe_Values.Look(ref x, "x", 0);
            Scribe_Values.Look(ref z, "z", 0);
            Scribe_Values.Look(ref effectiveTerrain, "effectiveTerrain");
            Scribe_Values.Look(ref topTerrain, "topTerrain");
            Scribe_Values.Look(ref underTerrain, "underTerrain");
            Scribe_Values.Look(ref roof, "roof");
            Scribe_Values.Look(ref pathCost, "pathCost", 0);
            Scribe_Values.Look(ref walkable, "walkable", false);
            Scribe_Values.Look(ref canBeSeenOver, "canBeSeenOver", false);
            Scribe_Values.Look(ref polluted, "polluted", false);
            Scribe_Values.Look(ref water, "water", false);
            Scribe_Values.Look(ref shoreline, "shoreline", false);
            Scribe_Values.Look(ref naturalRoof, "naturalRoof", false);
            Scribe_Values.Look(ref thickRoof, "thickRoof", false);
            Scribe_Values.Look(ref packedGas, "packedGas", 0u);
            Scribe_Values.Look(ref edificeThingId, "edificeThingId", -1);
            Scribe_Values.Look(ref coverThingId, "coverThingId", -1);
        }

        internal int EstimateBytes()
        {
            return 96 + StringBytes(effectiveTerrain) + StringBytes(topTerrain)
                + StringBytes(underTerrain) + StringBytes(roof);
        }

        private static int StringBytes(string value)
        {
            return value == null ? 0 : value.Length * 2;
        }
    }

    internal sealed class CACombatTopologyDelta : IExposable
    {
        internal int tick = -1;
        internal int sequence;
        internal string kind;
        internal string detail;
        internal int mapId = -1;
        internal List<CACombatTopologyCellState> cells =
            new List<CACombatTopologyCellState>();
        internal CACombatTopologyThingState thing;
        internal List<CACombatTopologyThingState> things =
            new List<CACombatTopologyThingState>();
        internal int observationRadius;
        internal int observerCenterCount;
        internal int newlyKnownCellCount;
        internal bool weaponSensorBurst;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick", -1);
            Scribe_Values.Look(ref sequence, "sequence", 0);
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref detail, "detail");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Collections.Look(ref cells, "cells", LookMode.Deep);
            Scribe_Deep.Look(ref thing, "thing");
            Scribe_Collections.Look(ref things, "things", LookMode.Deep);
            Scribe_Values.Look(ref observationRadius,
                "observationRadius", 0);
            Scribe_Values.Look(ref observerCenterCount,
                "observerCenterCount", 0);
            Scribe_Values.Look(ref newlyKnownCellCount,
                "newlyKnownCellCount", 0);
            Scribe_Values.Look(ref weaponSensorBurst,
                "weaponSensorBurst", false);
            if (cells == null)
                cells = new List<CACombatTopologyCellState>();
            if (things == null)
                things = new List<CACombatTopologyThingState>();
        }

        internal int EstimateBytes()
        {
            int total = 64 + (kind == null ? 0 : kind.Length * 2)
                + (detail == null ? 0 : detail.Length * 2)
                + (thing == null ? 0 : thing.EstimateBytes());
            for (int i = 0; i < cells.Count; i++)
                total += cells[i]?.EstimateBytes() ?? 0;
            for (int i = 0; i < things.Count; i++)
                total += things[i]?.EstimateBytes() ?? 0;
            return total;
        }
    }

    internal sealed class CACombatTopologyHazardCell : IExposable
    {
        internal int index = -1;
        internal int x;
        internal int z;
        internal uint packedGas;
        internal bool polluted;
        internal string fires;

        public void ExposeData()
        {
            Scribe_Values.Look(ref index, "index", -1);
            Scribe_Values.Look(ref x, "x", 0);
            Scribe_Values.Look(ref z, "z", 0);
            Scribe_Values.Look(ref packedGas, "packedGas", 0u);
            Scribe_Values.Look(ref polluted, "polluted", false);
            Scribe_Values.Look(ref fires, "fires");
        }

        internal int EstimateBytes()
        {
            return 32 + (fires == null ? 0 : fires.Length * 2);
        }
    }

    internal sealed class CACombatTopologyHazardSample : IExposable
    {
        internal int logId;
        internal int tick = -1;
        internal int pawnObservationRadius;
        internal int weaponSensorBurstRadius;
        internal bool includesLineOfSightCorridors;
        internal bool weaponSensorBurst;
        internal int candidateCellCount;
        internal bool candidateCoverageTruncated;
        internal List<int> centerCellIndices = new List<int>();
        internal List<CACombatTopologyHazardCell> nonzeroCells =
            new List<CACombatTopologyHazardCell>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref logId, "logId", 0);
            Scribe_Values.Look(ref tick, "tick", -1);
            Scribe_Values.Look(ref pawnObservationRadius,
                "pawnObservationRadius", 0);
            Scribe_Values.Look(ref weaponSensorBurstRadius,
                "weaponSensorBurstRadius", 0);
            Scribe_Values.Look(ref includesLineOfSightCorridors,
                "includesLineOfSightCorridors", false);
            Scribe_Values.Look(ref weaponSensorBurst,
                "weaponSensorBurst", false);
            Scribe_Values.Look(ref candidateCellCount,
                "candidateCellCount", 0);
            Scribe_Values.Look(ref candidateCoverageTruncated,
                "candidateCoverageTruncated", false);
            Scribe_Collections.Look(ref centerCellIndices,
                "centerCellIndices", LookMode.Value);
            Scribe_Collections.Look(ref nonzeroCells, "nonzeroCells",
                LookMode.Deep);
            if (centerCellIndices == null)
                centerCellIndices = new List<int>();
            if (nonzeroCells == null)
                nonzeroCells = new List<CACombatTopologyHazardCell>();
        }

        internal int EstimateBytes()
        {
            int total = 64 + centerCellIndices.Count * 4;
            for (int i = 0; i < nonzeroCells.Count; i++)
                total += nonzeroCells[i]?.EstimateBytes() ?? 0;
            return total;
        }
    }

    internal sealed class CACombatTopologyReferenceArtifactState : IExposable
    {
        internal string thingId;
        internal int thingIdNumber = -1;
        internal string defName;
        internal string stuffDefName;
        internal string runtimeClass;
        internal string category;
        internal string thingCategories;
        internal int x;
        internal int z;
        internal int rotation;
        internal int minX;
        internal int minZ;
        internal int maxX;
        internal int maxZ;
        internal string passability;
        internal string fillCategory;
        internal float fillPercent;
        internal float baseBlockChance;
        internal bool building;
        internal bool wall;
        internal bool naturalRock;
        internal bool fence;
        internal bool door;
        internal bool turret;
        internal bool chunk;
        internal bool affectsRegions;
        internal bool affectsReachability;

        public void ExposeData()
        {
            Scribe_Values.Look(ref thingId, "thingId");
            Scribe_Values.Look(ref thingIdNumber, "thingIdNumber", -1);
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref stuffDefName, "stuffDefName");
            Scribe_Values.Look(ref runtimeClass, "runtimeClass");
            Scribe_Values.Look(ref category, "category");
            Scribe_Values.Look(ref thingCategories, "thingCategories");
            Scribe_Values.Look(ref x, "x", 0);
            Scribe_Values.Look(ref z, "z", 0);
            Scribe_Values.Look(ref rotation, "rotation", 0);
            Scribe_Values.Look(ref minX, "minX", 0);
            Scribe_Values.Look(ref minZ, "minZ", 0);
            Scribe_Values.Look(ref maxX, "maxX", 0);
            Scribe_Values.Look(ref maxZ, "maxZ", 0);
            Scribe_Values.Look(ref passability, "passability");
            Scribe_Values.Look(ref fillCategory, "fillCategory");
            Scribe_Values.Look(ref fillPercent, "fillPercent", 0f);
            Scribe_Values.Look(ref baseBlockChance,
                "baseBlockChance", 0f);
            Scribe_Values.Look(ref building, "building", false);
            Scribe_Values.Look(ref wall, "wall", false);
            Scribe_Values.Look(ref naturalRock, "naturalRock", false);
            Scribe_Values.Look(ref fence, "fence", false);
            Scribe_Values.Look(ref door, "door", false);
            Scribe_Values.Look(ref turret, "turret", false);
            Scribe_Values.Look(ref chunk, "chunk", false);
            Scribe_Values.Look(ref affectsRegions,
                "affectsRegions", false);
            Scribe_Values.Look(ref affectsReachability,
                "affectsReachability", false);
        }

        internal int EstimateBytes()
        {
            return 192 + StringBytes(thingId) + StringBytes(defName)
                + StringBytes(stuffDefName) + StringBytes(runtimeClass)
                + StringBytes(category) + StringBytes(thingCategories)
                + StringBytes(passability) + StringBytes(fillCategory);
        }

        private static int StringBytes(string value)
        {
            return value == null ? 0 : value.Length * 2;
        }
    }

    internal sealed class CACombatTopologyReferenceCellState : IExposable
    {
        internal int index = -1;
        internal int x;
        internal int z;
        internal string effectiveTerrain;
        internal string topTerrain;
        internal string underTerrain;
        internal string roof;
        internal bool water;
        internal bool shoreline;
        internal bool naturalRoof;
        internal bool thickRoof;

        public void ExposeData()
        {
            Scribe_Values.Look(ref index, "index", -1);
            Scribe_Values.Look(ref x, "x", 0);
            Scribe_Values.Look(ref z, "z", 0);
            Scribe_Values.Look(ref effectiveTerrain, "effectiveTerrain");
            Scribe_Values.Look(ref topTerrain, "topTerrain");
            Scribe_Values.Look(ref underTerrain, "underTerrain");
            Scribe_Values.Look(ref roof, "roof");
            Scribe_Values.Look(ref water, "water", false);
            Scribe_Values.Look(ref shoreline, "shoreline", false);
            Scribe_Values.Look(ref naturalRoof, "naturalRoof", false);
            Scribe_Values.Look(ref thickRoof, "thickRoof", false);
        }

        internal int EstimateBytes()
        {
            return 64 + StringBytes(effectiveTerrain)
                + StringBytes(topTerrain) + StringBytes(underTerrain)
                + StringBytes(roof);
        }

        private static int StringBytes(string value)
        {
            return value == null ? 0 : value.Length * 2;
        }
    }

    internal sealed class CACombatTopologyReferenceChange : IExposable
    {
        internal int tick = -1;
        internal int sequence;
        internal string kind;
        internal string detail;
        internal int mapId = -1;
        internal CACombatTopologyReferenceCellState cell;
        internal CACombatTopologyReferenceArtifactState artifact;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick", -1);
            Scribe_Values.Look(ref sequence, "sequence", 0);
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref detail, "detail");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Deep.Look(ref cell, "cell");
            Scribe_Deep.Look(ref artifact, "artifact");
        }

        internal int EstimateBytes()
        {
            return 48 + StringBytes(kind) + StringBytes(detail)
                + (cell?.EstimateBytes() ?? 0)
                + (artifact?.EstimateBytes() ?? 0);
        }

        private static int StringBytes(string value)
        {
            return value == null ? 0 : value.Length * 2;
        }
    }

    internal sealed class CACombatTopologyBattlefieldReference : IExposable
    {
        internal int capturedTick = -1;
        internal string captureTiming;
        internal string cellFormat;
        internal byte[] cellPayload;
        internal string cellPayloadSha256;
        internal int cellCount;
        internal List<string> terrainPalette = new List<string>();
        internal List<string> roofPalette = new List<string>();
        internal List<CACombatTopologyReferenceArtifactState> artifacts =
            new List<CACombatTopologyReferenceArtifactState>();
        internal List<CACombatTopologyReferenceChange> changes =
            new List<CACombatTopologyReferenceChange>();
        internal int baselineEstimatedBytes;
        internal int nextSequence;
        internal bool changesTruncated;
        internal int changesTruncatedAtTick = -1;
        internal string changesTruncationReason;
        // Artifact census accounting: how many battlefield-reference
        // artifacts existed on the map and how many fell outside the
        // bounded battlefield window or the hard cap. The census itself
        // stays bounded so its cost never scales with total map area.
        internal int artifactCensusTotal;
        internal int artifactsBeyondWindow;
        private int cachedChangeEstimatedBytes = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref capturedTick, "capturedTick", -1);
            Scribe_Values.Look(ref captureTiming, "captureTiming");
            Scribe_Values.Look(ref cellFormat, "cellFormat");
            DataExposeUtility.LookByteArray(ref cellPayload, "cellPayload");
            Scribe_Values.Look(ref cellPayloadSha256,
                "cellPayloadSha256");
            Scribe_Values.Look(ref cellCount, "cellCount", 0);
            Scribe_Collections.Look(ref terrainPalette, "terrainPalette",
                LookMode.Value);
            Scribe_Collections.Look(ref roofPalette, "roofPalette",
                LookMode.Value);
            Scribe_Collections.Look(ref artifacts, "artifacts",
                LookMode.Deep);
            Scribe_Collections.Look(ref changes, "changes", LookMode.Deep);
            Scribe_Values.Look(ref baselineEstimatedBytes,
                "baselineEstimatedBytes", 0);
            Scribe_Values.Look(ref nextSequence, "nextSequence", 0);
            Scribe_Values.Look(ref artifactCensusTotal,
                "artifactCensusTotal", 0);
            Scribe_Values.Look(ref artifactsBeyondWindow,
                "artifactsBeyondWindow", 0);
            Scribe_Values.Look(ref changesTruncated,
                "changesTruncated", false);
            Scribe_Values.Look(ref changesTruncatedAtTick,
                "changesTruncatedAtTick", -1);
            Scribe_Values.Look(ref changesTruncationReason,
                "changesTruncationReason");
            if (terrainPalette == null) terrainPalette = new List<string>();
            if (roofPalette == null) roofPalette = new List<string>();
            if (artifacts == null)
                artifacts = new List<CACombatTopologyReferenceArtifactState>();
            if (changes == null)
                changes = new List<CACombatTopologyReferenceChange>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                cachedChangeEstimatedBytes = -1;
        }

        internal int EstimateBytes()
        {
            int total = 192 + (cellPayload?.Length ?? 0)
                + StringBytes(captureTiming) + StringBytes(cellFormat)
                + StringBytes(cellPayloadSha256)
                + StringBytes(changesTruncationReason);
            for (int i = 0; i < terrainPalette.Count; i++)
                total += StringBytes(terrainPalette[i]);
            for (int i = 0; i < roofPalette.Count; i++)
                total += StringBytes(roofPalette[i]);
            for (int i = 0; i < artifacts.Count; i++)
                total += artifacts[i]?.EstimateBytes() ?? 0;
            for (int i = 0; i < changes.Count; i++)
                total += changes[i]?.EstimateBytes() ?? 0;
            return total;
        }

        internal int ChangeEstimatedBytes()
        {
            if (cachedChangeEstimatedBytes >= 0)
                return cachedChangeEstimatedBytes;
            int total = StringBytes(changesTruncationReason);
            for (int i = 0; i < changes.Count; i++)
                total += changes[i]?.EstimateBytes() ?? 0;
            cachedChangeEstimatedBytes = Math.Max(0, total);
            return cachedChangeEstimatedBytes;
        }

        internal void IncreaseChangeEstimatedBytes(int value)
        {
            if (cachedChangeEstimatedBytes >= 0)
                cachedChangeEstimatedBytes += Math.Max(0, value);
        }

        internal void InvalidateChangeEstimate()
        {
            cachedChangeEstimatedBytes = -1;
        }

        private static int StringBytes(string value)
        {
            return value == null ? 0 : value.Length * 2;
        }
    }

    internal sealed class CACombatTopologyIncident : IExposable
    {
        internal string incidentId;
        internal int mapId = -1;
        internal int width;
        internal int height;
        internal int firstLogId;
        internal int firstLogTick = -1;
        internal int lastLogTick = -1;
        internal int baselineCapturedTick = -1;
        internal string baselineTiming;
        internal string cellFormat;
        internal byte[] cellPayload;
        internal string cellPayloadSha256;
        internal int baselineEstimatedBytes;
        internal bool baselineExceededByteTarget;
        internal int observationRadius;
        internal int knownCellCount;
        internal List<string> terrainPalette = new List<string>();
        internal List<string> roofPalette = new List<string>();
        internal List<CACombatTopologyThingState> baselineThings =
            new List<CACombatTopologyThingState>();
        internal List<int> knownCellIndices = new List<int>();
        internal List<string> observerPawnIds = new List<string>();
        internal List<string> observedThingIds = new List<string>();
        internal List<int> logIds = new List<int>();
        internal int omittedLogIdCount;
        internal int firstOmittedLogId;
        internal int lastOmittedLogId;
        internal List<string> nativeBattleAliases = new List<string>();
        internal List<CACombatTopologyDelta> deltas =
            new List<CACombatTopologyDelta>();
        internal List<CACombatTopologyHazardSample> hazardSamples =
            new List<CACombatTopologyHazardSample>();
        internal CACombatTopologyBattlefieldReference battlefieldReference;
        internal int nextSequence;
        internal bool truncated;
        internal int truncatedAtTick = -1;
        internal string truncationReason;
        internal int observationFailureCount;
        internal int lastObservationFailureTick = -1;
        internal string lastObservationFailure;
        private int cachedEstimatedBytes = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref incidentId, "incidentId");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref width, "width", 0);
            Scribe_Values.Look(ref height, "height", 0);
            Scribe_Values.Look(ref firstLogId, "firstLogId", 0);
            Scribe_Values.Look(ref firstLogTick, "firstLogTick", -1);
            Scribe_Values.Look(ref lastLogTick, "lastLogTick", -1);
            Scribe_Values.Look(ref baselineCapturedTick,
                "baselineCapturedTick", -1);
            Scribe_Values.Look(ref baselineTiming, "baselineTiming");
            Scribe_Values.Look(ref cellFormat, "cellFormat");
            DataExposeUtility.LookByteArray(ref cellPayload, "cellPayload");
            Scribe_Values.Look(ref cellPayloadSha256,
                "cellPayloadSha256");
            Scribe_Values.Look(ref baselineEstimatedBytes,
                "baselineEstimatedBytes", 0);
            Scribe_Values.Look(ref baselineExceededByteTarget,
                "baselineExceededByteTarget", false);
            Scribe_Values.Look(ref observationRadius,
                "observationRadius", 0);
            Scribe_Values.Look(ref knownCellCount, "knownCellCount", 0);
            Scribe_Collections.Look(ref terrainPalette, "terrainPalette",
                LookMode.Value);
            Scribe_Collections.Look(ref roofPalette, "roofPalette",
                LookMode.Value);
            Scribe_Collections.Look(ref baselineThings, "baselineThings",
                LookMode.Deep);
            Scribe_Collections.Look(ref knownCellIndices,
                "knownCellIndices", LookMode.Value);
            Scribe_Collections.Look(ref observerPawnIds,
                "observerPawnIds", LookMode.Value);
            Scribe_Collections.Look(ref observedThingIds,
                "observedThingIds", LookMode.Value);
            Scribe_Collections.Look(ref logIds, "logIds", LookMode.Value);
            Scribe_Values.Look(ref omittedLogIdCount,
                "omittedLogIdCount", 0);
            Scribe_Values.Look(ref firstOmittedLogId,
                "firstOmittedLogId", 0);
            Scribe_Values.Look(ref lastOmittedLogId,
                "lastOmittedLogId", 0);
            Scribe_Collections.Look(ref nativeBattleAliases,
                "nativeBattleAliases", LookMode.Value);
            Scribe_Collections.Look(ref deltas, "deltas", LookMode.Deep);
            Scribe_Collections.Look(ref hazardSamples, "hazardSamples",
                LookMode.Deep);
            Scribe_Deep.Look(ref battlefieldReference,
                "battlefieldReference");
            Scribe_Values.Look(ref nextSequence, "nextSequence", 0);
            Scribe_Values.Look(ref truncated, "truncated", false);
            Scribe_Values.Look(ref truncatedAtTick, "truncatedAtTick", -1);
            Scribe_Values.Look(ref truncationReason, "truncationReason");
            Scribe_Values.Look(ref observationFailureCount,
                "observationFailureCount", 0);
            Scribe_Values.Look(ref lastObservationFailureTick,
                "lastObservationFailureTick", -1);
            Scribe_Values.Look(ref lastObservationFailure,
                "lastObservationFailure");
            if (terrainPalette == null) terrainPalette = new List<string>();
            if (roofPalette == null) roofPalette = new List<string>();
            if (baselineThings == null)
                baselineThings = new List<CACombatTopologyThingState>();
            if (knownCellIndices == null)
                knownCellIndices = new List<int>();
            if (observerPawnIds == null)
                observerPawnIds = new List<string>();
            if (observedThingIds == null)
                observedThingIds = new List<string>();
            if (logIds == null) logIds = new List<int>();
            if (nativeBattleAliases == null)
                nativeBattleAliases = new List<string>();
            if (deltas == null) deltas = new List<CACombatTopologyDelta>();
            if (hazardSamples == null)
                hazardSamples = new List<CACombatTopologyHazardSample>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                cachedEstimatedBytes = -1;
        }

        internal bool ContainsLogId(int logId)
        {
            return logIds.BinarySearch(logId) >= 0;
        }

        internal void AddLogId(int logId, bool retain = true)
        {
            if (!retain || logIds.Count >= 8192)
            {
                if (omittedLogIdCount < int.MaxValue) omittedLogIdCount++;
                if (firstOmittedLogId == 0) firstOmittedLogId = logId;
                lastOmittedLogId = logId;
                return;
            }
            int index = logIds.BinarySearch(logId);
            if (index < 0)
            {
                logIds.Insert(~index, logId);
                IncreaseEstimatedBytes(4);
            }
        }

        internal void AddBattleAlias(string alias)
        {
            if (alias.NullOrEmpty()) return;
            int index = nativeBattleAliases.BinarySearch(alias,
                StringComparer.Ordinal);
            if (index < 0)
            {
                nativeBattleAliases.Insert(~index, alias);
                IncreaseEstimatedBytes(alias.Length * 2);
            }
        }

        internal int EstimateBytes()
        {
            if (cachedEstimatedBytes >= 0) return cachedEstimatedBytes;
            int total = 384 + (cellPayload?.Length ?? 0)
                + StringBytes(incidentId) + StringBytes(baselineTiming)
                + StringBytes(cellFormat) + StringBytes(cellPayloadSha256)
                + StringBytes(truncationReason)
                + StringBytes(lastObservationFailure) + logIds.Count * 4
                + knownCellIndices.Count * 4;
            for (int i = 0; i < terrainPalette.Count; i++)
                total += StringBytes(terrainPalette[i]);
            for (int i = 0; i < roofPalette.Count; i++)
                total += StringBytes(roofPalette[i]);
            for (int i = 0; i < nativeBattleAliases.Count; i++)
                total += StringBytes(nativeBattleAliases[i]);
            for (int i = 0; i < observerPawnIds.Count; i++)
                total += StringBytes(observerPawnIds[i]);
            for (int i = 0; i < observedThingIds.Count; i++)
                total += StringBytes(observedThingIds[i]);
            for (int i = 0; i < baselineThings.Count; i++)
                total += baselineThings[i]?.EstimateBytes() ?? 0;
            for (int i = 0; i < deltas.Count; i++)
                total += deltas[i]?.EstimateBytes() ?? 0;
            for (int i = 0; i < hazardSamples.Count; i++)
                total += hazardSamples[i]?.EstimateBytes() ?? 0;
            total += battlefieldReference?.EstimateBytes() ?? 0;
            cachedEstimatedBytes = total;
            return total;
        }

        internal void SetEstimatedBytes(int value)
        {
            cachedEstimatedBytes = Math.Max(0, value);
        }

        internal void InvalidateEstimate()
        {
            cachedEstimatedBytes = -1;
        }

        internal int DynamicEstimatedBytes()
        {
            int referenceChangeBytes = battlefieldReference
                ?.ChangeEstimatedBytes() ?? 0;
            return Math.Max(0, EstimateBytes() - baselineEstimatedBytes
                - referenceChangeBytes);
        }

        private void IncreaseEstimatedBytes(int value)
        {
            if (cachedEstimatedBytes >= 0)
                cachedEstimatedBytes += Math.Max(0, value);
        }

        private static int StringBytes(string value)
        {
            return value == null ? 0 : value.Length * 2;
        }
    }

    internal struct CACombatTopologyCellSignature : IEquatable<CACombatTopologyCellSignature>
    {
        internal ushort effective;
        internal ushort top;
        internal ushort under;
        internal ushort roof;
        internal int pathCost;
        internal byte flags;
        internal uint gas;
        internal int edifice;
        internal int cover;

        public bool Equals(CACombatTopologyCellSignature other)
        {
            return effective == other.effective && top == other.top
                && under == other.under && roof == other.roof
                && pathCost == other.pathCost && flags == other.flags
                && gas == other.gas && edifice == other.edifice
                && cover == other.cover;
        }
    }

    internal sealed class CACombatTopologyCellRun
    {
        internal int start;
        internal int count;
        internal CACombatTopologyCellSignature signature;
    }

    internal struct CACombatTopologyReferenceCellSignature :
        IEquatable<CACombatTopologyReferenceCellSignature>
    {
        internal ushort effective;
        internal ushort top;
        internal ushort under;
        internal ushort roof;
        internal byte flags;

        public bool Equals(CACombatTopologyReferenceCellSignature other)
        {
            return effective == other.effective && top == other.top
                && under == other.under && roof == other.roof
                && flags == other.flags;
        }
    }

    internal struct CACombatTopologyReferenceFingerprint :
        IEquatable<CACombatTopologyReferenceFingerprint>
    {
        internal ushort effective;
        internal ushort top;
        internal ushort under;
        internal ushort roof;
        internal byte flags;

        public bool Equals(CACombatTopologyReferenceFingerprint other)
        {
            return effective == other.effective && top == other.top
                && under == other.under && roof == other.roof
                && flags == other.flags;
        }
    }

    internal sealed class CACombatTopologyReferenceCellRun
    {
        internal int start;
        internal int count;
        internal CACombatTopologyReferenceCellSignature signature;
    }

    internal static class CACombatTopologyCapture
    {
        internal const string CellFormat =
            "CA-topology-cells-v2:rle-row-major;flags=walkable,seen-over,polluted,water,natural-roof,thick-roof,shoreline,known";
        internal const string BattlefieldReferenceCellFormat =
            "CA-battlefield-reference-cells-v1:rle-row-major;flags=water,shoreline,natural-roof,thick-roof";
        internal const int ObservationRadius = 8;
        internal const int WeaponSensorBurstRadius = 6;
        private const int MaxHazardCandidateCells = 4096;

        internal static CACombatTopologyIncident CaptureBaseline(Map map,
            LogEntry entry, CACombatSpatialLogRecord record, int firstLogId,
            int firstLogTick, string battleAlias)
        {
            if (map == null || entry == null || record == null) return null;
            var incident = new CACombatTopologyIncident
            {
                incidentId = "CAT-" + map.uniqueID.ToString(
                    CultureInfo.InvariantCulture) + "-" + firstLogId.ToString(
                    CultureInfo.InvariantCulture),
                mapId = map.uniqueID,
                width = map.Size.x,
                height = map.Size.z,
                firstLogId = firstLogId,
                firstLogTick = firstLogTick,
                lastLogTick = firstLogTick,
                baselineCapturedTick = CurrentTicksAbs(),
                baselineTiming = "after-first-qualifying-BattleLog.Add",
                cellFormat = CellFormat,
                observationRadius = ObservationRadius
            };
            incident.AddLogId(firstLogId);
            incident.AddBattleAlias(battleAlias);
            List<IntVec3> pawnCenters = PawnConcernCenters(entry, record,
                map, incident.observerPawnIds);
            incident.battlefieldReference = CaptureBattlefieldReference(map,
                incident.baselineCapturedTick, pawnCenters);
            SortedSet<int> known = ProximalRevealCellIndices(map,
                pawnCenters, record.hasBulletImpact
                    && record.impactMapId == map.uniqueID
                    ? record.impactCell : IntVec3.Invalid);
            incident.knownCellIndices.AddRange(known);
            incident.knownCellCount = incident.knownCellIndices.Count;

            int cells = map.cellIndices.NumGridCells;
            var terrainNames = new HashSet<string>(StringComparer.Ordinal)
            {
                ""
            };
            var roofNames = new HashSet<string>(StringComparer.Ordinal)
            {
                ""
            };
            foreach (int i in known)
            {
                TerrainDef effective = map.terrainGrid.TerrainAt(i);
                TerrainDef top = map.terrainGrid.TopTerrainAt(i);
                TerrainDef under = map.terrainGrid.UnderTerrainAt(i);
                RoofDef roof = map.roofGrid.RoofAt(i);
                terrainNames.Add(DefName(effective));
                terrainNames.Add(DefName(top));
                terrainNames.Add(DefName(under));
                roofNames.Add(DefName(roof));
            }
            incident.terrainPalette.AddRange(terrainNames);
            incident.roofPalette.AddRange(roofNames);
            incident.terrainPalette.Sort(StringComparer.Ordinal);
            incident.roofPalette.Sort(StringComparer.Ordinal);
            var terrainIndex = PaletteIndex(incident.terrainPalette);
            var roofIndex = PaletteIndex(incident.roofPalette);

            var signatures = new CACombatTopologyCellSignature[cells];
            foreach (int i in known)
            {
                IntVec3 cell = map.cellIndices.IndexToCell(i);
                TerrainDef effective = map.terrainGrid.TerrainAt(i);
                TerrainDef top = map.terrainGrid.TopTerrainAt(i);
                TerrainDef under = map.terrainGrid.UnderTerrainAt(i);
                RoofDef roof = map.roofGrid.RoofAt(i);
                Building edifice = map.edificeGrid[i];
                Thing cover = map.coverGrid[i];
                byte flags = 0;
                if (map.pathing.Normal.pathGrid.WalkableFast(i)) flags |= 1;
                if (cell.CanBeSeenOver(map)) flags |= 2;
                if (map.pollutionGrid.IsPolluted(cell)) flags |= 4;
                if (effective != null && effective.IsWater) flags |= 8;
                if (roof != null && roof.isNatural) flags |= 16;
                if (roof != null && roof.isThickRoof) flags |= 32;
                if (IsShoreline(cell, map, effective)) flags |= 64;
                flags |= 128;
                signatures[i] = new CACombatTopologyCellSignature
                {
                    effective = PaletteValue(terrainIndex, DefName(effective)),
                    top = PaletteValue(terrainIndex, DefName(top)),
                    under = PaletteValue(terrainIndex, DefName(under)),
                    roof = PaletteValue(roofIndex, DefName(roof)),
                    pathCost = map.pathing.Normal.pathGrid.Cost(cell),
                    flags = flags,
                    gas = map.gasGrid.GetDirect(i),
                    edifice = edifice?.thingIDNumber ?? -1,
                    cover = cover?.thingIDNumber ?? -1
                };
            }
            incident.cellPayload = Encode(signatures);
            incident.cellPayloadSha256 = Sha256(incident.cellPayload);

            List<CACombatTopologyThingState> artifacts =
                CaptureArtifactsInKnownCells(map, known);
            for (int i = 0; i < artifacts.Count; i++)
            {
                CACombatTopologyThingState artifact = artifacts[i];
                incident.baselineThings.Add(artifact);
                AddSortedUnique(incident.observedThingIds, artifact.thingId);
            }
            incident.baselineThings.Sort(CompareThings);
            return incident;
        }

        internal static CACombatTopologyBattlefieldReference
            CaptureBattlefieldReference(Map map, int capturedTick,
                List<IntVec3> battleCenters = null)
        {
            if (map == null) return null;
            var reference = new CACombatTopologyBattlefieldReference
            {
                capturedTick = capturedTick,
                captureTiming = "after-first-qualifying-BattleLog.Add",
                cellFormat = BattlefieldReferenceCellFormat,
                cellCount = map.cellIndices.NumGridCells
            };
            var terrainNames = new HashSet<string>(StringComparer.Ordinal)
            {
                ""
            };
            var roofNames = new HashSet<string>(StringComparer.Ordinal)
            {
                ""
            };
            for (int i = 0; i < reference.cellCount; i++)
            {
                terrainNames.Add(DefName(map.terrainGrid.TerrainAt(i)));
                terrainNames.Add(DefName(map.terrainGrid.TopTerrainAt(i)));
                terrainNames.Add(DefName(map.terrainGrid.UnderTerrainAt(i)));
                roofNames.Add(DefName(map.roofGrid.RoofAt(i)));
            }
            reference.terrainPalette.AddRange(terrainNames);
            reference.roofPalette.AddRange(roofNames);
            reference.terrainPalette.Sort(StringComparer.Ordinal);
            reference.roofPalette.Sort(StringComparer.Ordinal);
            Dictionary<string, ushort> terrainIndex = PaletteIndex(
                reference.terrainPalette);
            Dictionary<string, ushort> roofIndex = PaletteIndex(
                reference.roofPalette);
            var signatures =
                new CACombatTopologyReferenceCellSignature[reference.cellCount];
            for (int i = 0; i < signatures.Length; i++)
            {
                IntVec3 cell = map.cellIndices.IndexToCell(i);
                TerrainDef effective = map.terrainGrid.TerrainAt(i);
                TerrainDef top = map.terrainGrid.TopTerrainAt(i);
                TerrainDef under = map.terrainGrid.UnderTerrainAt(i);
                RoofDef roof = map.roofGrid.RoofAt(i);
                byte flags = 0;
                if (effective != null && effective.IsWater) flags |= 1;
                if (IsShoreline(cell, map, effective)) flags |= 2;
                if (roof != null && roof.isNatural) flags |= 4;
                if (roof != null && roof.isThickRoof) flags |= 8;
                signatures[i] = new CACombatTopologyReferenceCellSignature
                {
                    effective = PaletteValue(terrainIndex,
                        DefName(effective)),
                    top = PaletteValue(terrainIndex, DefName(top)),
                    under = PaletteValue(terrainIndex, DefName(under)),
                    roof = PaletteValue(roofIndex, DefName(roof)),
                    flags = flags
                };
            }
            reference.cellPayload = EncodeReference(signatures);
            reference.cellPayloadSha256 = Sha256(reference.cellPayload);
            CaptureReferenceArtifacts(map, battleCenters, reference);
            reference.baselineEstimatedBytes = reference.EstimateBytes();
            return reference;
        }

        internal static CACombatTopologyReferenceCellState
            CaptureReferenceCell(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map)) return null;
            int index = map.cellIndices.CellToIndex(cell);
            TerrainDef effective = map.terrainGrid.TerrainAt(index);
            RoofDef roof = map.roofGrid.RoofAt(index);
            return new CACombatTopologyReferenceCellState
            {
                index = index,
                x = cell.x,
                z = cell.z,
                effectiveTerrain = DefName(effective),
                topTerrain = DefName(map.terrainGrid.TopTerrainAt(index)),
                underTerrain = DefName(map.terrainGrid.UnderTerrainAt(index)),
                roof = DefName(roof),
                water = effective != null && effective.IsWater,
                shoreline = IsShoreline(cell, map, effective),
                naturalRoof = roof != null && roof.isNatural,
                thickRoof = roof != null && roof.isThickRoof
            };
        }

        internal static CACombatTopologyReferenceFingerprint
            CaptureReferenceFingerprint(Map map, int index)
        {
            IntVec3 cell = map.cellIndices.IndexToCell(index);
            TerrainDef effective = map.terrainGrid.TerrainAt(index);
            RoofDef roof = map.roofGrid.RoofAt(index);
            byte flags = 0;
            if (effective != null && effective.IsWater) flags |= 1;
            if (IsShoreline(cell, map, effective)) flags |= 2;
            if (roof != null && roof.isNatural) flags |= 4;
            if (roof != null && roof.isThickRoof) flags |= 8;
            return new CACombatTopologyReferenceFingerprint
            {
                effective = effective?.shortHash ?? 0,
                top = map.terrainGrid.TopTerrainAt(index)?.shortHash ?? 0,
                under = map.terrainGrid.UnderTerrainAt(index)?.shortHash ?? 0,
                roof = roof?.shortHash ?? 0,
                flags = flags
            };
        }

        internal static bool IsBattlefieldReferenceArtifact(Thing thing)
        {
            if (thing == null || thing.def == null || thing is Pawn
                || thing is Fire || thing is Filth) return false;
            return thing is Building || IsChunk(thing)
                || thing.def.BaseBlockChance() > 0f;
        }

        internal static CACombatTopologyReferenceArtifactState
            CaptureReferenceArtifact(Thing thing)
        {
            if (!IsBattlefieldReferenceArtifact(thing)) return null;
            CellRect rect = thing.OccupiedRect();
            var categories = new List<string>();
            if (thing.def.thingCategories != null)
                for (int i = 0; i < thing.def.thingCategories.Count; i++)
                {
                    ThingCategoryDef category = thing.def.thingCategories[i];
                    if (category != null) categories.Add(category.defName);
                }
            categories.Sort(StringComparer.Ordinal);
            float baseBlockChance = thing.def.BaseBlockChance();
            return new CACombatTopologyReferenceArtifactState
            {
                thingId = thing.ThingID,
                thingIdNumber = thing.thingIDNumber,
                defName = thing.def.defName,
                stuffDefName = thing.Stuff?.defName,
                runtimeClass = thing.GetType().FullName,
                category = thing.def.category.ToString(),
                thingCategories = string.Join("|", categories.ToArray()),
                x = thing.Position.x,
                z = thing.Position.z,
                rotation = thing.Rotation.AsInt,
                minX = rect.minX,
                minZ = rect.minZ,
                maxX = rect.maxX,
                maxZ = rect.maxZ,
                passability = thing.def.passability.ToString(),
                fillCategory = thing.def.Fillage.ToString(),
                fillPercent = thing.def.fillPercent,
                baseBlockChance = baseBlockChance,
                building = thing is Building,
                wall = thing.def.IsWall,
                naturalRock = thing.def.building != null
                    && thing.def.building.isNaturalRock,
                fence = thing.def.IsFence,
                door = thing is Building_Door,
                turret = thing is Building_Turret,
                chunk = IsChunk(thing),
                affectsRegions = thing.def.AffectsRegions,
                affectsReachability = thing.def.AffectsReachability
            };
        }

        // A battlefield reference censuses the BATTLEFIELD, not the map:
        // on regional maps a whole-map artifact census serializes past the
        // campaign preflight's element limit (measured at 400x6: the
        // artifact list alone crossed 1,000,000 elements), and its cost
        // would scale with total map area. Artifacts are captured within a
        // bounded window around the battle's concern centers, nearest
        // first under a hard cap, and the census totals record exactly
        // what stayed outside.
        private const int ArtifactWindowRadius = 96;
        private const int MaxReferenceArtifacts = 4096;

        private static void CaptureReferenceArtifacts(Map map,
            List<IntVec3> battleCenters,
            CACombatTopologyBattlefieldReference reference)
        {
            if (map == null || map.listerThings == null
                || reference == null) return;
            bool windowed = battleCenters != null && battleCenters.Count > 0;
            int windowSquared = ArtifactWindowRadius * ArtifactWindowRadius;
            var kept = new List<CACombatTopologyReferenceArtifactState>();
            var keptDistances = new List<int>();
            int total = 0;
            int beyond = 0;
            List<Thing> things = map.listerThings.AllThings;
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing == null || !thing.Spawned || thing.Map != map
                    || !IsBattlefieldReferenceArtifact(thing)) continue;
                total++;
                int nearest = 0;
                if (windowed)
                {
                    nearest = int.MaxValue;
                    for (int c = 0; c < battleCenters.Count; c++)
                    {
                        int distance = thing.Position.DistanceToSquared(
                            battleCenters[c]);
                        if (distance < nearest) nearest = distance;
                    }
                    if (nearest > windowSquared) { beyond++; continue; }
                }
                kept.Add(CaptureReferenceArtifact(thing));
                keptDistances.Add(nearest);
            }
            if (kept.Count > MaxReferenceArtifacts)
            {
                int[] order = new int[kept.Count];
                for (int i = 0; i < order.Length; i++) order[i] = i;
                int[] distances = keptDistances.ToArray();
                Array.Sort(distances, order);
                var nearestKept =
                    new List<CACombatTopologyReferenceArtifactState>(
                        MaxReferenceArtifacts);
                for (int i = 0; i < MaxReferenceArtifacts; i++)
                    nearestKept.Add(kept[order[i]]);
                beyond += kept.Count - MaxReferenceArtifacts;
                kept = nearestKept;
            }
            kept.Sort(CompareReferenceArtifacts);
            reference.artifacts.AddRange(kept);
            reference.artifactCensusTotal = total;
            reference.artifactsBeyondWindow = beyond;
        }

        private static bool IsChunk(Thing thing)
        {
            return thing?.def?.thingCategories != null
                && thing.def.thingCategories.Contains(ThingCategoryDefOf.Chunks);
        }

        internal static CACombatTopologyCellState CaptureCell(Map map,
            IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map)) return null;
            int index = map.cellIndices.CellToIndex(cell);
            TerrainDef effective = map.terrainGrid.TerrainAt(index);
            TerrainDef top = map.terrainGrid.TopTerrainAt(index);
            TerrainDef under = map.terrainGrid.UnderTerrainAt(index);
            RoofDef roof = map.roofGrid.RoofAt(index);
            Building edifice = map.edificeGrid[index];
            Thing cover = map.coverGrid[index];
            return new CACombatTopologyCellState
            {
                index = index,
                x = cell.x,
                z = cell.z,
                effectiveTerrain = DefName(effective),
                topTerrain = DefName(top),
                underTerrain = DefName(under),
                roof = DefName(roof),
                pathCost = map.pathing.Normal.pathGrid.Cost(cell),
                walkable = map.pathing.Normal.pathGrid.WalkableFast(index),
                canBeSeenOver = cell.CanBeSeenOver(map),
                polluted = map.pollutionGrid.IsPolluted(cell),
                water = effective != null && effective.IsWater,
                shoreline = IsShoreline(cell, map, effective),
                naturalRoof = roof != null && roof.isNatural,
                thickRoof = roof != null && roof.isThickRoof,
                packedGas = map.gasGrid.GetDirect(index),
                edificeThingId = edifice?.thingIDNumber ?? -1,
                coverThingId = cover?.thingIDNumber ?? -1
            };
        }

        internal static CACombatTopologyThingState CaptureThing(Thing thing)
        {
            if (thing == null || thing.def == null) return null;
            CellRect rect = thing.OccupiedRect();
            Building_Door door = thing as Building_Door;
            Fire fire = thing as Fire;
            List<string> categories = new List<string>();
            if (thing.def.thingCategories != null)
            {
                for (int i = 0; i < thing.def.thingCategories.Count; i++)
                {
                    ThingCategoryDef category = thing.def.thingCategories[i];
                    if (category != null) categories.Add(category.defName);
                }
                categories.Sort(StringComparer.Ordinal);
            }
            Faction faction = thing.Faction;
            bool useHitPoints = thing.def.useHitPoints;
            return new CACombatTopologyThingState
            {
                thingId = thing.ThingID,
                thingIdNumber = thing.thingIDNumber,
                defName = thing.def.defName,
                stuffDefName = thing.Stuff?.defName,
                label = thing.Label,
                runtimeClass = thing.GetType().FullName,
                category = thing.def.category.ToString(),
                thingCategories = string.Join("|", categories.ToArray()),
                x = thing.Position.x,
                z = thing.Position.z,
                rotation = thing.Rotation.AsInt,
                minX = rect.minX,
                minZ = rect.minZ,
                maxX = rect.maxX,
                maxZ = rect.maxZ,
                hitPoints = useHitPoints ? thing.HitPoints : -1,
                maxHitPoints = useHitPoints ? thing.MaxHitPoints : -1,
                factionId = faction?.GetUniqueLoadID(),
                factionName = faction?.Name,
                passability = thing.def.passability.ToString(),
                pathCost = thing.def.pathCost,
                fillCategory = thing.def.Fillage.ToString(),
                fillPercent = thing.def.fillPercent,
                baseBlockChance = thing.BaseBlockChance(),
                edifice = thing.def.IsEdifice(),
                wall = thing.def.IsWall,
                naturalRock = thing.def.building != null
                    && thing.def.building.isNaturalRock,
                fence = thing.def.IsFence,
                door = door != null,
                doorOpen = door != null && door.Open,
                doorHoldOpen = door != null && door.HoldOpen,
                turret = thing is Building_Turret,
                chunk = thing.def.thingCategories != null
                    && thing.def.thingCategories.Contains(
                        ThingCategoryDefOf.Chunks),
                plant = thing is Plant,
                fire = fire != null,
                fireSize = fire?.fireSize ?? -1f,
                affectsRegions = thing.def.AffectsRegions,
                affectsReachability = thing.def.AffectsReachability,
                blocksSight = thing is Building building
                    && !building.CanBeSeenOver()
            };
        }

        internal static bool IsTopologyThing(Thing thing)
        {
            if (thing == null || thing.def == null || thing is Pawn)
                return false;
            return thing is Building || thing is Fire
                || thing.def.Fillage != FillCategory.None
                || thing.def.pathCost != 0
                || thing.def.passability != Traversability.Standable;
        }

        internal static List<IntVec3> PawnConcernCenters(LogEntry entry,
            CACombatSpatialLogRecord record, Map map,
            List<string> observerPawnIds)
        {
            var livePawnIds = new HashSet<string>(StringComparer.Ordinal);
            if (entry != null)
            {
                foreach (Thing concern in entry.GetConcerns())
                {
                    Pawn pawn = concern as Pawn;
                    if (pawn == null || !pawn.Spawned || pawn.Map != map)
                        continue;
                    livePawnIds.Add(pawn.ThingID);
                    AddSortedUnique(observerPawnIds, pawn.ThingID);
                }
            }

            var centerIndices = new SortedSet<int>();
            if (record?.concerns != null)
            {
                for (int i = 0; i < record.concerns.Count; i++)
                {
                    CACombatSpatialConcernSnapshot concern =
                        record.concerns[i];
                    if (concern == null || concern.mapId != map.uniqueID
                        || !concern.cell.InBounds(map)
                        || !livePawnIds.Contains(concern.thingId))
                        continue;
                    centerIndices.Add(map.cellIndices.CellToIndex(
                        concern.cell));
                }
            }
            var result = new List<IntVec3>(centerIndices.Count);
            foreach (int index in centerIndices)
                result.Add(map.cellIndices.IndexToCell(index));
            return result;
        }

        internal static SortedSet<int> ProximalRevealCellIndices(Map map,
            List<IntVec3> pawnCenters, IntVec3 weaponImpact)
        {
            var result = new SortedSet<int>();
            if (map == null) return result;
            if (pawnCenters == null) pawnCenters = new List<IntVec3>();

            for (int i = 0; i < pawnCenters.Count; i++)
                AddVisibleBurst(map, pawnCenters[i], ObservationRadius,
                    result);

            for (int i = 0; i < pawnCenters.Count; i++)
                for (int j = i + 1; j < pawnCenters.Count; j++)
                    AddVisibleCorridor(map, pawnCenters[i], pawnCenters[j],
                        result);

            if (weaponImpact.IsValid && weaponImpact.InBounds(map))
            {
                // A copied projectile impact is an event-grounded sensor burst,
                // not a claim that a pawn had omniscient map vision.
                AddVisibleBurst(map, weaponImpact, WeaponSensorBurstRadius,
                    result);
                for (int i = 0; i < pawnCenters.Count; i++)
                    AddVisibleCorridor(map, pawnCenters[i], weaponImpact,
                        result);
            }
            return result;
        }

        internal static CACombatTopologyDelta CaptureKnowledgeReveal(Map map,
            LogEntry entry, CACombatSpatialLogRecord record,
            CACombatTopologyIncident incident)
        {
            if (map == null || entry == null || record == null
                || incident == null) return null;
            List<IntVec3> centers = PawnConcernCenters(entry, record, map,
                incident.observerPawnIds);
            bool weaponBurst = record.hasBulletImpact
                && record.impactMapId == map.uniqueID
                && record.impactCell.InBounds(map);
            return BuildKnowledgeReveal(map, incident, centers,
                weaponBurst ? record.impactCell : IntVec3.Invalid,
                record.eventTick, weaponBurst,
                "pawn-proximal BattleLog evidence; previously unknown cells only");
        }

        internal static CACombatTopologyDelta CaptureCurrentObserverReveal(
            Map map, CACombatTopologyIncident incident, IntVec3 eventCell)
        {
            if (map == null || incident == null || !eventCell.InBounds(map))
                return null;
            var centerIndices = new SortedSet<int>();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            int radius = incident.observationRadius > 0
                ? incident.observationRadius : ObservationRadius;
            int radiusSquared = radius * radius;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || incident.observerPawnIds.BinarySearch(
                    pawn.ThingID, StringComparer.Ordinal) < 0
                    || pawn.Position.DistanceToSquared(eventCell)
                        > radiusSquared
                    || !GenSight.LineOfSight(pawn.Position, eventCell, map,
                        skipFirstCell: true)) continue;
                centerIndices.Add(map.cellIndices.CellToIndex(pawn.Position));
            }
            if (centerIndices.Count == 0) return null;
            var centers = new List<IntVec3>(centerIndices.Count);
            foreach (int index in centerIndices)
                centers.Add(map.cellIndices.IndexToCell(index));
            return BuildKnowledgeReveal(map, incident, centers,
                IntVec3.Invalid, CurrentTicksAbs(), false,
                "current observer proximity at an engine map event; previously unknown cells only");
        }

        private static CACombatTopologyDelta BuildKnowledgeReveal(Map map,
            CACombatTopologyIncident incident, List<IntVec3> centers,
            IntVec3 weaponImpact, int tick, bool weaponBurst, string detail)
        {
            SortedSet<int> candidates = ProximalRevealCellIndices(map,
                centers, weaponImpact);
            var known = new HashSet<int>(incident.knownCellIndices);
            var newlyKnown = new SortedSet<int>();
            foreach (int index in candidates)
                if (!known.Contains(index)) newlyKnown.Add(index);
            if (newlyKnown.Count == 0) return null;

            var totalKnown = new HashSet<int>(known);
            foreach (int index in newlyKnown) totalKnown.Add(index);
            var delta = new CACombatTopologyDelta
            {
                tick = tick,
                kind = "knowledge-reveal",
                detail = detail,
                mapId = map.uniqueID,
                observationRadius = ObservationRadius,
                observerCenterCount = centers.Count,
                newlyKnownCellCount = newlyKnown.Count,
                weaponSensorBurst = weaponBurst
            };
            foreach (int index in newlyKnown)
                delta.cells.Add(CaptureCell(map,
                    map.cellIndices.IndexToCell(index)));

            List<CACombatTopologyThingState> artifacts =
                CaptureArtifactsInKnownCells(map, newlyKnown, totalKnown);
            for (int i = 0; i < artifacts.Count; i++)
            {
                CACombatTopologyThingState artifact = artifacts[i];
                if (incident.observedThingIds.BinarySearch(artifact.thingId,
                    StringComparer.Ordinal) < 0)
                    delta.things.Add(artifact);
            }
            return delta;
        }

        internal static bool IsCurrentlyObserved(
            CACombatTopologyIncident incident, Map map, IntVec3 cell)
        {
            if (incident == null || map == null || !cell.InBounds(map)
                || incident.observerPawnIds == null
                || incident.observerPawnIds.Count == 0)
                return false;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            int radius = incident.observationRadius > 0
                ? incident.observationRadius : ObservationRadius;
            int radiusSquared = radius * radius;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || incident.observerPawnIds.BinarySearch(
                    pawn.ThingID, StringComparer.Ordinal) < 0
                    || pawn.Position.DistanceToSquared(cell) > radiusSquared)
                    continue;
                if (GenSight.LineOfSight(pawn.Position, cell, map,
                    skipFirstCell: true)) return true;
            }
            return false;
        }

        internal static bool AllFootprintCellsCurrentlyObserved(
            CACombatTopologyIncident incident, Map map, Thing thing)
        {
            if (thing == null) return false;
            CellRect footprint = thing.OccupiedRect();
            bool any = false;
            for (int z = footprint.minZ; z <= footprint.maxZ; z++)
                for (int x = footprint.minX; x <= footprint.maxX; x++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (!cell.InBounds(map)
                        || !IsCurrentlyObserved(incident, map, cell))
                        return false;
                    any = true;
                }
            return any;
        }

        private static void AddVisibleBurst(Map map, IntVec3 center,
            int radius, SortedSet<int> result)
        {
            if (!center.InBounds(map)) return;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center,
                radius, useCenter: true))
            {
                if (!cell.InBounds(map)
                    || !GenSight.LineOfSight(center, cell, map,
                        skipFirstCell: true))
                    continue;
                result.Add(map.cellIndices.CellToIndex(cell));
            }
        }

        private static void AddVisibleCorridor(Map map, IntVec3 start,
            IntVec3 end, SortedSet<int> result)
        {
            if (!start.InBounds(map) || !end.InBounds(map)
                || !GenSight.LineOfSight(start, end, map,
                    skipFirstCell: true)) return;
            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(start, end))
                if (cell.InBounds(map))
                    result.Add(map.cellIndices.CellToIndex(cell));
        }

        private static List<CACombatTopologyThingState>
            CaptureArtifactsInKnownCells(Map map, IEnumerable<int> scanIndices,
                HashSet<int> completeKnown = null)
        {
            var result = new List<CACombatTopologyThingState>();
            if (map == null || scanIndices == null) return result;
            var scan = new SortedSet<int>(scanIndices);
            var known = completeKnown ?? new HashSet<int>(scan);
            var seen = new HashSet<int>();
            foreach (int index in scan)
            {
                List<Thing> things = map.thingGrid.ThingsListAtFast(index);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (!IsTopologyThing(thing)
                        || !seen.Add(thing.thingIDNumber)
                        || !FootprintKnown(thing, map, known)) continue;
                    result.Add(CaptureThing(thing));
                }
            }
            result.Sort(CompareThings);
            return result;
        }

        private static bool FootprintKnown(Thing thing, Map map,
            HashSet<int> known)
        {
            CellRect footprint = thing.OccupiedRect();
            for (int z = footprint.minZ; z <= footprint.maxZ; z++)
                for (int x = footprint.minX; x <= footprint.maxX; x++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (!cell.InBounds(map) || !known.Contains(
                        map.cellIndices.CellToIndex(cell))) return false;
                }
            return true;
        }

        internal static void AddSortedUnique(List<string> values,
            string value)
        {
            if (values == null || value.NullOrEmpty()) return;
            int index = values.BinarySearch(value, StringComparer.Ordinal);
            if (index < 0) values.Insert(~index, value);
        }

        internal static CACombatTopologyHazardSample CaptureHazards(Map map,
            LogEntry entry, CACombatSpatialLogRecord record,
            CACombatTopologyIncident incident)
        {
            if (map == null || entry == null || record == null
                || incident == null) return null;
            List<IntVec3> pawnCenters = PawnConcernCenters(entry, record, map,
                incident.observerPawnIds);
            IntVec3 weaponImpact = record.hasBulletImpact
                && record.impactMapId == map.uniqueID
                && record.impactCell.InBounds(map)
                ? record.impactCell : IntVec3.Invalid;
            var centers = new SortedSet<int>();
            for (int i = 0; i < pawnCenters.Count; i++)
                centers.Add(map.cellIndices.CellToIndex(pawnCenters[i]));
            if (weaponImpact.IsValid)
                centers.Add(map.cellIndices.CellToIndex(weaponImpact));
            if (centers.Count == 0) return null;

            SortedSet<int> currentReveal = ProximalRevealCellIndices(map,
                pawnCenters, weaponImpact);
            var candidates = new SortedSet<int>();
            bool clipped = false;
            foreach (int index in currentReveal)
            {
                if (incident.knownCellIndices.BinarySearch(index) < 0)
                    continue;
                if (candidates.Count >= MaxHazardCandidateCells)
                {
                    clipped = true;
                    continue;
                }
                candidates.Add(index);
            }

            var sample = new CACombatTopologyHazardSample
            {
                logId = record.logId,
                tick = record.eventTick,
                pawnObservationRadius = ObservationRadius,
                weaponSensorBurstRadius = WeaponSensorBurstRadius,
                includesLineOfSightCorridors = true,
                weaponSensorBurst = weaponImpact.IsValid,
                candidateCellCount = candidates.Count,
                candidateCoverageTruncated = clipped
            };
            sample.centerCellIndices.AddRange(centers);
            foreach (int index in candidates)
            {
                IntVec3 cell = map.cellIndices.IndexToCell(index);
                uint gas = map.gasGrid.GetDirect(index);
                bool polluted = map.pollutionGrid.IsPolluted(cell);
                string fires = FiresAt(map, cell);
                if (gas == 0 && !polluted && fires.NullOrEmpty()) continue;
                sample.nonzeroCells.Add(new CACombatTopologyHazardCell
                {
                    index = index,
                    x = cell.x,
                    z = cell.z,
                    packedGas = gas,
                    polluted = polluted,
                    fires = fires
                });
            }
            return sample;
        }

        internal static byte[] Encode(
            CACombatTopologyCellSignature[] signatures)
        {
            if (signatures == null) signatures =
                new CACombatTopologyCellSignature[0];
            var runs = new List<CACombatTopologyCellRun>();
            int start = 0;
            while (start < signatures.Length)
            {
                int end = start + 1;
                while (end < signatures.Length
                    && signatures[start].Equals(signatures[end])) end++;
                runs.Add(new CACombatTopologyCellRun
                {
                    start = start,
                    count = end - start,
                    signature = signatures[start]
                });
                start = end;
            }
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(1);
                writer.Write(signatures.Length);
                writer.Write(runs.Count);
                for (int i = 0; i < runs.Count; i++)
                {
                    CACombatTopologyCellRun run = runs[i];
                    writer.Write(run.start);
                    writer.Write(run.count);
                    WriteSignature(writer, run.signature);
                }
                return stream.ToArray();
            }
        }

        internal static List<CACombatTopologyCellRun> Decode(byte[] payload,
            out int cellCount)
        {
            var result = new List<CACombatTopologyCellRun>();
            cellCount = 0;
            if (payload == null || payload.Length == 0) return result;
            using (var stream = new MemoryStream(payload, writable: false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                int version = reader.ReadInt32();
                if (version != 1)
                    throw new InvalidDataException(
                        "Unsupported combat topology cell version " + version);
                cellCount = reader.ReadInt32();
                int count = reader.ReadInt32();
                int expectedStart = 0;
                for (int i = 0; i < count; i++)
                {
                    var run = new CACombatTopologyCellRun
                    {
                        start = reader.ReadInt32(),
                        count = reader.ReadInt32(),
                        signature = ReadSignature(reader)
                    };
                    if (run.start != expectedStart || run.count <= 0)
                        throw new InvalidDataException(
                            "Non-contiguous combat topology RLE");
                    expectedStart += run.count;
                    result.Add(run);
                }
                if (expectedStart != cellCount || stream.Position != stream.Length)
                    throw new InvalidDataException(
                        "Combat topology RLE length mismatch");
            }
            return result;
        }

        internal static byte[] EncodeReference(
            CACombatTopologyReferenceCellSignature[] signatures)
        {
            if (signatures == null) signatures =
                new CACombatTopologyReferenceCellSignature[0];
            var runs = new List<CACombatTopologyReferenceCellRun>();
            int start = 0;
            while (start < signatures.Length)
            {
                int end = start + 1;
                while (end < signatures.Length
                    && signatures[start].Equals(signatures[end])) end++;
                runs.Add(new CACombatTopologyReferenceCellRun
                {
                    start = start,
                    count = end - start,
                    signature = signatures[start]
                });
                start = end;
            }
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(1);
                writer.Write(signatures.Length);
                writer.Write(runs.Count);
                for (int i = 0; i < runs.Count; i++)
                {
                    CACombatTopologyReferenceCellRun run = runs[i];
                    writer.Write(run.start);
                    writer.Write(run.count);
                    WriteReferenceSignature(writer, run.signature);
                }
                return stream.ToArray();
            }
        }

        internal static List<CACombatTopologyReferenceCellRun>
            DecodeReference(byte[] payload, out int cellCount)
        {
            var result = new List<CACombatTopologyReferenceCellRun>();
            cellCount = 0;
            if (payload == null || payload.Length == 0) return result;
            using (var stream = new MemoryStream(payload, writable: false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                int version = reader.ReadInt32();
                if (version != 1)
                    throw new InvalidDataException(
                        "Unsupported battlefield reference cell version "
                            + version);
                cellCount = reader.ReadInt32();
                int count = reader.ReadInt32();
                int expectedStart = 0;
                for (int i = 0; i < count; i++)
                {
                    var run = new CACombatTopologyReferenceCellRun
                    {
                        start = reader.ReadInt32(),
                        count = reader.ReadInt32(),
                        signature = ReadReferenceSignature(reader)
                    };
                    if (run.start != expectedStart || run.count <= 0)
                        throw new InvalidDataException(
                            "Non-contiguous battlefield reference RLE");
                    expectedStart += run.count;
                    result.Add(run);
                }
                if (expectedStart != cellCount
                    || stream.Position != stream.Length)
                    throw new InvalidDataException(
                        "Battlefield reference RLE length mismatch");
            }
            return result;
        }

        internal static string RegressionReceipt()
        {
            var a = new CACombatTopologyCellSignature
            {
                effective = 1, top = 1, under = 0, roof = 0,
                pathCost = 13, flags = 203, gas = 0, edifice = -1,
                cover = 42
            };
            var b = new CACombatTopologyCellSignature
            {
                effective = 2, top = 2, under = 1, roof = 1,
                pathCost = 10000, flags = 176, gas = 0x01020304u,
                edifice = 91, cover = 91
            };
            var source = new[] { a, a, b, b, b, a };
            byte[] first = Encode(source);
            byte[] second = Encode(source);
            const string expectedSha256 =
                "D9C2AF7ACF948FB843BAEC81B6F36DACCE7C67E094FAE8BE2DB367331626CA0E";
            string actualSha256 = Sha256(first);
            int decodedCells;
            List<CACombatTopologyCellRun> runs = Decode(first,
                out decodedCells);
            bool encodingStable = ByteEqual(first, second);
            bool canonicalHash = actualSha256 == expectedSha256;
            bool replay = decodedCells == 6 && runs.Count == 3
                && runs[0].start == 0 && runs[0].count == 2
                && runs[0].signature.Equals(a)
                && runs[1].start == 2 && runs[1].count == 3
                && runs[1].signature.Equals(b)
                && runs[2].start == 5 && runs[2].count == 1
                && runs[2].signature.Equals(a);
            var incident = new CACombatTopologyIncident();
            incident.AddLogId(9);
            incident.AddLogId(4);
            incident.AddLogId(9);
            incident.AddBattleAlias("Battle_8");
            incident.AddBattleAlias("Battle_2");
            incident.AddBattleAlias("Battle_8");
            bool ordering = incident.logIds.Count == 2
                && incident.logIds[0] == 4 && incident.logIds[1] == 9
                && incident.nativeBattleAliases.Count == 2
                && incident.nativeBattleAliases[0] == "Battle_2"
                && incident.nativeBattleAliases[1] == "Battle_8";
            object olderEntry = new object();
            object newlyInsertedEntry = new object();
            var nativeBattleEntryOrder = new List<object>();
            nativeBattleEntryOrder.Insert(0, olderEntry);
            nativeBattleEntryOrder.Insert(0, newlyInsertedEntry);
            bool newestEntryAssociation = Patch_CABattleLogSpatialCapture
                .HasNewlyInsertedEntry(nativeBattleEntryOrder,
                    newlyInsertedEntry)
                && !Patch_CABattleLogSpatialCapture.HasNewlyInsertedEntry(
                    nativeBattleEntryOrder, olderEntry)
                && !Patch_CABattleLogSpatialCapture.HasNewlyInsertedEntry(
                    new List<object>(), newlyInsertedEntry);

            const int fixtureWidth = 12;
            const int fixtureHeight = 6;
            var firstReveal = new SortedSet<int>(new[]
                { 13, 14, 25, 26, 37 });
            var secondReveal = new[] { 14, 15, 26, 27, 38 };
            var accumulatedReveal = new SortedSet<int>(firstReveal);
            for (int i = 0; i < secondReveal.Length; i++)
                accumulatedReveal.Add(secondReveal[i]);
            bool revealMonotonic = accumulatedReveal.Count == 8
                && firstReveal.IsSubsetOf(accumulatedReveal);
            int fixtureCells = fixtureWidth * fixtureHeight;
            int fixtureUnknown = fixtureCells - accumulatedReveal.Count;
            bool accounting = accumulatedReveal.Count + fixtureUnknown
                == fixtureCells;
            bool blockerShadowUnknown = !accumulatedReveal.Contains(16);
            bool nearMutationAccepted = accumulatedReveal.Contains(13);
            bool remoteMutationRejected = !accumulatedReveal.Contains(71);
            bool nearArtifactAccepted = FixtureFootprintKnown(1, 1, 2, 1,
                fixtureWidth, fixtureHeight, accumulatedReveal);
            bool distantArtifactRejected = !FixtureFootprintKnown(11, 5,
                11, 5, fixtureWidth, fixtureHeight, accumulatedReveal);
            var knowledgeMask =
                new CACombatTopologyCellSignature[fixtureCells];
            foreach (int index in accumulatedReveal)
                knowledgeMask[index].flags = 128;
            byte[] knowledgeFirst = Encode(knowledgeMask);
            byte[] knowledgeSecond = Encode(knowledgeMask);
            string knowledgeSha256 = Sha256(knowledgeFirst);
            const string expectedKnowledgeSha256 =
                "35AFF9A6B59753B9872B43F897E393528E2D81E87973360F687BB6FC803C721F";
            bool knowledgeMaskStable = ByteEqual(knowledgeFirst,
                knowledgeSecond) && knowledgeSha256
                    == expectedKnowledgeSha256;
            bool proximityContract = revealMonotonic && accounting
                && blockerShadowUnknown && nearMutationAccepted
                && remoteMutationRejected && nearArtifactAccepted
                && distantArtifactRejected && knowledgeMaskStable;
            var referenceA = new CACombatTopologyReferenceCellSignature
            {
                effective = 1, top = 1, under = 0, roof = 0, flags = 0
            };
            var referenceB = new CACombatTopologyReferenceCellSignature
            {
                effective = 2, top = 2, under = 1, roof = 1, flags = 15
            };
            var referenceSource = new[]
                { referenceA, referenceA, referenceB, referenceB,
                    referenceB, referenceA };
            byte[] referenceFirst = EncodeReference(referenceSource);
            byte[] referenceSecond = EncodeReference(referenceSource);
            string referenceSha256 = Sha256(referenceFirst);
            const string expectedReferenceSha256 =
                "3F07C55485E6EDE7227A2F5E00C8083032D3DE0DB0475BD260D224B0143BB504";
            int referenceDecodedCells;
            List<CACombatTopologyReferenceCellRun> referenceRuns =
                DecodeReference(referenceFirst, out referenceDecodedCells);
            bool referenceReplay = ByteEqual(referenceFirst, referenceSecond)
                && referenceSha256 == expectedReferenceSha256
                && referenceDecodedCells == 6 && referenceRuns.Count == 3
                && referenceRuns[0].start == 0
                && referenceRuns[0].count == 2
                && referenceRuns[1].start == 2
                && referenceRuns[1].count == 3
                && referenceRuns[2].start == 5
                && referenceRuns[2].count == 1;
            var remoteWall = new CACombatTopologyReferenceArtifactState
            {
                thingId = "RemoteWall91",
                thingIdNumber = 91,
                defName = "Wall",
                stuffDefName = "BlocksLimestone",
                runtimeClass = "Verse.Building",
                category = "Building",
                x = 2,
                z = 1,
                minX = 2,
                minZ = 1,
                maxX = 2,
                maxZ = 1,
                passability = "Impassable",
                fillCategory = "Full",
                fillPercent = 1f,
                baseBlockChance = 0.75f,
                building = true,
                wall = true,
                affectsRegions = true,
                affectsReachability = true
            };
            var battlefieldReference =
                new CACombatTopologyBattlefieldReference
                {
                    capturedTick = 100,
                    captureTiming =
                        "after-first-qualifying-BattleLog.Add",
                    cellFormat = BattlefieldReferenceCellFormat,
                    cellPayload = referenceFirst,
                    cellPayloadSha256 = Sha256(referenceFirst),
                    cellCount = 6
                };
            battlefieldReference.terrainPalette.AddRange(
                new[] { "", "Soil", "Water" });
            battlefieldReference.roofPalette.AddRange(
                new[] { "", "RoofRockThick" });
            battlefieldReference.artifacts.Add(remoteWall);
            battlefieldReference.baselineEstimatedBytes =
                battlefieldReference.EstimateBytes();
            var partialDynamicSource = new[]
            {
                a, a, new CACombatTopologyCellSignature(),
                new CACombatTopologyCellSignature(),
                new CACombatTopologyCellSignature(),
                new CACombatTopologyCellSignature()
            };
            byte[] partialDynamicPayload = Encode(partialDynamicSource);
            var dualFixture = new CACombatTopologyIncident
            {
                incidentId = "CAT-v3-regression",
                mapId = 7,
                width = 3,
                height = 2,
                firstLogId = 10,
                firstLogTick = 100,
                lastLogTick = 101,
                baselineCapturedTick = 100,
                baselineTiming = "regression",
                cellFormat = CellFormat,
                cellPayload = partialDynamicPayload,
                cellPayloadSha256 = Sha256(partialDynamicPayload),
                observationRadius = ObservationRadius,
                knownCellCount = 2,
                battlefieldReference = battlefieldReference
            };
            dualFixture.knownCellIndices.AddRange(new[] { 0, 1 });
            dualFixture.terrainPalette.AddRange(
                new[] { "", "Soil", "Water" });
            dualFixture.roofPalette.AddRange(
                new[] { "", "RoofRockThick" });
            dualFixture.baselineEstimatedBytes = dualFixture.EstimateBytes();
            battlefieldReference.changes.Add(
                new CACombatTopologyReferenceChange
                {
                    tick = 101,
                    sequence = 0,
                    kind = "thing-removed",
                    detail = "destroy:KillFinalize",
                    mapId = 7,
                    artifact = remoteWall
                });
            battlefieldReference.nextSequence = 1;
            dualFixture.InvalidateEstimate();
            var fixture = new CACombatTopologyIncident
            {
                incidentId = "CAT-regression",
                mapId = 7,
                width = 3,
                height = 2,
                firstLogId = 4,
                firstLogTick = 100,
                lastLogTick = 102,
                baselineCapturedTick = 100,
                baselineTiming = "regression",
                cellFormat = CellFormat,
                cellPayload = first,
                cellPayloadSha256 = actualSha256,
                observationRadius = ObservationRadius,
                knownCellCount = 6
            };
            fixture.knownCellIndices.AddRange(new[] { 0, 1, 2, 3, 4, 5 });
            fixture.observerPawnIds.AddRange(new[]
                { "Human12", "Human42" });
            fixture.terrainPalette.AddRange(new[] { "", "Soil", "Water" });
            fixture.roofPalette.AddRange(new[] { "", "RoofRockThick" });
            fixture.AddLogId(4);
            fixture.AddLogId(9);
            fixture.AddBattleAlias("Battle_2");
            fixture.baselineThings.Add(new CACombatTopologyThingState
            {
                thingId = "Wall91",
                thingIdNumber = 91,
                defName = "Wall",
                stuffDefName = "BlocksLimestone",
                label = "limestone wall",
                runtimeClass = "Verse.Building",
                category = "Building",
                x = 2,
                z = 1,
                minX = 2,
                minZ = 1,
                maxX = 2,
                maxZ = 1,
                hitPoints = 180,
                maxHitPoints = 300,
                passability = "Impassable",
                fillCategory = "Full",
                fillPercent = 1f,
                baseBlockChance = 0.75f,
                edifice = true,
                wall = true,
                affectsRegions = true,
                affectsReachability = true,
                blocksSight = true
            });
            fixture.observedThingIds.Add("Wall91");
            fixture.InvalidateEstimate();
            fixture.baselineEstimatedBytes = fixture.EstimateBytes();
            fixture.SetEstimatedBytes(fixture.baselineEstimatedBytes);
            var revealDelta = new CACombatTopologyDelta
            {
                tick = 101,
                sequence = 0,
                kind = "knowledge-reveal",
                detail = "regression sensor burst",
                mapId = 7,
                observationRadius = ObservationRadius,
                observerCenterCount = 2,
                newlyKnownCellCount = 1,
                weaponSensorBurst = true
            };
            revealDelta.cells.Add(new CACombatTopologyCellState
            {
                index = 5,
                x = 2,
                z = 1,
                effectiveTerrain = "Water",
                pathCost = 10000,
                polluted = true,
                water = true,
                naturalRoof = true,
                thickRoof = true,
                packedGas = 0x01020304u,
                edificeThingId = 91,
                coverThingId = 91
            });
            revealDelta.things.Add(new CACombatTopologyThingState
            {
                thingId = "Chunk92",
                thingIdNumber = 92,
                defName = "ChunkSlagSteel",
                label = "steel slag chunk",
                runtimeClass = "Verse.Thing",
                category = "Item",
                x = 2,
                z = 1,
                minX = 2,
                minZ = 1,
                maxX = 2,
                maxZ = 1,
                passability = "PassThroughOnly",
                fillCategory = "Partial",
                chunk = true
            });
            fixture.deltas.Add(revealDelta);
            fixture.deltas.Add(new CACombatTopologyDelta
            {
                tick = 101,
                sequence = 1,
                kind = "thing-spawned",
                mapId = 7
            });
            fixture.deltas.Add(new CACombatTopologyDelta
            {
                tick = 101,
                sequence = 2,
                kind = "terrain-changed",
                mapId = 7
            });
            fixture.deltas.Add(new CACombatTopologyDelta
            {
                tick = 102,
                sequence = 3,
                kind = "thing-removed",
                detail = "destroy:KillFinalize",
                mapId = 7
            });
            var hazard = new CACombatTopologyHazardSample
            {
                logId = 9,
                tick = 102,
                pawnObservationRadius = ObservationRadius,
                weaponSensorBurstRadius = WeaponSensorBurstRadius,
                includesLineOfSightCorridors = true,
                weaponSensorBurst = true,
                candidateCellCount = 1
            };
            hazard.centerCellIndices.Add(5);
            hazard.nonzeroCells.Add(new CACombatTopologyHazardCell
            {
                index = 5,
                x = 2,
                z = 1,
                packedGas = 0x01020304u,
                polluted = true,
                fires = "Fire12=0.5"
            });
            fixture.hazardSamples.Add(hazard);
            fixture.InvalidateEstimate();
            var jsonFirst = new StringBuilder();
            var jsonSecond = new StringBuilder();
            CACombatTopologyJson.AppendIncident(jsonFirst, fixture);
            CACombatTopologyJson.AppendIncident(jsonSecond, fixture);
            string json = jsonFirst.ToString();
            string jsonSha256 = Sha256(Encoding.UTF8.GetBytes(json));
            const string expectedJsonSha256 =
                "19C1EBBDF14180D951F7E9C43121B019EB323B024C06D0B3BBCF293F4ABB4EBE";
            bool jsonDeterministic = json == jsonSecond.ToString();
            bool jsonShape = json.StartsWith("{\"id\":\"CAT-regression\"",
                    StringComparison.Ordinal)
                && json.Contains("\"battlefieldReference\":{"
                    + "\"status\":\"legacy-unavailable\"}")
                && json.Contains("\"mapArtifacts\":[{")
                && json.Contains("\"stuffDefName\":\"BlocksLimestone\"")
                && json.Contains("\"known\":true")
                && json.Contains("\"unknownCellCount\":0")
                && json.Contains("\"observerPawnIds\":[\"Human12\",\"Human42\"]")
                && json.Contains("\"kind\":\"knowledge-reveal\"")
                && json.Contains("\"weaponSensorBurst\":true")
                && json.Contains("\"things\":[{\"thingId\":\"Chunk92\"")
                && json.Contains("\"kind\":\"thing-spawned\"")
                && json.Contains("\"kind\":\"thing-removed\"")
                && json.Contains("\"pawnObservationRadius\":8,"
                    + "\"weaponSensorBurstRadius\":6,"
                    + "\"includesLineOfSightCorridors\":true,"
                    + "\"weaponSensorBurst\":true")
                && json.Contains("\"toxicGas\":3")
                && json.EndsWith("}", StringComparison.Ordinal);
            bool jsonCanonicalHash = jsonSha256 == expectedJsonSha256;
            bool operationKinds = fixture.deltas.Count == 4
                && fixture.deltas[0].kind == "knowledge-reveal"
                && fixture.deltas[1].kind == "thing-spawned"
                && fixture.deltas[2].kind == "terrain-changed"
                && fixture.deltas[3].kind == "thing-removed";
            var dualJsonFirst = new StringBuilder();
            var dualJsonSecond = new StringBuilder();
            CACombatTopologyJson.AppendIncident(dualJsonFirst, dualFixture);
            CACombatTopologyJson.AppendIncident(dualJsonSecond, dualFixture);
            string dualJson = dualJsonFirst.ToString();
            bool dualExportDeterministic = dualJson
                == dualJsonSecond.ToString();
            bool fullReferencePartialKnowledge = referenceReplay
                && dualFixture.knownCellCount == 2
                && dualFixture.width * dualFixture.height == 6
                && dualJson.Contains("\"status\":\"captured\"")
                && dualJson.Contains("\"cellCount\":6")
                && dualJson.Contains("\"unknownCellCount\":4");
            bool remoteStaticNoKnowledge = dualFixture.knownCellIndices
                    .BinarySearch(5) < 0
                && dualFixture.deltas.Count == 0
                && dualJson.Contains("\"thingId\":\"RemoteWall91\"")
                && dualJson.Contains("\"kind\":\"thing-removed\"");
            bool referenceAccountingSeparated = battlefieldReference
                    .ChangeEstimatedBytes() > 0
                && dualFixture.EstimateBytes()
                    > dualFixture.baselineEstimatedBytes
                && dualFixture.DynamicEstimatedBytes() == 0;
            bool legacyV2Unavailable = fixture.battlefieldReference == null
                && json.Contains("\"status\":\"legacy-unavailable\"");
            bool passed = encodingStable && canonicalHash && replay
                && ordering && jsonDeterministic && jsonShape
                && jsonCanonicalHash && operationKinds
                && proximityContract && newestEntryAssociation
                && dualExportDeterministic
                && fullReferencePartialKnowledge
                && remoteStaticNoKnowledge && referenceAccountingSeparated
                && legacyV2Unavailable;
            return "[CA] combat-topology regression: "
                + (passed ? "PASS" : "FAIL")
                + "; contract read-only; RLE replay " + replay
                + "; canonical encoding " + encodingStable
                + "; fixed schema hash " + canonicalHash
                + "; duplicate LogID rejected and aliases ordered " + ordering
                + "; newest native Battle entry associated "
                    + newestEntryAssociation
                + "; full battlefield reference with partial dynamic mask "
                    + fullReferencePartialKnowledge
                + "; remote static removal preserves unknown dynamic state "
                    + remoteStaticNoKnowledge
                + "; reference changes excluded from dynamic caps "
                    + referenceAccountingSeparated
                + "; dual-layer export deterministic "
                    + dualExportDeterministic
                + "; legacy v2 reference unavailable "
                    + legacyV2Unavailable
                + "; add/change/remove fixture " + operationKinds
                + "; pawn-proximal knowledge fixture "
                    + proximityContract
                + " (known " + accumulatedReveal.Count + ", unknown "
                    + fixtureUnknown + ", blocker shadow unknown "
                    + blockerShadowUnknown + ", distant artifact rejected "
                    + distantArtifactRejected + ", remote mutation rejected "
                    + remoteMutationRejected + ")"
                + "; JSON deterministic " + jsonDeterministic
                + "; JSON shape " + jsonShape
                + "; fixed JSON hash " + jsonCanonicalHash
                + "; cells 6; runs " + runs.Count
                + "; payload bytes " + first.Length
                + "; SHA256 " + actualSha256
                + "; JSON SHA256 " + jsonSha256
                + "; knowledge SHA256 " + knowledgeSha256
                + "; reference SHA256 "
                    + referenceSha256
                + "; no map, pawn, battle, save, or gameplay state mutated";
        }

        private static bool FixtureFootprintKnown(int minX, int minZ,
            int maxX, int maxZ, int width, int height,
            SortedSet<int> known)
        {
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                {
                    if (x < 0 || z < 0 || x >= width || z >= height
                        || !known.Contains(z * width + x)) return false;
                }
            return true;
        }

        internal static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes ?? new byte[0]);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("X2",
                        CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        internal static int CurrentTicksAbs()
        {
            return Find.TickManager != null ? Find.TickManager.TicksAbs : -1;
        }

        private static Dictionary<string, ushort> PaletteIndex(
            List<string> palette)
        {
            if (palette.Count > ushort.MaxValue)
                throw new InvalidDataException(
                    "Combat topology palette exceeds UInt16 capacity");
            var result = new Dictionary<string, ushort>(
                StringComparer.Ordinal);
            for (int i = 0; i < palette.Count; i++)
                result[palette[i]] = (ushort)i;
            return result;
        }

        private static ushort PaletteValue(
            Dictionary<string, ushort> palette, string value)
        {
            ushort index;
            if (!palette.TryGetValue(value ?? "", out index))
                throw new InvalidDataException(
                    "Combat topology palette value missing");
            return index;
        }

        private static string DefName(Def def)
        {
            return def?.defName ?? "";
        }

        private static bool IsShoreline(IntVec3 cell, Map map,
            TerrainDef effective)
        {
            bool water = effective != null && effective.IsWater;
            for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
            {
                IntVec3 adjacent = cell + GenAdj.CardinalDirections[i];
                if (!adjacent.InBounds(map)) continue;
                TerrainDef terrain = map.terrainGrid.TerrainAt(adjacent);
                if ((terrain != null && terrain.IsWater) != water) return true;
            }
            return false;
        }

        private static string FiresAt(Map map, IntVec3 cell)
        {
            List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
            List<string> fires = null;
            for (int i = 0; i < things.Count; i++)
            {
                Fire fire = things[i] as Fire;
                if (fire == null) continue;
                if (fires == null) fires = new List<string>();
                fires.Add(fire.ThingID + "=" + fire.fireSize.ToString(
                    "R", CultureInfo.InvariantCulture));
            }
            if (fires == null) return null;
            fires.Sort(StringComparer.Ordinal);
            return string.Join("|", fires.ToArray());
        }

        private static int CompareThings(CACombatTopologyThingState a,
            CACombatTopologyThingState b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int compare = a.thingIdNumber.CompareTo(b.thingIdNumber);
            if (compare != 0) return compare;
            compare = a.z.CompareTo(b.z);
            if (compare != 0) return compare;
            compare = a.x.CompareTo(b.x);
            if (compare != 0) return compare;
            return string.CompareOrdinal(a.thingId, b.thingId);
        }

        private static int CompareReferenceArtifacts(
            CACombatTopologyReferenceArtifactState a,
            CACombatTopologyReferenceArtifactState b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int compare = a.thingIdNumber.CompareTo(b.thingIdNumber);
            if (compare != 0) return compare;
            compare = a.z.CompareTo(b.z);
            if (compare != 0) return compare;
            compare = a.x.CompareTo(b.x);
            if (compare != 0) return compare;
            return string.CompareOrdinal(a.thingId, b.thingId);
        }

        private static void WriteSignature(BinaryWriter writer,
            CACombatTopologyCellSignature value)
        {
            writer.Write(value.effective);
            writer.Write(value.top);
            writer.Write(value.under);
            writer.Write(value.roof);
            writer.Write(value.pathCost);
            writer.Write(value.flags);
            writer.Write(value.gas);
            writer.Write(value.edifice);
            writer.Write(value.cover);
        }

        private static CACombatTopologyCellSignature ReadSignature(
            BinaryReader reader)
        {
            return new CACombatTopologyCellSignature
            {
                effective = reader.ReadUInt16(),
                top = reader.ReadUInt16(),
                under = reader.ReadUInt16(),
                roof = reader.ReadUInt16(),
                pathCost = reader.ReadInt32(),
                flags = reader.ReadByte(),
                gas = reader.ReadUInt32(),
                edifice = reader.ReadInt32(),
                cover = reader.ReadInt32()
            };
        }

        private static void WriteReferenceSignature(BinaryWriter writer,
            CACombatTopologyReferenceCellSignature value)
        {
            writer.Write(value.effective);
            writer.Write(value.top);
            writer.Write(value.under);
            writer.Write(value.roof);
            writer.Write(value.flags);
        }

        private static CACombatTopologyReferenceCellSignature
            ReadReferenceSignature(BinaryReader reader)
        {
            return new CACombatTopologyReferenceCellSignature
            {
                effective = reader.ReadUInt16(),
                top = reader.ReadUInt16(),
                under = reader.ReadUInt16(),
                roof = reader.ReadUInt16(),
                flags = reader.ReadByte()
            };
        }

        private static bool ByteEqual(byte[] first, byte[] second)
        {
            if (first == null || second == null
                || first.Length != second.Length) return false;
            for (int i = 0; i < first.Length; i++)
                if (first[i] != second[i]) return false;
            return true;
        }
    }

    internal sealed partial class CACombatSpatialLogComponent
    {
        // These byte budgets also bound the campaign preflight's ELEMENT
        // economics: reference cells and delta cell lists estimate ~100
        // bytes apiece but serialize as ~12 XML elements each, so the
        // former 5-incident/2-MiB budget could stream past the preflight's
        // 1,000,000-element component limit after a day of chronic combat
        // and fail every save. Element-per-byte density varies about
        // fivefold across delta shapes, so the budget carries real margin:
        // a measured day-long chronic-combat save serialized ~820K
        // elements under the previous 2x512-KiB budget, so two incidents
        // at 256 KiB bound the worst case near ~410K elements with the
        // same counted-eviction degradation, and the preflight's overflow
        // diagnostic names the dominating element path if any future shape
        // breaks the model.
        private const int MaxTopologyIncidents = 2;
        private const int MaxTopologyDeltasPerIncident = 1024;
        private const int MaxTopologyIncidentBytes = 256 * 1024;
        private const int MaxTopologyHistoryBytes = 512 * 1024;
        private const int MaxBattlefieldReferenceChanges = 4096;
        private const int MaxBattlefieldReferenceChangeBytes = 1024 * 1024;

        private List<CACombatTopologyIncident> topologyIncidents =
            new List<CACombatTopologyIncident>();
        private int topologyCapacityEvictedIncidentCount;
        private int topologyObservationFailureCount;
        private int topologyLastObservationFailureTick = -1;
        private string topologyLastObservationFailure;
        private Dictionary<int, CACombatTopologyMapSubscription>
            topologySubscriptions;
        private Dictionary<string, string> topologyIncidentByBattleAlias;
        private Dictionary<string, int> topologyBattleEntryCounts;

        private void ExposeTopologyData()
        {
            Scribe_Collections.Look(ref topologyIncidents,
                "CA_combatTopologyIncidents", LookMode.Deep);
            Scribe_Values.Look(ref topologyCapacityEvictedIncidentCount,
                "CA_combatTopologyCapacityEvictedIncidentCount", 0);
            Scribe_Values.Look(ref topologyObservationFailureCount,
                "CA_combatTopologyObservationFailureCount", 0);
            Scribe_Values.Look(ref topologyLastObservationFailureTick,
                "CA_combatTopologyLastObservationFailureTick", -1);
            Scribe_Values.Look(ref topologyLastObservationFailure,
                "CA_combatTopologyLastObservationFailure");
            if (topologyIncidents == null)
                topologyIncidents = new List<CACombatTopologyIncident>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                topologySubscriptions = null;
                topologyIncidentByBattleAlias = null;
                topologyBattleEntryCounts = null;
            }
        }

        private void FinalizeTopologyData()
        {
            NormalizeTopologyData();
            PruneTopologyToRecords();
            RefreshTopologySubscriptions();
        }

        private void AttachTopology(LogEntry entry,
            CACombatSpatialLogRecord record, Battle battle)
        {
            if (entry == null || record == null) return;
            Map map = FindMap(record.mapId);
            if (map == null) return;

            string battleAlias = battle?.GetUniqueLoadID();
            EnsureTopologyBattleCaches();
            int entryCount = battle?.Entries?.Count ?? 0;
            CACombatTopologyIncident incident = null;
            bool scanBattle = true;
            string cachedIncidentId;
            int priorEntryCount;
            if (!battleAlias.NullOrEmpty()
                && topologyIncidentByBattleAlias.TryGetValue(battleAlias,
                    out cachedIncidentId)
                && topologyBattleEntryCounts.TryGetValue(battleAlias,
                    out priorEntryCount)
                && entryCount == priorEntryCount + 1)
            {
                incident = FindTopologyIncident(cachedIncidentId);
                scanBattle = !cachedIncidentId.NullOrEmpty()
                    && (incident == null || incident.mapId != map.uniqueID);
                if (!scanBattle && incident != null
                    && incident.mapId != map.uniqueID)
                {
                    incident = null;
                    scanBattle = true;
                }
            }
            if (scanBattle)
            {
                var related = new List<CACombatTopologyIncident>();
                if (battle?.Entries != null)
                {
                    for (int i = 0; i < battle.Entries.Count; i++)
                    {
                        LogEntry battleEntry = battle.Entries[i];
                        if (battleEntry == null) continue;
                        CACombatSpatialLogRecord priorRecord;
                        if (!records.TryGetValue(battleEntry.LogID,
                            out priorRecord) || priorRecord == null
                            || priorRecord.topologyIncidentId.NullOrEmpty())
                            continue;
                        CACombatTopologyIncident prior = FindTopologyIncident(
                            priorRecord.topologyIncidentId);
                        if (prior != null && !related.Contains(prior))
                            related.Add(prior);
                    }
                }
                related.Sort(CompareIncidents);
                for (int i = 0; i < related.Count; i++)
                {
                    CACombatTopologyIncident candidate = related[i];
                    if (candidate.mapId != map.uniqueID
                        || battleAlias.NullOrEmpty()
                        || candidate.nativeBattleAliases.BinarySearch(
                            battleAlias, StringComparer.Ordinal) < 0)
                        continue;
                    if (incident == null || candidate.lastLogTick
                        > incident.lastLogTick)
                        incident = candidate;
                }
                if (incident == null)
                    for (int i = 0; i < related.Count; i++)
                    {
                        CACombatTopologyIncident candidate = related[i];
                        if (candidate.mapId != map.uniqueID) continue;
                        if (incident == null || candidate.lastLogTick
                            > incident.lastLogTick)
                            incident = candidate;
                    }
                // A native absorption aliases all preserved topology segments to
                // the surviving Battle without collapsing their stable IDs.
                for (int i = 0; i < related.Count; i++)
                    related[i].AddBattleAlias(battleAlias);
            }
            bool createdIncident = false;
            if (incident == null && IsQualifyingHostileEntry(entry))
            {
                incident = CACombatTopologyCapture.CaptureBaseline(map,
                    entry, record, record.logId, record.eventTick,
                    battleAlias);
                if (incident != null)
                {
                    topologyIncidents.Add(incident);
                    int baselineBytes = incident.EstimateBytes();
                    incident.baselineEstimatedBytes = baselineBytes;
                    int referenceBaselineBytes = incident
                        .battlefieldReference?.baselineEstimatedBytes ?? 0;
                    incident.baselineExceededByteTarget = Math.Max(0,
                        baselineBytes - referenceBaselineBytes)
                            > MaxTopologyIncidentBytes;
                    incident.SetEstimatedBytes(baselineBytes);
                    createdIncident = true;
                }
            }
            if (!battleAlias.NullOrEmpty())
            {
                topologyIncidentByBattleAlias[battleAlias] =
                    incident?.incidentId;
                topologyBattleEntryCounts[battleAlias] = entryCount;
            }
            if (incident == null) return;

            record.topologyIncidentId = incident.incidentId;
            if (!incident.ContainsLogId(record.logId))
                incident.AddLogId(record.logId, !incident.truncated);
            incident.AddBattleAlias(battleAlias);
            incident.lastLogTick = Math.Max(incident.lastLogTick,
                record.eventTick);
            if (!incident.truncated)
            {
                if (!createdIncident)
                {
                    CACombatTopologyDelta reveal =
                        CACombatTopologyCapture.CaptureKnowledgeReveal(map,
                            entry, record, incident);
                    incident.InvalidateEstimate();
                    if (reveal != null)
                        AppendKnowledgeReveal(incident, reveal);
                }
                CACombatTopologyHazardSample sample =
                    CACombatTopologyCapture.CaptureHazards(map, entry, record,
                        incident);
                if (sample != null) AppendHazardSample(incident, sample);
            }
            EnforceTopologyRetention(incident.incidentId);
            RefreshTopologySubscriptions();
        }

        private void PruneTopologyToRecords()
        {
            if (topologyIncidents == null || topologyIncidents.Count == 0)
            {
                RefreshTopologySubscriptions();
                return;
            }
            var retained = new Dictionary<string, HashSet<int>>(
                StringComparer.Ordinal);
            var lastTicks = new Dictionary<string, int>(
                StringComparer.Ordinal);
            foreach (KeyValuePair<int, CACombatSpatialLogRecord> pair in records)
            {
                string id = pair.Value?.topologyIncidentId;
                if (id.NullOrEmpty()) continue;
                HashSet<int> ids;
                if (!retained.TryGetValue(id, out ids))
                {
                    ids = new HashSet<int>();
                    retained.Add(id, ids);
                }
                ids.Add(pair.Key);
                int priorTick;
                if (!lastTicks.TryGetValue(id, out priorTick)
                    || pair.Value.eventTick > priorTick)
                    lastTicks[id] = pair.Value.eventTick;
            }
            for (int i = topologyIncidents.Count - 1; i >= 0; i--)
            {
                CACombatTopologyIncident incident = topologyIncidents[i];
                HashSet<int> ids;
                if (incident == null || !retained.TryGetValue(
                    incident.incidentId, out ids))
                {
                    topologyIncidents.RemoveAt(i);
                    continue;
                }
                for (int j = incident.logIds.Count - 1; j >= 0; j--)
                    if (!ids.Contains(incident.logIds[j]))
                        incident.logIds.RemoveAt(j);
                for (int j = incident.hazardSamples.Count - 1; j >= 0; j--)
                {
                    CACombatTopologyHazardSample sample =
                        incident.hazardSamples[j];
                    if (sample == null || !ids.Contains(sample.logId))
                        incident.hazardSamples.RemoveAt(j);
                }
                var omitted = new List<int>();
                foreach (int logId in ids)
                    if (incident.logIds.BinarySearch(logId) < 0)
                        omitted.Add(logId);
                omitted.Sort();
                incident.omittedLogIdCount = omitted.Count;
                incident.firstOmittedLogId = omitted.Count > 0
                    ? omitted[0] : 0;
                incident.lastOmittedLogId = omitted.Count > 0
                    ? omitted[omitted.Count - 1] : 0;
                incident.lastLogTick = lastTicks[incident.incidentId];
                incident.InvalidateEstimate();
            }
            RefreshTopologySubscriptions();
        }

        private void NormalizeTopologyData()
        {
            if (topologyIncidents == null)
                topologyIncidents = new List<CACombatTopologyIncident>();
            for (int i = topologyIncidents.Count - 1; i >= 0; i--)
            {
                CACombatTopologyIncident incident = topologyIncidents[i];
                if (incident == null || incident.incidentId.NullOrEmpty())
                {
                    topologyIncidents.RemoveAt(i);
                    continue;
                }
                incident.logIds.Sort();
                DedupeSorted(incident.logIds);
                incident.nativeBattleAliases.Sort(StringComparer.Ordinal);
                DedupeSorted(incident.nativeBattleAliases);
                incident.knownCellIndices.Sort();
                DedupeSorted(incident.knownCellIndices);
                incident.knownCellCount = incident.knownCellIndices.Count;
                incident.observerPawnIds.Sort(StringComparer.Ordinal);
                DedupeSorted(incident.observerPawnIds);
                incident.observedThingIds.Sort(StringComparer.Ordinal);
                DedupeSorted(incident.observedThingIds);
                incident.deltas.Sort(CompareDeltas);
                int maximumSequence = -1;
                for (int j = 0; j < incident.deltas.Count; j++)
                {
                    CACombatTopologyDelta delta = incident.deltas[j];
                    if (delta != null)
                        maximumSequence = Math.Max(maximumSequence,
                            delta.sequence);
                }
                incident.nextSequence = Math.Max(incident.nextSequence,
                    maximumSequence + 1);
                CACombatTopologyBattlefieldReference reference =
                    incident.battlefieldReference;
                if (reference != null)
                {
                    reference.changes.Sort(CompareReferenceChanges);
                    int maximumReferenceSequence = -1;
                    for (int j = 0; j < reference.changes.Count; j++)
                    {
                        CACombatTopologyReferenceChange change =
                            reference.changes[j];
                        if (change != null)
                            maximumReferenceSequence = Math.Max(
                                maximumReferenceSequence, change.sequence);
                    }
                    reference.nextSequence = Math.Max(
                        reference.nextSequence,
                        maximumReferenceSequence + 1);
                }
                incident.InvalidateEstimate();
            }
            topologyIncidents.Sort(CompareIncidents);
        }

        private static void DedupeSorted<T>(List<T> values)
        {
            if (values == null) return;
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = values.Count - 1; i > 0; i--)
                if (comparer.Equals(values[i], values[i - 1]))
                    values.RemoveAt(i);
        }

        private static int CompareIncidents(CACombatTopologyIncident a,
            CACombatTopologyIncident b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int compare = a.firstLogTick.CompareTo(b.firstLogTick);
            if (compare != 0) return compare;
            compare = a.firstLogId.CompareTo(b.firstLogId);
            if (compare != 0) return compare;
            return string.CompareOrdinal(a.incidentId, b.incidentId);
        }

        private static int CompareDeltas(CACombatTopologyDelta a,
            CACombatTopologyDelta b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int compare = a.tick.CompareTo(b.tick);
            return compare != 0 ? compare : a.sequence.CompareTo(b.sequence);
        }

        private static int CompareReferenceChanges(
            CACombatTopologyReferenceChange a,
            CACombatTopologyReferenceChange b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int compare = a.tick.CompareTo(b.tick);
            return compare != 0 ? compare : a.sequence.CompareTo(b.sequence);
        }

        private CACombatTopologyIncident FindTopologyIncident(string id)
        {
            if (id.NullOrEmpty()) return null;
            for (int i = 0; i < topologyIncidents.Count; i++)
            {
                CACombatTopologyIncident incident = topologyIncidents[i];
                if (incident != null && incident.incidentId == id)
                    return incident;
            }
            return null;
        }

        private void EnsureTopologyBattleCaches()
        {
            if (topologyIncidentByBattleAlias == null)
                topologyIncidentByBattleAlias =
                    new Dictionary<string, string>(StringComparer.Ordinal);
            if (topologyBattleEntryCounts == null)
                topologyBattleEntryCounts =
                    new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private static Map FindMap(int mapId)
        {
            List<Map> maps = Find.Maps;
            if (maps == null) return null;
            for (int i = 0; i < maps.Count; i++)
                if (maps[i] != null && maps[i].uniqueID == mapId)
                    return maps[i];
            return null;
        }

        private static bool IsQualifyingHostileEntry(LogEntry entry)
        {
            var pawns = new List<Pawn>();
            foreach (Thing concern in entry.GetConcerns())
            {
                Pawn pawn = concern as Pawn;
                if (pawn != null && !pawns.Contains(pawn)) pawns.Add(pawn);
            }
            for (int i = 0; i < pawns.Count; i++)
                for (int j = i + 1; j < pawns.Count; j++)
                    if (pawns[i].HostileTo(pawns[j])
                        || pawns[j].HostileTo(pawns[i]))
                        return true;
            return false;
        }

        private void AppendHazardSample(CACombatTopologyIncident incident,
            CACombatTopologyHazardSample sample)
        {
            if (incident.truncated) return;
            int sampleBytes = sample.EstimateBytes();
            int projected = incident.EstimateBytes() + sampleBytes;
            if (incident.DynamicEstimatedBytes() + sampleBytes
                > MaxTopologyIncidentBytes)
            {
                MarkTruncated(incident, sample.tick,
                    "two-mebibyte dynamic incident evidence cap before hazard sample");
                return;
            }
            if (!EnsureTopologyCapacity(sampleBytes, incident.incidentId))
            {
                MarkTruncated(incident, sample.tick,
                    "eight-mebibyte dynamic topology history cap before hazard sample; no older incident was removable");
                return;
            }
            incident.hazardSamples.Add(sample);
            incident.SetEstimatedBytes(projected);
        }

        private void AppendKnowledgeReveal(CACombatTopologyIncident incident,
            CACombatTopologyDelta delta)
        {
            if (incident == null || incident.truncated || delta == null)
                return;
            if (incident.deltas.Count >= MaxTopologyDeltasPerIncident)
            {
                MarkTruncated(incident, delta.tick,
                    "8192-delta dynamic incident evidence cap");
                return;
            }
            int metadataBytes = delta.cells.Count * 4;
            for (int i = 0; i < delta.things.Count; i++)
            {
                string id = delta.things[i]?.thingId;
                if (!id.NullOrEmpty() && incident.observedThingIds.BinarySearch(
                    id, StringComparer.Ordinal) < 0)
                    metadataBytes += id.Length * 2;
            }
            int deltaBytes = delta.EstimateBytes() + metadataBytes;
            int projected = incident.EstimateBytes() + deltaBytes;
            if (incident.DynamicEstimatedBytes() + deltaBytes
                > MaxTopologyIncidentBytes)
            {
                MarkTruncated(incident, delta.tick,
                    "two-mebibyte dynamic incident evidence cap before knowledge reveal");
                return;
            }
            if (!EnsureTopologyCapacity(deltaBytes, incident.incidentId))
            {
                MarkTruncated(incident, delta.tick,
                    "eight-mebibyte dynamic topology history cap before knowledge reveal; no older incident was removable");
                return;
            }
            delta.sequence = incident.nextSequence++;
            incident.deltas.Add(delta);
            for (int i = 0; i < delta.cells.Count; i++)
            {
                int index = delta.cells[i]?.index ?? -1;
                if (index < 0) continue;
                int insertion = incident.knownCellIndices.BinarySearch(index);
                if (insertion < 0)
                    incident.knownCellIndices.Insert(~insertion, index);
            }
            for (int i = 0; i < delta.things.Count; i++)
                CACombatTopologyCapture.AddSortedUnique(
                    incident.observedThingIds, delta.things[i]?.thingId);
            incident.knownCellCount = incident.knownCellIndices.Count;
            incident.SetEstimatedBytes(projected);
        }

        private void AppendDelta(CACombatTopologyIncident incident,
            string kind, string detail, Map map, IntVec3 cell, Thing thing)
        {
            if (incident == null || incident.truncated) return;
            var delta = new CACombatTopologyDelta
            {
                tick = CACombatTopologyCapture.CurrentTicksAbs(),
                sequence = incident.nextSequence,
                kind = kind,
                detail = detail,
                mapId = map?.uniqueID ?? -1,
                thing = CACombatTopologyCapture.IsTopologyThing(thing)
                    ? CACombatTopologyCapture.CaptureThing(thing) : null
            };
            if (map != null && thing != null
                && CACombatTopologyCapture.IsTopologyThing(thing))
            {
                CellRect footprint = thing.OccupiedRect();
                for (int z = footprint.minZ; z <= footprint.maxZ; z++)
                    for (int x = footprint.minX; x <= footprint.maxX; x++)
                    {
                        IntVec3 footprintCell = new IntVec3(x, 0, z);
                        if (footprintCell.InBounds(map))
                            delta.cells.Add(CACombatTopologyCapture
                                .CaptureCell(map, footprintCell));
                    }
            }
            else if (map != null && cell.IsValid && cell.InBounds(map))
            {
                delta.cells.Add(CACombatTopologyCapture.CaptureCell(map,
                    cell));
            }
            if (incident.deltas.Count >= MaxTopologyDeltasPerIncident)
            {
                MarkTruncated(incident, delta.tick,
                    "8192-delta incident evidence cap");
                return;
            }
            int deltaBytes = delta.EstimateBytes();
            int newlyKnown = 0;
            for (int i = 0; i < delta.cells.Count; i++)
            {
                int index = delta.cells[i]?.index ?? -1;
                if (index >= 0 && incident.knownCellIndices.BinarySearch(index)
                    < 0) newlyKnown++;
            }
            deltaBytes += newlyKnown * 4;
            string newlyObservedThingId = delta.thing?.thingId;
            if (!newlyObservedThingId.NullOrEmpty()
                && incident.observedThingIds.BinarySearch(
                    newlyObservedThingId, StringComparer.Ordinal) < 0)
                deltaBytes += newlyObservedThingId.Length * 2;
            int projected = incident.EstimateBytes() + deltaBytes;
            if (incident.DynamicEstimatedBytes() + deltaBytes
                > MaxTopologyIncidentBytes)
            {
                MarkTruncated(incident, delta.tick,
                    "two-mebibyte dynamic incident evidence cap");
                return;
            }
            if (!EnsureTopologyCapacity(deltaBytes,
                incident.incidentId))
            {
                MarkTruncated(incident, delta.tick,
                    "eight-mebibyte dynamic topology history cap before delta; no older incident was removable");
                return;
            }
            incident.nextSequence++;
            incident.deltas.Add(delta);
            for (int i = 0; i < delta.cells.Count; i++)
            {
                int index = delta.cells[i]?.index ?? -1;
                if (index < 0) continue;
                int insertion = incident.knownCellIndices.BinarySearch(index);
                if (insertion < 0)
                    incident.knownCellIndices.Insert(~insertion, index);
            }
            incident.knownCellCount = incident.knownCellIndices.Count;
            CACombatTopologyCapture.AddSortedUnique(
                incident.observedThingIds, newlyObservedThingId);
            incident.SetEstimatedBytes(projected);
        }

        private void MarkTruncated(CACombatTopologyIncident incident,
            int tick, string reason)
        {
            if (incident == null) return;
            if (incident.truncated)
            {
                if (!reason.NullOrEmpty()
                    && (incident.truncationReason.NullOrEmpty()
                        || incident.truncationReason.IndexOf(reason,
                            StringComparison.Ordinal) < 0))
                {
                    incident.truncationReason =
                        (incident.truncationReason.NullOrEmpty() ? ""
                            : incident.truncationReason + "; ") + reason;
                    incident.InvalidateEstimate();
                }
                RefreshTopologySubscriptions();
                return;
            }
            incident.truncated = true;
            incident.truncatedAtTick = tick;
            incident.truncationReason = reason;
            incident.InvalidateEstimate();
            RefreshTopologySubscriptions();
        }

        private void AppendReferenceChange(CACombatTopologyIncident incident,
            string kind, string detail, Map map, IntVec3 cell, Thing thing)
        {
            CACombatTopologyBattlefieldReference reference =
                incident?.battlefieldReference;
            if (reference == null || reference.changesTruncated) return;
            var change = new CACombatTopologyReferenceChange
            {
                tick = CACombatTopologyCapture.CurrentTicksAbs(),
                sequence = reference.nextSequence,
                kind = kind,
                detail = detail,
                mapId = map?.uniqueID ?? -1,
                cell = map != null && cell.IsValid && cell.InBounds(map)
                    ? CACombatTopologyCapture.CaptureReferenceCell(map, cell)
                    : null,
                artifact = CACombatTopologyCapture
                    .IsBattlefieldReferenceArtifact(thing)
                        ? CACombatTopologyCapture
                            .CaptureReferenceArtifact(thing) : null
            };
            int changeBytes = change.EstimateBytes();
            if (reference.changes.Count >= MaxBattlefieldReferenceChanges)
            {
                MarkReferenceChangesTruncated(incident, change.tick,
                    "4096-change battlefield reference history cap");
                return;
            }
            if (reference.ChangeEstimatedBytes() + changeBytes
                > MaxBattlefieldReferenceChangeBytes)
            {
                MarkReferenceChangesTruncated(incident, change.tick,
                    "one-mebibyte battlefield reference change cap");
                return;
            }
            int projected = incident.EstimateBytes() + changeBytes;
            reference.nextSequence++;
            reference.changes.Add(change);
            reference.IncreaseChangeEstimatedBytes(changeBytes);
            incident.SetEstimatedBytes(projected);
        }

        private void MarkReferenceChangesTruncated(
            CACombatTopologyIncident incident, int tick, string reason)
        {
            CACombatTopologyBattlefieldReference reference =
                incident?.battlefieldReference;
            if (reference == null) return;
            reference.changesTruncated = true;
            reference.changesTruncatedAtTick = tick;
            reference.changesTruncationReason = reason;
            reference.InvalidateChangeEstimate();
            incident.InvalidateEstimate();
            RefreshTopologySubscriptions();
        }

        private void RecordCellChange(Map map, IntVec3 cell, string kind,
            string detail = null)
        {
            try
            {
                if (map == null || !cell.InBounds(map)) return;
                if (IsBattlefieldReferenceCellChange(kind))
                    ForEachReferenceIncident(map.uniqueID, incident =>
                        AppendReferenceChange(incident, kind, detail, map,
                            cell, null));
                ForEachLiveIncident(map.uniqueID, incident =>
                {
                    if (CACombatTopologyCapture.IsCurrentlyObserved(
                        incident, map, cell))
                    {
                        CACombatTopologyDelta reveal =
                            CACombatTopologyCapture
                                .CaptureCurrentObserverReveal(map, incident,
                                    cell);
                        if (reveal != null)
                            AppendKnowledgeReveal(incident, reveal);
                        AppendDelta(incident, kind, detail, map, cell, null);
                    }
                });
            }
            catch
            {
                // Engine notification observers never participate in the mutation.
                RecordTopologyObservationFailure(map?.uniqueID ?? -1,
                    kind + ":cell-capture");
            }
        }

        private void RecordThingChange(Map map, Thing thing, string kind,
            string detail = null)
        {
            try
            {
                if (map == null) return;
                bool dynamicThing = CACombatTopologyCapture
                    .IsTopologyThing(thing);
                bool referenceThing = IsBattlefieldReferenceThingChange(kind)
                    && CACombatTopologyCapture
                        .IsBattlefieldReferenceArtifact(thing);
                if (!dynamicThing && !referenceThing) return;
                IntVec3 cell = thing.Position;
                if (referenceThing)
                    ForEachReferenceIncident(map.uniqueID, incident =>
                        AppendReferenceChange(incident, kind, detail, map,
                            IntVec3.Invalid, thing));
                if (dynamicThing)
                    ForEachLiveIncident(map.uniqueID, incident =>
                    {
                        if (CACombatTopologyCapture
                            .AllFootprintCellsCurrentlyObserved(incident, map,
                                thing))
                        {
                            CACombatTopologyDelta reveal =
                                CACombatTopologyCapture
                                    .CaptureCurrentObserverReveal(map,
                                        incident, cell);
                            if (reveal != null)
                                AppendKnowledgeReveal(incident, reveal);
                            AppendDelta(incident, kind, detail, map, cell,
                                thing);
                        }
                    });
            }
            catch
            {
                // Engine notification observers never participate in the mutation.
                RecordTopologyObservationFailure(map?.uniqueID ?? -1,
                    kind + ":thing-capture");
            }
        }

        internal void RecordTopologyObservationFailure(int mapId,
            string context)
        {
            try
            {
                int tick = CACombatTopologyCapture.CurrentTicksAbs();
                topologyObservationFailureCount++;
                topologyLastObservationFailureTick = tick;
                topologyLastObservationFailure = context;
                for (int i = 0; i < topologyIncidents.Count; i++)
                {
                    CACombatTopologyIncident incident = topologyIncidents[i];
                    if (!IsLive(incident, mapId, tick)
                        && !IsReferenceLive(incident, mapId, tick))
                        continue;
                    incident.observationFailureCount++;
                    incident.lastObservationFailureTick = tick;
                    incident.lastObservationFailure = context;
                    incident.InvalidateEstimate();
                }
            }
            catch
            {
                // Even failure accounting is subordinate to the game mutation.
            }
        }

        private void ForEachLiveIncident(int mapId,
            Action<CACombatTopologyIncident> action)
        {
            int now = CACombatTopologyCapture.CurrentTicksAbs();
            var live = new List<CACombatTopologyIncident>();
            for (int i = 0; i < topologyIncidents.Count; i++)
            {
                CACombatTopologyIncident incident = topologyIncidents[i];
                if (!IsLive(incident, mapId, now)) continue;
                live.Add(incident);
            }
            for (int i = 0; i < live.Count; i++)
                if (topologyIncidents.Contains(live[i])) action(live[i]);
            if (live.Count == 0) RefreshTopologySubscriptions();
        }

        private void ForEachReferenceIncident(int mapId,
            Action<CACombatTopologyIncident> action)
        {
            int now = CACombatTopologyCapture.CurrentTicksAbs();
            var live = new List<CACombatTopologyIncident>();
            for (int i = 0; i < topologyIncidents.Count; i++)
            {
                CACombatTopologyIncident incident = topologyIncidents[i];
                if (IsReferenceLive(incident, mapId, now)) live.Add(incident);
            }
            for (int i = 0; i < live.Count; i++)
                if (topologyIncidents.Contains(live[i])) action(live[i]);
            if (live.Count == 0) RefreshTopologySubscriptions();
        }

        private static bool IsLive(CACombatTopologyIncident incident,
            int mapId, int now)
        {
            return incident != null && incident.mapId == mapId
                && !incident.truncated && incident.lastLogTick >= 0
                && now >= incident.lastLogTick
                && now - incident.lastLogTick <= Battle.TicksForBattleExit;
        }

        private static bool IsReferenceLive(
            CACombatTopologyIncident incident, int mapId, int now)
        {
            CACombatTopologyBattlefieldReference reference =
                incident?.battlefieldReference;
            return incident != null && incident.mapId == mapId
                && reference != null && !reference.changesTruncated
                && incident.lastLogTick >= 0 && now >= incident.lastLogTick
                && now - incident.lastLogTick <= Battle.TicksForBattleExit;
        }

        private static bool IsBattlefieldReferenceCellChange(string kind)
        {
            return kind == "terrain-changed"
                || kind == "derived-shoreline-recalculated"
                || kind == "roof-changed"
                || kind == "gravship-static-grid-changed";
        }

        private static bool IsBattlefieldReferenceThingChange(string kind)
        {
            return kind == "thing-spawned" || kind == "thing-removed";
        }

        private void EnforceTopologyRetention(string protectedIncidentId)
        {
            if (!EnsureTopologyCapacity(0, protectedIncidentId))
            {
                CACombatTopologyIncident current = FindTopologyIncident(
                    protectedIncidentId);
                MarkTruncated(current,
                    CACombatTopologyCapture.CurrentTicksAbs(),
                    "topology retention cap; no older incident was removable");
            }
        }

        private bool EnsureTopologyCapacity(int additionalBytes,
            string protectedIncidentId)
        {
            while (topologyIncidents.Count > MaxTopologyIncidents
                || TopologyDynamicHistoryBytes() + additionalBytes
                    > MaxTopologyHistoryBytes)
            {
                int remove = OldestCompletedIndex(protectedIncidentId);
                if (remove < 0) remove = OldestUnprotectedIndex(
                    protectedIncidentId);
                if (remove < 0) return false;
                RemoveTopologyIncidentAt(remove);
            }
            return true;
        }

        private void RemoveTopologyIncidentAt(int index)
        {
            string removedId = topologyIncidents[index].incidentId;
            topologyIncidents.RemoveAt(index);
            topologyCapacityEvictedIncidentCount++;
            foreach (KeyValuePair<int, CACombatSpatialLogRecord> pair
                in records)
                if (pair.Value?.topologyIncidentId == removedId)
                    pair.Value.topologyIncidentId = null;
        }

        private int TopologyHistoryBytes()
        {
            int total = 0;
            for (int i = 0; i < topologyIncidents.Count; i++)
                total += topologyIncidents[i]?.EstimateBytes() ?? 0;
            return total;
        }

        private int TopologyDynamicHistoryBytes()
        {
            int total = 0;
            for (int i = 0; i < topologyIncidents.Count; i++)
                total += topologyIncidents[i]?.DynamicEstimatedBytes() ?? 0;
            return total;
        }

        private int OldestCompletedIndex(string protectedIncidentId)
        {
            int now = CACombatTopologyCapture.CurrentTicksAbs();
            for (int i = 0; i < topologyIncidents.Count; i++)
            {
                CACombatTopologyIncident incident = topologyIncidents[i];
                if (incident?.incidentId == protectedIncidentId) continue;
                if (!IsLive(incident, incident.mapId, now)
                    && !IsReferenceLive(incident, incident.mapId, now))
                    return i;
            }
            return -1;
        }

        private int OldestUnprotectedIndex(string protectedIncidentId)
        {
            for (int i = 0; i < topologyIncidents.Count; i++)
                if (topologyIncidents[i]?.incidentId != protectedIncidentId)
                    return i;
            return -1;
        }

        private void RefreshTopologySubscriptions()
        {
            if (topologySubscriptions == null)
                topologySubscriptions =
                    new Dictionary<int, CACombatTopologyMapSubscription>();
            int now = CACombatTopologyCapture.CurrentTicksAbs();
            var needed = new HashSet<int>();
            for (int i = 0; i < topologyIncidents.Count; i++)
            {
                CACombatTopologyIncident incident = topologyIncidents[i];
                if (incident != null && (IsLive(incident, incident.mapId, now)
                    || IsReferenceLive(incident, incident.mapId, now)))
                    needed.Add(incident.mapId);
            }
            var stale = new List<int>();
            foreach (KeyValuePair<int, CACombatTopologyMapSubscription> pair
                in topologySubscriptions)
                if (!needed.Contains(pair.Key)) stale.Add(pair.Key);
            for (int i = 0; i < stale.Count; i++)
            {
                topologySubscriptions[stale[i]].Dispose();
                topologySubscriptions.Remove(stale[i]);
            }
            foreach (int mapId in needed)
            {
                if (topologySubscriptions.ContainsKey(mapId)) continue;
                Map map = FindMap(mapId);
                if (map != null)
                    topologySubscriptions.Add(mapId,
                        new CACombatTopologyMapSubscription(this, map));
            }
        }

        private sealed class CACombatTopologyMapSubscription : IDisposable
        {
            private readonly CACombatSpatialLogComponent owner;
            private readonly Map map;
            private readonly CACombatTopologyReferenceFingerprint[]
                referenceFingerprints;
            private readonly Dictionary<int, string> pendingBuildingRemovals =
                new Dictionary<int, string>();

            internal CACombatTopologyMapSubscription(
                CACombatSpatialLogComponent owner, Map map)
            {
                this.owner = owner;
                this.map = map;
                referenceFingerprints =
                    new CACombatTopologyReferenceFingerprint[
                        map.cellIndices.NumGridCells];
                for (int i = 0; i < referenceFingerprints.Length; i++)
                    referenceFingerprints[i] = CACombatTopologyCapture
                        .CaptureReferenceFingerprint(map, i);
                map.events.ThingSpawned += ThingSpawned;
                map.events.BuildingSpawned += BuildingSpawned;
                map.events.ThingDespawned += ThingDespawned;
                map.events.BuildingDespawned += BuildingDespawned;
                map.events.BuildingHitPointsChanged += BuildingHitPointsChanged;
                map.events.TerrainChanged += TerrainChanged;
                map.events.PathCostRecalculate += PathCostRecalculate;
                map.events.DoorOpened += DoorOpened;
                map.events.DoorClosed += DoorClosed;
                map.events.RoofChanged += RoofChanged;
            }

            public void Dispose()
            {
                map.events.ThingSpawned -= ThingSpawned;
                map.events.BuildingSpawned -= BuildingSpawned;
                map.events.ThingDespawned -= ThingDespawned;
                map.events.BuildingDespawned -= BuildingDespawned;
                map.events.BuildingHitPointsChanged -= BuildingHitPointsChanged;
                map.events.TerrainChanged -= TerrainChanged;
                map.events.PathCostRecalculate -= PathCostRecalculate;
                map.events.DoorOpened -= DoorOpened;
                map.events.DoorClosed -= DoorClosed;
                map.events.RoofChanged -= RoofChanged;
                pendingBuildingRemovals.Clear();
            }

            private void ThingSpawned(Thing thing)
            {
                if (thing is Building) return;
                owner.RecordThingChange(map, thing, "thing-spawned");
            }

            private void BuildingSpawned(Building building)
            {
                owner.RecordThingChange(map, building, "thing-spawned");
            }

            private void ThingDespawned(Thing thing)
            {
                // Building.DeSpawn removes its edifice only after the generic
                // ThingDespawned event. Preserve the exact mode while the base
                // DeSpawn context is live, then let BuildingDespawned capture
                // the finalized cell.
                Building building = thing as Building;
                if (building != null)
                {
                    pendingBuildingRemovals[building.thingIDNumber] =
                        CACombatTopologyOperationContext.DespawnDetail(building);
                    return;
                }
                owner.RecordThingChange(map, thing, "thing-removed",
                    CACombatTopologyOperationContext.DespawnDetail(thing));
            }

            private void BuildingDespawned(Building building)
            {
                string detail;
                if (!pendingBuildingRemovals.TryGetValue(
                    building.thingIDNumber, out detail))
                    detail = CACombatTopologyOperationContext
                        .DespawnDetail(building);
                pendingBuildingRemovals.Remove(building.thingIDNumber);
                owner.RecordThingChange(map, building, "thing-removed",
                    detail);
            }

            private void BuildingHitPointsChanged(Building building)
            {
                owner.RecordThingChange(map, building,
                    "building-hit-points-changed");
            }

            private void TerrainChanged(IntVec3 cell)
            {
                RecordReferenceCellIfChanged(cell, "terrain-changed");
                for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
                {
                    IntVec3 adjacent = cell + GenAdj.CardinalDirections[i];
                    if (adjacent.InBounds(map))
                        RecordReferenceCellIfChanged(adjacent,
                            "derived-shoreline-recalculated",
                            "neighbor-terrain-changed");
                }
            }

            private void PathCostRecalculate(IntVec3 cell)
            {
                // Gravship generation removes terrain and roofs through unsafe
                // grid helpers, then emits this path event for each completed
                // cell. The fingerprint cache records only resulting static
                // differences and ignores earlier substructure despawn events.
                if (GravshipUtility.generatingGravship)
                {
                    RecordReferenceCellIfChanged(cell,
                        "gravship-static-grid-changed",
                        "path-recalculated-after-unsafe-grid-removal");
                    for (int i = 0;
                        i < GenAdj.CardinalDirections.Length; i++)
                    {
                        IntVec3 adjacent = cell
                            + GenAdj.CardinalDirections[i];
                        if (adjacent.InBounds(map))
                            RecordReferenceCellIfChanged(adjacent,
                                "derived-shoreline-recalculated",
                                "neighbor-gravship-terrain-removed");
                    }
                }
                owner.RecordCellChange(map, cell,
                    "path-cost-recalculated");
            }

            private void DoorOpened(Building_Door door)
            {
                owner.RecordThingChange(map, door, "door-opened");
            }

            private void DoorClosed(Building_Door door)
            {
                owner.RecordThingChange(map, door, "door-closed");
            }

            private void RoofChanged(IntVec3 cell)
            {
                RecordReferenceCellIfChanged(cell, "roof-changed");
            }

            private void RecordReferenceCellIfChanged(IntVec3 cell,
                string kind, string detail = null)
            {
                try
                {
                    if (!cell.InBounds(map)) return;
                    int index = map.cellIndices.CellToIndex(cell);
                    CACombatTopologyReferenceFingerprint current =
                        CACombatTopologyCapture.CaptureReferenceFingerprint(
                            map, index);
                    if (referenceFingerprints[index].Equals(current)) return;
                    referenceFingerprints[index] = current;
                    owner.RecordCellChange(map, cell, kind, detail);
                }
                catch
                {
                    // MapEvents observers never participate in the mutation.
                    owner.RecordTopologyObservationFailure(map?.uniqueID ?? -1,
                        kind + ":reference-fingerprint");
                }
            }
        }

        internal static string TopologyRegressionReceipt()
        {
            return CACombatTopologyCapture.RegressionReceipt();
        }

        internal static void ReportTopologyObservationFailure(int mapId,
            string context)
        {
            try
            {
                CurrentComponent()?.RecordTopologyObservationFailure(mapId,
                    context);
            }
            catch
            {
                // Failure reporting cannot participate in the observed operation.
            }
        }

        internal static string TopologyExportReceipt()
        {
            CACombatSpatialLogComponent component = CurrentComponent();
            if (component == null)
                return "[CA] combat-topology export refused: no current game component";
            try
            {
                int eventCount;
                int dynamicTruncatedCount;
                int referenceCapturedCount;
                int referenceLegacyUnavailableCount;
                int referenceChangeTruncatedCount;
                string json = component.BuildTopologyJson(out eventCount,
                    out dynamicTruncatedCount, out referenceCapturedCount,
                    out referenceLegacyUnavailableCount,
                    out referenceChangeTruncatedCount);
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                string path = Path.Combine(
                    GenFilePaths.DevOutputFolderPath,
                    "ColonistAwareness.combat-topology.json");
                Directory.CreateDirectory(
                    GenFilePaths.DevOutputFolderPath);
                File.WriteAllBytes(path, bytes);
                return "[CA] combat-topology export: PASS; path " + path
                    + "; bytes " + bytes.Length
                    + "; SHA256 "
                    + CACombatTopologyCapture.Sha256(bytes)
                    + "; incidents " + component.topologyIncidents.Count
                    + "; native events " + eventCount
                    + "; dynamic-truncated incidents "
                    + dynamicTruncatedCount
                    + "; battlefield references captured "
                    + referenceCapturedCount
                    + "; legacy incidents without battlefield reference "
                    + referenceLegacyUnavailableCount
                    + "; reference-change-truncated incidents "
                    + referenceChangeTruncatedCount
                    + "; capacity-evicted incidents "
                    + component.topologyCapacityEvictedIncidentCount
                    + "; observation failures "
                    + component.topologyObservationFailureCount
                    + "; estimated topology bytes "
                    + component.TopologyHistoryBytes()
                    + "; estimated dynamic topology bytes "
                    + component.TopologyDynamicHistoryBytes()
                    + "; estimated battlefield reference bytes "
                    + component.TopologyReferenceHistoryBytes()
                    + "; estimated battlefield reference baseline bytes "
                    + component.TopologyReferenceBaselineHistoryBytes()
                    + "; estimated battlefield reference change bytes "
                    + component.TopologyReferenceChangeHistoryBytes()
                    + "; dynamic retention byte limit "
                    + MaxTopologyHistoryBytes
                    + (component.topologyObservationFailureCount > 0
                        ? "; last failure tick "
                            + component.topologyLastObservationFailureTick
                            + " context "
                            + component.topologyLastObservationFailure
                        : "")
                    + "; export is read-only and did not mutate a map, pawn, battle, save, or gameplay decision";
            }
            catch (Exception exception)
            {
                return "[CA] combat-topology export: FAIL; " + exception;
            }
        }

        private string BuildTopologyJson(out int eventCount,
            out int dynamicTruncatedCount, out int referenceCapturedCount,
            out int referenceLegacyUnavailableCount,
            out int referenceChangeTruncatedCount)
        {
            List<CACombatTopologyExportEvent> events =
                CollectTopologyEvents();
            eventCount = events.Count;
            dynamicTruncatedCount = 0;
            referenceCapturedCount = 0;
            referenceLegacyUnavailableCount = 0;
            referenceChangeTruncatedCount = 0;
            for (int i = 0; i < topologyIncidents.Count; i++)
            {
                CACombatTopologyIncident incident = topologyIncidents[i];
                if (incident?.truncated == true) dynamicTruncatedCount++;
                if (incident?.battlefieldReference == null)
                {
                    referenceLegacyUnavailableCount++;
                    continue;
                }
                referenceCapturedCount++;
                if (incident.battlefieldReference.changesTruncated)
                    referenceChangeTruncatedCount++;
            }

            var sb = new StringBuilder(Math.Max(4096,
                TopologyHistoryBytes() * 2));
            sb.Append('{');
            CACombatTopologyJson.Property(sb, "schema",
                "CA-combat-topology-v3", true);
            CACombatTopologyJson.Property(sb,
                "dynamicTruncatedIncidentCount", dynamicTruncatedCount,
                true);
            CACombatTopologyJson.Property(sb,
                "battlefieldReferenceCapturedIncidentCount",
                referenceCapturedCount, true);
            CACombatTopologyJson.Property(sb,
                "battlefieldReferenceLegacyUnavailableIncidentCount",
                referenceLegacyUnavailableCount, true);
            CACombatTopologyJson.Property(sb,
                "battlefieldReferenceChangeTruncatedIncidentCount",
                referenceChangeTruncatedCount, true);
            CACombatTopologyJson.Property(sb, "capturePolicy",
                "the first qualifying hostile event captures one incident-owned full-map battlefield reference; pawn-proximal line-of-sight evidence remains a separate dynamic layer; copied projectile impacts add bounded sensor bursts and exact visible corridors; dynamic engine-event deltas are accepted only near current observer pawns during the native 5000-tick battle window", true);
            CACombatTopologyJson.Property(sb, "knowledgePolicy",
                "the battlefield reference is analyst ground truth, not pawn knowledge; the separate accumulated combat-evidence mask is analogous to fog of war and does not read, write, or claim equivalence with RimWorld FogGrid; a known dynamic cell is last-observed evidence, not continuous surveillance", true);
            CACombatTopologyJson.Property(sb, "battlefieldReferencePolicy",
                "all terrain, top terrain, under terrain, roof, water, shoreline, natural-roof, and thick-roof cells plus map-wide combat-geometry artifacts are captured once; actual terrain, roof, and reference-artifact spawn or despawn changes are recorded without changing pawn knowledge; compact cell fingerprints suppress no-op shoreline updates and detect Odyssey gravship unsafe-grid removals at their engine-emitted path recalculation; legacy v2 incidents remain unavailable and are never backfilled", true);
            CACombatTopologyJson.Property(sb, "pawnObservationRadius",
                CACombatTopologyCapture.ObservationRadius, true);
            CACombatTopologyJson.Property(sb, "weaponSensorBurstRadius",
                CACombatTopologyCapture.WeaponSensorBurstRadius, true);
            CACombatTopologyJson.Property(sb, "artifactScopePolicy",
                "the battlefield reference contains buildings, including walls, doors, natural rock, and turrets, plus chunks and things whose engine BaseBlockChance is positive; raw def, category, passability, fill, block-chance, region, and reachability fields are exported while pawns, fire, and filth are excluded; dynamic instance state remains serialized only when its full occupied footprint is known or currently observed", true);
            CACombatTopologyJson.Property(sb, "hazardSamplingPolicy",
                "gas, pollution, and fire are sampled only inside the current pawn line-of-sight reveal or approved projectile-impact sensor burst, intersected with accumulated known cells; prior knowledge never grants continuous surveillance", true);
            CACombatTopologyJson.Property(sb, "interpretationPolicy",
                "raw definitions and engine state only; no inferred tactical ruin, choke point, defended side, or beachhead", true);
            CACombatTopologyJson.Property(sb, "displayTextPolicy",
                "displayText invokes the most-derived protected RimWorld renderer under LogID-seeded Rand state without assigning the public renderer's cached string, POV, or height fields; failures remain explicit", true);
            CACombatTopologyJson.Property(sb,
                "capacityEvictedIncidentCount",
                topologyCapacityEvictedIncidentCount, true);
            CACombatTopologyJson.Property(sb, "observationFailureCount",
                topologyObservationFailureCount, true);
            CACombatTopologyJson.Property(sb,
                "lastObservationFailureTick",
                topologyLastObservationFailureTick, true);
            CACombatTopologyJson.Property(sb, "lastObservationFailure",
                topologyLastObservationFailure, true);
            CACombatTopologyJson.Property(sb, "incidentLimit",
                MaxTopologyIncidents, true);
            CACombatTopologyJson.Property(sb, "dynamicIncidentByteLimit",
                MaxTopologyIncidentBytes, true);
            CACombatTopologyJson.Property(sb, "dynamicHistoryByteLimit",
                MaxTopologyHistoryBytes, true);
            CACombatTopologyJson.Property(sb,
                "battlefieldReferenceChangeLimit",
                MaxBattlefieldReferenceChanges, true);
            CACombatTopologyJson.Property(sb,
                "battlefieldReferenceChangeByteLimit",
                MaxBattlefieldReferenceChangeBytes, true);
            CACombatTopologyJson.Property(sb, "historyEstimatedBytes",
                TopologyHistoryBytes(), true);
            CACombatTopologyJson.Property(sb,
                "dynamicHistoryEstimatedBytes",
                TopologyDynamicHistoryBytes(), true);
            CACombatTopologyJson.Property(sb,
                "battlefieldReferenceEstimatedBytes",
                TopologyReferenceHistoryBytes(), true);
            CACombatTopologyJson.Property(sb,
                "battlefieldReferenceBaselineEstimatedBytes",
                TopologyReferenceBaselineHistoryBytes(), true);
            CACombatTopologyJson.Property(sb,
                "battlefieldReferenceChangeEstimatedBytes",
                TopologyReferenceChangeHistoryBytes(), true);
            CACombatTopologyJson.Property(sb,
                "baselinePreservationPolicy",
                "immutable full-map battlefield references and pawn-proximal dynamic baselines are preserved separately from dynamic evidence limits; baselineExceededByteTarget is reported but does not suppress later evidence", true);
            CACombatTopologyJson.Name(sb, "incidents", true);
            sb.Append('[');
            for (int i = 0; i < topologyIncidents.Count; i++)
            {
                if (i > 0) sb.Append(',');
                CACombatTopologyJson.AppendIncident(sb,
                    topologyIncidents[i]);
            }
            sb.Append(']');
            CACombatTopologyJson.Name(sb, "events", true);
            sb.Append('[');
            for (int i = 0; i < events.Count; i++)
            {
                if (i > 0) sb.Append(',');
                CACombatTopologyJson.AppendEvent(sb, events[i]);
            }
            sb.Append(']');
            sb.Append('}');
            return sb.ToString();
        }

        private int TopologyReferenceHistoryBytes()
        {
            int total = 0;
            for (int i = 0; i < topologyIncidents.Count; i++)
                total += topologyIncidents[i]?.battlefieldReference
                    ?.EstimateBytes() ?? 0;
            return total;
        }

        private int TopologyReferenceBaselineHistoryBytes()
        {
            int total = 0;
            for (int i = 0; i < topologyIncidents.Count; i++)
                total += topologyIncidents[i]?.battlefieldReference
                    ?.baselineEstimatedBytes ?? 0;
            return total;
        }

        private int TopologyReferenceChangeHistoryBytes()
        {
            int total = 0;
            for (int i = 0; i < topologyIncidents.Count; i++)
                total += topologyIncidents[i]?.battlefieldReference
                    ?.ChangeEstimatedBytes() ?? 0;
            return total;
        }

        private List<CACombatTopologyExportEvent> CollectTopologyEvents()
        {
            var result = new List<CACombatTopologyExportEvent>();
            var seen = new HashSet<int>();
            BattleLog log = game?.battleLog;
            if (log?.Battles == null) return result;
            for (int i = 0; i < log.Battles.Count; i++)
            {
                Battle battle = log.Battles[i];
                if (battle?.Entries == null) continue;
                string battleId = battle.GetUniqueLoadID();
                for (int j = 0; j < battle.Entries.Count; j++)
                {
                    LogEntry entry = battle.Entries[j];
                    if (entry == null || seen.Contains(entry.LogID)) continue;
                    CACombatSpatialLogRecord record;
                    if (!records.TryGetValue(entry.LogID, out record)
                        || record == null
                        || record.topologyIncidentId.NullOrEmpty())
                        continue;
                    seen.Add(entry.LogID);
                    result.Add(CACombatTopologyExportEvent.Capture(entry,
                        record, battleId));
                }
            }
            result.Sort(CACombatTopologyExportEvent.Compare);
            return result;
        }

        internal static void RecordNonBuildingDamage(Thing thing,
            int previousHitPoints)
        {
            if (thing == null || thing is Building || !thing.Spawned) return;
            if (!thing.def.useHitPoints || previousHitPoints == thing.HitPoints)
                return;
            CurrentComponent()?.RecordThingChange(thing.Map, thing,
                "nonbuilding-hit-points-changed",
                previousHitPoints.ToString(CultureInfo.InvariantCulture)
                    + "->" + thing.HitPoints.ToString(
                        CultureInfo.InvariantCulture));
        }

        internal static void RecordFactionChange(Thing thing,
            string previousFaction)
        {
            if (thing == null || !thing.Spawned) return;
            string current = thing.Faction?.GetUniqueLoadID();
            if (string.Equals(previousFaction, current,
                StringComparison.Ordinal)) return;
            CurrentComponent()?.RecordThingChange(thing.Map, thing,
                "thing-faction-changed", (previousFaction ?? "null")
                    + "->" + (current ?? "null"));
        }

        internal static void RecordPollutionChange(Map map, IntVec3 cell,
            bool previous)
        {
            if (map == null || !cell.InBounds(map)) return;
            bool current = map.pollutionGrid.IsPolluted(cell);
            if (previous == current) return;
            CurrentComponent()?.RecordCellChange(map, cell,
                "pollution-changed", previous + "->" + current);
        }

        internal static void RecordFireSizeChange(Fire fire, float previous)
        {
            if (fire == null || !fire.Spawned || fire.fireSize == previous)
                return;
            CurrentComponent()?.RecordThingChange(fire.Map, fire,
                "fire-size-changed", previous.ToString("R",
                    CultureInfo.InvariantCulture) + "->" + fire.fireSize
                    .ToString("R", CultureInfo.InvariantCulture));
        }

    }

    internal sealed class CACombatTopologyExportEvent
    {
        private static readonly Dictionary<Type, MethodInfo> RenderWorkers =
            new Dictionary<Type, MethodInfo>();
        internal int logId;
        internal int tick;
        internal string topologyIncidentId;
        internal string nativeBattleId;
        internal string runtimeClass;
        internal string defName;
        internal string displayText;
        internal string displayTextFailure;
        internal CACombatSpatialLogRecord spatial;

        internal static CACombatTopologyExportEvent Capture(LogEntry entry,
            CACombatSpatialLogRecord spatial, string battleId)
        {
            string displayFailure;
            return new CACombatTopologyExportEvent
            {
                logId = entry.LogID,
                tick = entry.Tick,
                topologyIncidentId = spatial.topologyIncidentId,
                nativeBattleId = battleId,
                runtimeClass = entry.GetType().FullName,
                defName = entry.def?.defName,
                displayText = RenderWithoutCacheMutation(entry,
                    out displayFailure),
                displayTextFailure = displayFailure,
                spatial = spatial
            };
        }

        private static string RenderWithoutCacheMutation(LogEntry entry,
            out string failure)
        {
            failure = null;
            if (entry == null) return null;
            try
            {
                Type type = entry.GetType();
                MethodInfo worker;
                lock (RenderWorkers)
                {
                    if (!RenderWorkers.TryGetValue(type, out worker))
                    {
                        worker = type.GetMethod("ToGameStringFromPOV_Worker",
                            BindingFlags.Instance | BindingFlags.NonPublic,
                            null, new[] { typeof(Thing), typeof(bool) }, null);
                        RenderWorkers[type] = worker;
                    }
                }
                if (worker == null)
                {
                    failure = "protected renderer not found";
                    return null;
                }
                Rand.PushState();
                try
                {
                    Rand.Seed = entry.LogID;
                    return worker.Invoke(entry, new object[] { null, false })
                        as string;
                }
                finally
                {
                    Rand.PopState();
                }
            }
            catch (Exception exception)
            {
                Exception root = exception.GetBaseException();
                failure = root.GetType().Name + ": " + root.Message;
                return null;
            }
        }

        internal static int Compare(CACombatTopologyExportEvent a,
            CACombatTopologyExportEvent b)
        {
            int compare = a.tick.CompareTo(b.tick);
            return compare != 0 ? compare : a.logId.CompareTo(b.logId);
        }
    }

    internal static class CACombatTopologyJson
    {
        internal static void AppendIncident(StringBuilder sb,
            CACombatTopologyIncident incident)
        {
            if (incident == null)
            {
                sb.Append("null");
                return;
            }
            int cellCount;
            List<CACombatTopologyCellRun> runs =
                CACombatTopologyCapture.Decode(incident.cellPayload,
                    out cellCount);
            int baselineKnownCellCount = 0;
            for (int i = 0; i < runs.Count; i++)
                if ((runs[i].signature.flags & 128) != 0)
                    baselineKnownCellCount += runs[i].count;
            sb.Append('{');
            Property(sb, "id", incident.incidentId, true);
            Property(sb, "mapId", incident.mapId, true);
            Property(sb, "width", incident.width, true);
            Property(sb, "height", incident.height, true);
            Property(sb, "baselineTriggerLogId", incident.firstLogId, true);
            Property(sb, "baselineTriggerTick", incident.firstLogTick, true);
            Property(sb, "lastLogTick", incident.lastLogTick, true);
            Property(sb, "baselineCapturedTick",
                incident.baselineCapturedTick, true);
            Property(sb, "baselineTiming", incident.baselineTiming, true);
            Property(sb, "estimatedBytes", incident.EstimateBytes(), true);
            Property(sb, "baselineEstimatedBytes",
                incident.baselineEstimatedBytes, true);
            Property(sb, "baselineExceedsDynamicIncidentLimitForComparison",
                incident.baselineExceededByteTarget, true);
            Property(sb, "dynamicEstimatedBytes",
                incident.DynamicEstimatedBytes(), true);
            Property(sb, "observationRadius",
                incident.observationRadius, true);
            Property(sb, "knownCellCount", incident.knownCellCount, true);
            Property(sb, "unknownCellCount", Math.Max(0,
                incident.width * incident.height
                    - incident.knownCellCount), true);
            Name(sb, "battlefieldReference", true);
            AppendBattlefieldReference(sb, incident.battlefieldReference);
            Property(sb, "truncated", incident.truncated, true);
            Property(sb, "truncatedAtTick", incident.truncatedAtTick, true);
            Property(sb, "truncationReason", incident.truncationReason, true);
            Property(sb, "observationFailureCount",
                incident.observationFailureCount, true);
            Property(sb, "lastObservationFailureTick",
                incident.lastObservationFailureTick, true);
            Property(sb, "lastObservationFailure",
                incident.lastObservationFailure, true);
            AppendStringArray(sb, "nativeBattleAliases",
                incident.nativeBattleAliases, true);
            AppendStringArray(sb, "observerPawnIds",
                incident.observerPawnIds, true);
            AppendIntArray(sb, "retainedLogIds", incident.logIds, true);
            Property(sb, "omittedLogIdCount",
                incident.omittedLogIdCount, true);
            Property(sb, "firstOmittedLogId",
                incident.firstOmittedLogId, true);
            Property(sb, "lastOmittedLogId",
                incident.lastOmittedLogId, true);
            AppendStringArray(sb, "terrainPalette",
                incident.terrainPalette, true);
            AppendStringArray(sb, "roofPalette", incident.roofPalette, true);
            Name(sb, "cellBaseline", true);
            sb.Append('{');
            Property(sb, "format", incident.cellFormat, true);
            Property(sb, "sha256", incident.cellPayloadSha256, true);
            Property(sb, "payloadBytes", incident.cellPayload?.Length ?? 0,
                true);
            Property(sb, "cellCount", cellCount, true);
            Property(sb, "knownCellCount", baselineKnownCellCount, true);
            Property(sb, "unknownCellCount", Math.Max(0,
                cellCount - baselineKnownCellCount), true);
            Name(sb, "runs", true);
            sb.Append('[');
            for (int i = 0; i < runs.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendRun(sb, runs[i]);
            }
            sb.Append(']');
            sb.Append('}');
            Name(sb, "mapArtifacts", true);
            sb.Append('[');
            for (int i = 0; i < incident.baselineThings.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendThing(sb, incident.baselineThings[i]);
            }
            sb.Append(']');
            Name(sb, "deltas", true);
            sb.Append('[');
            for (int i = 0; i < incident.deltas.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendDelta(sb, incident.deltas[i]);
            }
            sb.Append(']');
            Name(sb, "hazardSamples", true);
            sb.Append('[');
            for (int i = 0; i < incident.hazardSamples.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendHazardSample(sb, incident.hazardSamples[i]);
            }
            sb.Append(']');
            sb.Append('}');
        }

        private static void AppendBattlefieldReference(StringBuilder sb,
            CACombatTopologyBattlefieldReference reference)
        {
            sb.Append('{');
            if (reference == null)
            {
                Property(sb, "status", "legacy-unavailable", false);
                sb.Append('}');
                return;
            }
            int cellCount;
            List<CACombatTopologyReferenceCellRun> runs =
                CACombatTopologyCapture.DecodeReference(
                    reference.cellPayload, out cellCount);
            Property(sb, "status", "captured", true);
            Property(sb, "capturedTick", reference.capturedTick, true);
            Property(sb, "captureTiming", reference.captureTiming, true);
            Property(sb, "estimatedBytes", reference.EstimateBytes(), true);
            Property(sb, "baselineEstimatedBytes",
                reference.baselineEstimatedBytes, true);
            Property(sb, "changeEstimatedBytes",
                reference.ChangeEstimatedBytes(), true);
            Property(sb, "changesTruncated",
                reference.changesTruncated, true);
            Property(sb, "changesTruncatedAtTick",
                reference.changesTruncatedAtTick, true);
            Property(sb, "changesTruncationReason",
                reference.changesTruncationReason, true);
            AppendStringArray(sb, "terrainPalette",
                reference.terrainPalette, true);
            AppendStringArray(sb, "roofPalette",
                reference.roofPalette, true);
            Name(sb, "cellReference", true);
            sb.Append('{');
            Property(sb, "format", reference.cellFormat, true);
            Property(sb, "sha256", reference.cellPayloadSha256, true);
            Property(sb, "payloadBytes",
                reference.cellPayload?.Length ?? 0, true);
            Property(sb, "cellCount", cellCount, true);
            Name(sb, "runs", true);
            sb.Append('[');
            for (int i = 0; i < runs.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendReferenceRun(sb, runs[i]);
            }
            sb.Append(']');
            sb.Append('}');
            Name(sb, "artifacts", true);
            sb.Append('[');
            for (int i = 0; i < reference.artifacts.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendReferenceArtifact(sb, reference.artifacts[i]);
            }
            sb.Append(']');
            Name(sb, "changes", true);
            sb.Append('[');
            for (int i = 0; i < reference.changes.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendReferenceChange(sb, reference.changes[i]);
            }
            sb.Append(']');
            sb.Append('}');
        }

        internal static void AppendEvent(StringBuilder sb,
            CACombatTopologyExportEvent item)
        {
            sb.Append('{');
            Property(sb, "logId", item.logId, true);
            Property(sb, "tick", item.tick, true);
            Property(sb, "topologyIncidentId", item.topologyIncidentId, true);
            Property(sb, "nativeBattleId", item.nativeBattleId, true);
            Property(sb, "runtimeClass", item.runtimeClass, true);
            Property(sb, "defName", item.defName, true);
            Property(sb, "displayText", item.displayText, true);
            Property(sb, "displayTextFailure",
                item.displayTextFailure, true);
            Property(sb, "mapId", item.spatial.mapId, true);
            Property(sb, "hasBulletImpact",
                item.spatial.hasBulletImpact, true);
            if (item.spatial.hasBulletImpact)
            {
                Name(sb, "bulletImpact", true);
                sb.Append('{');
                Property(sb, "mapId", item.spatial.impactMapId, true);
                Property(sb, "x", item.spatial.impactCell.x, true);
                Property(sb, "z", item.spatial.impactCell.z, false);
                sb.Append('}');
            }
            else
            {
                Name(sb, "bulletImpact", true);
                sb.Append("null");
            }
            Name(sb, "concerns", true);
            sb.Append('[');
            for (int i = 0; i < item.spatial.concerns.Count; i++)
            {
                if (i > 0) sb.Append(',');
                CACombatSpatialConcernSnapshot concern =
                    item.spatial.concerns[i];
                if (concern == null)
                {
                    sb.Append("null");
                    continue;
                }
                sb.Append('{');
                Property(sb, "ordinal", concern.ordinal, true);
                Property(sb, "thingId", concern.thingId, true);
                Property(sb, "label", concern.label, true);
                Property(sb, "mapId", concern.mapId, true);
                Property(sb, "x", concern.cell.x, true);
                Property(sb, "z", concern.cell.z, false);
                sb.Append('}');
            }
            sb.Append(']');
            sb.Append('}');
        }

        private static void AppendRun(StringBuilder sb,
            CACombatTopologyCellRun run)
        {
            CACombatTopologyCellSignature value = run.signature;
            bool known = (value.flags & 128) != 0;
            sb.Append('{');
            Property(sb, "start", run.start, true);
            Property(sb, "count", run.count, true);
            Property(sb, "known", known, known);
            if (!known)
            {
                sb.Append('}');
                return;
            }
            Property(sb, "effectiveTerrain", value.effective, true);
            Property(sb, "topTerrain", value.top, true);
            Property(sb, "underTerrain", value.under, true);
            Property(sb, "roof", value.roof, true);
            Property(sb, "pathCost", value.pathCost, true);
            Property(sb, "flags", value.flags, true);
            Property(sb, "walkable", (value.flags & 1) != 0, true);
            Property(sb, "canBeSeenOver", (value.flags & 2) != 0, true);
            Property(sb, "polluted", (value.flags & 4) != 0, true);
            Property(sb, "water", (value.flags & 8) != 0, true);
            Property(sb, "naturalRoof", (value.flags & 16) != 0, true);
            Property(sb, "thickRoof", (value.flags & 32) != 0, true);
            Property(sb, "shoreline", (value.flags & 64) != 0, true);
            Property(sb, "packedGas", value.gas, true);
            AppendGasChannels(sb, value.gas);
            Property(sb, "edificeThingId", value.edifice, true);
            Property(sb, "coverThingId", value.cover, false);
            sb.Append('}');
        }

        private static void AppendReferenceRun(StringBuilder sb,
            CACombatTopologyReferenceCellRun run)
        {
            CACombatTopologyReferenceCellSignature value = run.signature;
            sb.Append('{');
            Property(sb, "start", run.start, true);
            Property(sb, "count", run.count, true);
            Property(sb, "effectiveTerrain", value.effective, true);
            Property(sb, "topTerrain", value.top, true);
            Property(sb, "underTerrain", value.under, true);
            Property(sb, "roof", value.roof, true);
            Property(sb, "flags", value.flags, true);
            Property(sb, "water", (value.flags & 1) != 0, true);
            Property(sb, "shoreline", (value.flags & 2) != 0, true);
            Property(sb, "naturalRoof", (value.flags & 4) != 0, true);
            Property(sb, "thickRoof", (value.flags & 8) != 0, false);
            sb.Append('}');
        }

        private static void AppendReferenceArtifact(StringBuilder sb,
            CACombatTopologyReferenceArtifactState artifact)
        {
            if (artifact == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append('{');
            Property(sb, "thingId", artifact.thingId, true);
            Property(sb, "thingIdNumber", artifact.thingIdNumber, true);
            Property(sb, "defName", artifact.defName, true);
            Property(sb, "stuffDefName", artifact.stuffDefName, true);
            Property(sb, "runtimeClass", artifact.runtimeClass, true);
            Property(sb, "category", artifact.category, true);
            Property(sb, "thingCategories", artifact.thingCategories, true);
            Property(sb, "x", artifact.x, true);
            Property(sb, "z", artifact.z, true);
            Property(sb, "rotation", artifact.rotation, true);
            Name(sb, "occupiedRect", true);
            sb.Append('{');
            Property(sb, "minX", artifact.minX, true);
            Property(sb, "minZ", artifact.minZ, true);
            Property(sb, "maxX", artifact.maxX, true);
            Property(sb, "maxZ", artifact.maxZ, false);
            sb.Append('}');
            Property(sb, "passability", artifact.passability, true);
            Property(sb, "fillCategory", artifact.fillCategory, true);
            Property(sb, "fillPercent", artifact.fillPercent, true);
            Property(sb, "baseBlockChance",
                artifact.baseBlockChance, true);
            Property(sb, "building", artifact.building, true);
            Property(sb, "wall", artifact.wall, true);
            Property(sb, "naturalRock", artifact.naturalRock, true);
            Property(sb, "fence", artifact.fence, true);
            Property(sb, "door", artifact.door, true);
            Property(sb, "turret", artifact.turret, true);
            Property(sb, "chunk", artifact.chunk, true);
            Property(sb, "affectsRegions", artifact.affectsRegions, true);
            Property(sb, "affectsReachability",
                artifact.affectsReachability, false);
            sb.Append('}');
        }

        private static void AppendReferenceCell(StringBuilder sb,
            CACombatTopologyReferenceCellState cell)
        {
            if (cell == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append('{');
            Property(sb, "index", cell.index, true);
            Property(sb, "x", cell.x, true);
            Property(sb, "z", cell.z, true);
            Property(sb, "effectiveTerrain", cell.effectiveTerrain, true);
            Property(sb, "topTerrain", cell.topTerrain, true);
            Property(sb, "underTerrain", cell.underTerrain, true);
            Property(sb, "roof", cell.roof, true);
            Property(sb, "water", cell.water, true);
            Property(sb, "shoreline", cell.shoreline, true);
            Property(sb, "naturalRoof", cell.naturalRoof, true);
            Property(sb, "thickRoof", cell.thickRoof, false);
            sb.Append('}');
        }

        private static void AppendReferenceChange(StringBuilder sb,
            CACombatTopologyReferenceChange change)
        {
            if (change == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append('{');
            Property(sb, "tick", change.tick, true);
            Property(sb, "sequence", change.sequence, true);
            Property(sb, "kind", change.kind, true);
            Property(sb, "detail", change.detail, true);
            Property(sb, "mapId", change.mapId, true);
            Name(sb, "cell", true);
            AppendReferenceCell(sb, change.cell);
            Name(sb, "artifact", true);
            AppendReferenceArtifact(sb, change.artifact);
            sb.Append('}');
        }

        private static void AppendThing(StringBuilder sb,
            CACombatTopologyThingState thing)
        {
            if (thing == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append('{');
            Property(sb, "thingId", thing.thingId, true);
            Property(sb, "thingIdNumber", thing.thingIdNumber, true);
            Property(sb, "defName", thing.defName, true);
            Property(sb, "stuffDefName", thing.stuffDefName, true);
            Property(sb, "label", thing.label, true);
            Property(sb, "runtimeClass", thing.runtimeClass, true);
            Property(sb, "category", thing.category, true);
            Property(sb, "thingCategories", thing.thingCategories, true);
            Property(sb, "x", thing.x, true);
            Property(sb, "z", thing.z, true);
            Property(sb, "rotation", thing.rotation, true);
            Name(sb, "occupiedRect", true);
            sb.Append('{');
            Property(sb, "minX", thing.minX, true);
            Property(sb, "minZ", thing.minZ, true);
            Property(sb, "maxX", thing.maxX, true);
            Property(sb, "maxZ", thing.maxZ, false);
            sb.Append('}');
            Property(sb, "hitPoints", thing.hitPoints, true);
            Property(sb, "maxHitPoints", thing.maxHitPoints, true);
            Property(sb, "factionId", thing.factionId, true);
            Property(sb, "factionName", thing.factionName, true);
            Property(sb, "passability", thing.passability, true);
            Property(sb, "pathCost", thing.pathCost, true);
            Property(sb, "fillCategory", thing.fillCategory, true);
            Property(sb, "fillPercent", thing.fillPercent, true);
            Property(sb, "baseBlockChance", thing.baseBlockChance, true);
            Property(sb, "edifice", thing.edifice, true);
            Property(sb, "wall", thing.wall, true);
            Property(sb, "naturalRock", thing.naturalRock, true);
            Property(sb, "fence", thing.fence, true);
            Property(sb, "door", thing.door, true);
            Property(sb, "doorOpen", thing.doorOpen, true);
            Property(sb, "doorHoldOpen", thing.doorHoldOpen, true);
            Property(sb, "turret", thing.turret, true);
            Property(sb, "chunk", thing.chunk, true);
            Property(sb, "plant", thing.plant, true);
            Property(sb, "fire", thing.fire, true);
            Property(sb, "fireSize", thing.fireSize, true);
            Property(sb, "affectsRegions", thing.affectsRegions, true);
            Property(sb, "affectsReachability",
                thing.affectsReachability, true);
            Property(sb, "blocksSight", thing.blocksSight, false);
            sb.Append('}');
        }

        private static void AppendCell(StringBuilder sb,
            CACombatTopologyCellState cell)
        {
            if (cell == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append('{');
            Property(sb, "index", cell.index, true);
            Property(sb, "x", cell.x, true);
            Property(sb, "z", cell.z, true);
            Property(sb, "effectiveTerrain", cell.effectiveTerrain, true);
            Property(sb, "topTerrain", cell.topTerrain, true);
            Property(sb, "underTerrain", cell.underTerrain, true);
            Property(sb, "roof", cell.roof, true);
            Property(sb, "pathCost", cell.pathCost, true);
            Property(sb, "walkable", cell.walkable, true);
            Property(sb, "canBeSeenOver", cell.canBeSeenOver, true);
            Property(sb, "polluted", cell.polluted, true);
            Property(sb, "water", cell.water, true);
            Property(sb, "shoreline", cell.shoreline, true);
            Property(sb, "naturalRoof", cell.naturalRoof, true);
            Property(sb, "thickRoof", cell.thickRoof, true);
            Property(sb, "packedGas", cell.packedGas, true);
            AppendGasChannels(sb, cell.packedGas);
            Property(sb, "edificeThingId", cell.edificeThingId, true);
            Property(sb, "coverThingId", cell.coverThingId, false);
            sb.Append('}');
        }

        private static void AppendDelta(StringBuilder sb,
            CACombatTopologyDelta delta)
        {
            if (delta == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append('{');
            Property(sb, "tick", delta.tick, true);
            Property(sb, "sequence", delta.sequence, true);
            Property(sb, "kind", delta.kind, true);
            Property(sb, "detail", delta.detail, true);
            Property(sb, "mapId", delta.mapId, true);
            Property(sb, "observationRadius",
                delta.observationRadius, true);
            Property(sb, "observerCenterCount",
                delta.observerCenterCount, true);
            Property(sb, "newlyKnownCellCount",
                delta.newlyKnownCellCount, true);
            Property(sb, "weaponSensorBurst",
                delta.weaponSensorBurst, true);
            Name(sb, "cells", true);
            sb.Append('[');
            for (int i = 0; i < delta.cells.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendCell(sb, delta.cells[i]);
            }
            sb.Append(']');
            Name(sb, "thing", true);
            AppendThing(sb, delta.thing);
            Name(sb, "things", true);
            sb.Append('[');
            for (int i = 0; i < delta.things.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendThing(sb, delta.things[i]);
            }
            sb.Append(']');
            sb.Append('}');
        }

        private static void AppendHazardSample(StringBuilder sb,
            CACombatTopologyHazardSample sample)
        {
            if (sample == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append('{');
            Property(sb, "logId", sample.logId, true);
            Property(sb, "tick", sample.tick, true);
            Property(sb, "pawnObservationRadius",
                sample.pawnObservationRadius, true);
            Property(sb, "weaponSensorBurstRadius",
                sample.weaponSensorBurstRadius, true);
            Property(sb, "includesLineOfSightCorridors",
                sample.includesLineOfSightCorridors, true);
            Property(sb, "weaponSensorBurst",
                sample.weaponSensorBurst, true);
            Property(sb, "candidateCellCount",
                sample.candidateCellCount, true);
            Property(sb, "candidateCoverageTruncated",
                sample.candidateCoverageTruncated, true);
            AppendIntArray(sb, "centerCellIndices",
                sample.centerCellIndices, true);
            Name(sb, "nonzeroCells", true);
            sb.Append('[');
            for (int i = 0; i < sample.nonzeroCells.Count; i++)
            {
                if (i > 0) sb.Append(',');
                CACombatTopologyHazardCell cell = sample.nonzeroCells[i];
                sb.Append('{');
                Property(sb, "index", cell.index, true);
                Property(sb, "x", cell.x, true);
                Property(sb, "z", cell.z, true);
                Property(sb, "packedGas", cell.packedGas, true);
                AppendGasChannels(sb, cell.packedGas);
                Property(sb, "polluted", cell.polluted, true);
                Property(sb, "fires", cell.fires, false);
                sb.Append('}');
            }
            sb.Append(']');
            sb.Append('}');
        }

        private static void AppendGasChannels(StringBuilder sb, uint packed)
        {
            Property(sb, "blindSmoke", (byte)(packed & 0xFFu), true);
            Property(sb, "toxicGas", (byte)((packed >> 8) & 0xFFu), true);
            Property(sb, "rotStink", (byte)((packed >> 16) & 0xFFu), true);
            Property(sb, "deadlifeDust", (byte)((packed >> 24) & 0xFFu),
                true);
        }

        private static void AppendStringArray(StringBuilder sb, string name,
            List<string> values, bool comma)
        {
            Name(sb, name, comma);
            sb.Append('[');
            if (values != null)
                for (int i = 0; i < values.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    String(sb, values[i]);
                }
            sb.Append(']');
        }

        private static void AppendIntArray(StringBuilder sb, string name,
            List<int> values, bool comma)
        {
            Name(sb, name, comma);
            sb.Append('[');
            if (values != null)
                for (int i = 0; i < values.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(values[i].ToString(CultureInfo.InvariantCulture));
                }
            sb.Append(']');
        }

        internal static void Property(StringBuilder sb, string name,
            string value, bool comma)
        {
            Name(sb, name, comma);
            String(sb, value);
        }

        internal static void Property(StringBuilder sb, string name,
            int value, bool comma)
        {
            Name(sb, name, comma);
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        internal static void Property(StringBuilder sb, string name,
            ushort value, bool comma)
        {
            Name(sb, name, comma);
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        internal static void Property(StringBuilder sb, string name,
            byte value, bool comma)
        {
            Name(sb, name, comma);
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        internal static void Property(StringBuilder sb, string name,
            uint value, bool comma)
        {
            Name(sb, name, comma);
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        internal static void Property(StringBuilder sb, string name,
            float value, bool comma)
        {
            Name(sb, name, comma);
            sb.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        internal static void Property(StringBuilder sb, string name,
            bool value, bool comma)
        {
            Name(sb, name, comma);
            sb.Append(value ? "true" : "false");
        }

        internal static void Name(StringBuilder sb, string name, bool comma)
        {
            // The caller's legacy boolean documents whether another property
            // follows. Placement is derived from the object boundary so the
            // first property can never acquire a leading comma.
            if (sb.Length > 0 && sb[sb.Length - 1] != '{') sb.Append(',');
            String(sb, name);
            sb.Append(':');
        }

        private static void String(StringBuilder sb, string value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("X4",
                                CultureInfo.InvariantCulture));
                        }
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }

    internal static class CACombatTopologyOperationContext
    {
        [ThreadStatic] private static List<Operation> despawns;
        [ThreadStatic] private static List<Operation> destroys;

        private sealed class Operation
        {
            internal Thing thing;
            internal DestroyMode mode;
        }

        internal static void BeginDespawn(Thing thing, DestroyMode mode)
        {
            if (despawns == null) despawns = new List<Operation>();
            despawns.Add(new Operation { thing = thing, mode = mode });
        }

        internal static void EndDespawn(Thing thing)
        {
            RemoveLast(despawns, thing);
        }

        internal static void BeginDestroy(Thing thing, DestroyMode mode)
        {
            if (destroys == null) destroys = new List<Operation>();
            destroys.Add(new Operation { thing = thing, mode = mode });
        }

        internal static void EndDestroy(Thing thing)
        {
            RemoveLast(destroys, thing);
        }

        internal static string DespawnDetail(Thing thing)
        {
            Operation destroy = FindLast(destroys, thing);
            if (destroy != null) return "destroy:" + destroy.mode;
            Operation despawn = FindLast(despawns, thing);
            return despawn != null ? "despawn:" + despawn.mode
                : "despawn:mode-unavailable";
        }

        private static Operation FindLast(List<Operation> operations,
            Thing thing)
        {
            if (operations == null) return null;
            for (int i = operations.Count - 1; i >= 0; i--)
                if (ReferenceEquals(operations[i].thing, thing))
                    return operations[i];
            return null;
        }

        private static void RemoveLast(List<Operation> operations,
            Thing thing)
        {
            if (operations == null) return;
            for (int i = operations.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(operations[i].thing, thing)) continue;
                operations.RemoveAt(i);
                return;
            }
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
    internal static class Patch_CACombatTopologyDespawnContext
    {
        [HarmonyPrefix]
        internal static void Prefix(Thing __instance, DestroyMode mode)
        {
            CACombatTopologyOperationContext.BeginDespawn(__instance, mode);
        }

        [HarmonyFinalizer]
        internal static Exception Finalizer(Thing __instance,
            Exception __exception)
        {
            CACombatTopologyOperationContext.EndDespawn(__instance);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
    internal static class Patch_CACombatTopologyDestroyContext
    {
        [HarmonyPrefix]
        internal static void Prefix(Thing __instance, DestroyMode mode)
        {
            CACombatTopologyOperationContext.BeginDestroy(__instance, mode);
        }

        [HarmonyFinalizer]
        internal static Exception Finalizer(Thing __instance,
            Exception __exception)
        {
            CACombatTopologyOperationContext.EndDestroy(__instance);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    internal static class Patch_CACombatTopologyNonBuildingDamage
    {
        internal struct DamageState
        {
            internal int hitPoints;
            internal float fireSize;
        }

        [HarmonyPrefix]
        internal static void Prefix(Thing __instance, out DamageState __state)
        {
            __state = new DamageState
            {
                hitPoints = __instance != null
                    && __instance.def.useHitPoints
                    ? __instance.HitPoints : int.MinValue,
                fireSize = __instance is Fire fire ? fire.fireSize : -1f
            };
        }

        [HarmonyPostfix]
        internal static void Postfix(Thing __instance, DamageState __state)
        {
            try
            {
                CACombatSpatialLogComponent.RecordNonBuildingDamage(__instance,
                    __state.hitPoints);
                if (__instance is Fire fire)
                    CACombatSpatialLogComponent.RecordFireSizeChange(fire,
                        __state.fireSize);
            }
            catch
            {
                // Damage resolution is never coupled to observation.
                CACombatSpatialLogComponent.ReportTopologyObservationFailure(
                    __instance?.MapHeld?.uniqueID ?? -1,
                    "nonbuilding-damage-postfix");
            }
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.SetFaction))]
    internal static class Patch_CACombatTopologyFactionChange
    {
        [HarmonyPrefix]
        internal static void Prefix(Thing __instance, out string __state)
        {
            __state = __instance?.Faction?.GetUniqueLoadID();
        }

        [HarmonyPostfix]
        internal static void Postfix(Thing __instance, string __state)
        {
            try
            {
                CACombatSpatialLogComponent.RecordFactionChange(__instance,
                    __state);
            }
            catch
            {
                // Faction changes are never coupled to observation.
                CACombatSpatialLogComponent.ReportTopologyObservationFailure(
                    __instance?.MapHeld?.uniqueID ?? -1,
                    "faction-change-postfix");
            }
        }
    }

    [HarmonyPatch(typeof(PollutionGrid), nameof(PollutionGrid.SetPolluted))]
    internal static class Patch_CACombatTopologyPollutionChange
    {
        [HarmonyPrefix]
        internal static void Prefix(PollutionGrid __instance, IntVec3 cell,
            out bool __state)
        {
            __state = __instance != null && __instance.IsPolluted(cell);
        }

        [HarmonyPostfix]
        internal static void Postfix(Map ___map, IntVec3 cell, bool __state)
        {
            try
            {
                CACombatSpatialLogComponent.RecordPollutionChange(___map,
                    cell, __state);
            }
            catch
            {
                // Pollution changes are never coupled to observation.
                CACombatSpatialLogComponent.ReportTopologyObservationFailure(
                    ___map?.uniqueID ?? -1,
                    "pollution-change-postfix");
            }
        }
    }

    [HarmonyPatch(typeof(Fire), "DoComplexCalcs")]
    internal static class Patch_CACombatTopologyFireSize
    {
        [HarmonyPrefix]
        internal static void Prefix(Fire __instance, out float __state)
        {
            __state = __instance?.fireSize ?? -1f;
        }

        [HarmonyPostfix]
        internal static void Postfix(Fire __instance, float __state)
        {
            try
            {
                CACombatSpatialLogComponent.RecordFireSizeChange(__instance,
                    __state);
            }
            catch
            {
                // Fire simulation is never coupled to observation.
                CACombatSpatialLogComponent.ReportTopologyObservationFailure(
                    __instance?.MapHeld?.uniqueID ?? -1,
                    "fire-size-postfix");
            }
        }
    }
}
