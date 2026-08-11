using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    internal enum CAToxicWasteLifecycleState : byte
    {
        Observed,
        StagingBlocked,
        FrozenStagingReady,
        FrozenStaged,
        NativeAtomizerContained,
        Closed,
        ContainmentReady,
        ContainedWithThermalRisk,
        Contained
    }

    internal sealed class CAToxicWasteInventoryLotRecord : IExposable
    {
        public string thingId;
        public string holderId;
        public IntVec3 cell = IntVec3.Invalid;
        public int units;
        public bool spawned;
        public bool roofed;
        public bool frozen;
        public bool canDissolveNow;
        public bool inAtomizer;
        public bool inSelectedContainment;

        public void ExposeData()
        {
            Scribe_Values.Look(ref thingId, "thingId");
            Scribe_Values.Look(ref holderId, "holderId");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref units, "units", 0);
            Scribe_Values.Look(ref spawned, "spawned", false);
            Scribe_Values.Look(ref roofed, "roofed", false);
            Scribe_Values.Look(ref frozen, "frozen", false);
            Scribe_Values.Look(ref canDissolveNow, "canDissolveNow", false);
            Scribe_Values.Look(ref inAtomizer, "inAtomizer", false);
            // Retain the original save key so a2-88 checkpoints remain readable.
            Scribe_Values.Look(ref inSelectedContainment,
                "inSelectedStaging", false);
        }
    }

    internal sealed class CAToxicWasteInventoryRecord : IExposable
    {
        public int observedTick = -1;
        public int stacks;
        public int units;
        public int spawnedUnits;
        public int containedUnits;
        public int atomizerUnits;
        public int playerForbiddenUnits;
        public List<CAToxicWasteInventoryLotRecord> lots =
            new List<CAToxicWasteInventoryLotRecord>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref observedTick, "observedTick", -1);
            Scribe_Values.Look(ref stacks, "stacks", 0);
            Scribe_Values.Look(ref units, "units", 0);
            Scribe_Values.Look(ref spawnedUnits, "spawnedUnits", 0);
            Scribe_Values.Look(ref containedUnits, "containedUnits", 0);
            Scribe_Values.Look(ref atomizerUnits, "atomizerUnits", 0);
            Scribe_Values.Look(ref playerForbiddenUnits,
                "playerForbiddenUnits", 0);
            Scribe_Collections.Look(ref lots, "lots", LookMode.Deep);
            if (lots == null)
                lots = new List<CAToxicWasteInventoryLotRecord>();
        }
    }

    internal sealed class CAToxicWasteStagingCandidateRecord : IExposable
    {
        public int programId;
        public string programLabel;
        public CASpacePurpose purpose;
        public int authoredCells;
        public int acceptedStorageCells;
        public int roofedStorageCells;
        public int reachableRoofedStorageCells;
        public int containmentCapacityUnits;
        public int freeContainmentCapacityUnits;
        public int reachableContainmentCapacityUnits;
        public int freeReachableContainmentCapacityUnits;
        public int containmentHaulValidStorageCells;
        public int containmentHaulValidCapacityUnits;
        public int freeContainmentHaulValidCapacityUnits;
        public int readyStorageCells;
        public int reachableReadyCells;
        public int readyCapacityUnits;
        public int freeReadyCapacityUnits;
        public int reachableReadyCapacityUnits;
        public int freeReachableReadyCapacityUnits;
        public int haulValidStorageCells;
        public int haulValidCapacityUnits;
        public int freeHaulValidCapacityUnits;
        public int reservedHaulCapacityUnits;
        public int eligibleHaulers;
        public int wasteUnitsInFootprint;
        public int wasteUnitsInContainmentStorage;
        public int wasteUnitsInReachableContainmentStorage;
        public int wasteUnitsInReadyStorage;
        public int wasteUnitsInReachableReadyStorage;
        public int coStoredUnits;
        public int beds;
        public int gridCells;
        public int nearestGrid = int.MaxValue;
        public int nearestHabitation = int.MaxValue;
        public int nearestDefenseProgram = int.MaxValue;
        public int nearestTurret = int.MaxValue;
        public float minimumContainmentTemperature = float.MaxValue;
        public float maximumContainmentTemperature = float.MinValue;
        public float minimumReadyTemperature = float.MaxValue;
        public float maximumReadyTemperature = float.MinValue;
        public List<IntVec3> authoredFootprint = new List<IntVec3>();
        public List<IntVec3> reachableContainmentCells = new List<IntVec3>();
        public List<IntVec3> containmentHaulValidCells = new List<IntVec3>();
        public List<IntVec3> readyCells = new List<IntVec3>();
        public List<IntVec3> haulValidCells = new List<IntVec3>();

        internal CASpaceProgram program;

        public void ExposeData()
        {
            Scribe_Values.Look(ref programId, "programId", 0);
            Scribe_Values.Look(ref programLabel, "programLabel");
            Scribe_Values.Look(ref purpose, "purpose", CASpacePurpose.None);
            Scribe_Values.Look(ref authoredCells, "authoredCells", 0);
            Scribe_Values.Look(ref acceptedStorageCells,
                "acceptedStorageCells", 0);
            Scribe_Values.Look(ref roofedStorageCells,
                "roofedStorageCells", 0);
            Scribe_Values.Look(ref reachableRoofedStorageCells,
                "reachableRoofedStorageCells", 0);
            Scribe_Values.Look(ref containmentCapacityUnits,
                "containmentCapacityUnits", 0);
            Scribe_Values.Look(ref freeContainmentCapacityUnits,
                "freeContainmentCapacityUnits", 0);
            Scribe_Values.Look(ref reachableContainmentCapacityUnits,
                "reachableContainmentCapacityUnits", 0);
            Scribe_Values.Look(ref freeReachableContainmentCapacityUnits,
                "freeReachableContainmentCapacityUnits", 0);
            Scribe_Values.Look(ref containmentHaulValidStorageCells,
                "containmentHaulValidStorageCells", 0);
            Scribe_Values.Look(ref containmentHaulValidCapacityUnits,
                "containmentHaulValidCapacityUnits", 0);
            Scribe_Values.Look(ref freeContainmentHaulValidCapacityUnits,
                "freeContainmentHaulValidCapacityUnits", 0);
            Scribe_Values.Look(ref readyStorageCells,
                "readyStorageCells", 0);
            Scribe_Values.Look(ref reachableReadyCells,
                "reachableReadyCells", 0);
            Scribe_Values.Look(ref readyCapacityUnits,
                "readyCapacityUnits", 0);
            Scribe_Values.Look(ref freeReadyCapacityUnits,
                "freeReadyCapacityUnits", 0);
            Scribe_Values.Look(ref reachableReadyCapacityUnits,
                "reachableReadyCapacityUnits", 0);
            Scribe_Values.Look(ref freeReachableReadyCapacityUnits,
                "freeReachableReadyCapacityUnits", 0);
            Scribe_Values.Look(ref haulValidStorageCells,
                "haulValidStorageCells", 0);
            Scribe_Values.Look(ref haulValidCapacityUnits,
                "haulValidCapacityUnits", 0);
            Scribe_Values.Look(ref freeHaulValidCapacityUnits,
                "freeHaulValidCapacityUnits", 0);
            Scribe_Values.Look(ref reservedHaulCapacityUnits,
                "reservedHaulCapacityUnits", 0);
            Scribe_Values.Look(ref eligibleHaulers,
                "eligibleHaulers", 0);
            Scribe_Values.Look(ref wasteUnitsInFootprint,
                "wasteUnitsInFootprint", 0);
            Scribe_Values.Look(ref wasteUnitsInContainmentStorage,
                "wasteUnitsInContainmentStorage", 0);
            Scribe_Values.Look(ref wasteUnitsInReachableContainmentStorage,
                "wasteUnitsInReachableContainmentStorage", 0);
            Scribe_Values.Look(ref wasteUnitsInReadyStorage,
                "wasteUnitsInReadyStorage", 0);
            Scribe_Values.Look(ref wasteUnitsInReachableReadyStorage,
                "wasteUnitsInReachableReadyStorage", 0);
            Scribe_Values.Look(ref coStoredUnits, "coStoredUnits", 0);
            Scribe_Values.Look(ref beds, "beds", 0);
            Scribe_Values.Look(ref gridCells, "gridCells", 0);
            Scribe_Values.Look(ref nearestGrid, "nearestGrid", int.MaxValue);
            Scribe_Values.Look(ref nearestHabitation,
                "nearestHabitation", int.MaxValue);
            Scribe_Values.Look(ref nearestDefenseProgram,
                "nearestDefenseProgram", int.MaxValue);
            Scribe_Values.Look(ref nearestTurret,
                "nearestTurret", int.MaxValue);
            Scribe_Values.Look(ref minimumContainmentTemperature,
                "minimumContainmentTemperature", float.MaxValue);
            Scribe_Values.Look(ref maximumContainmentTemperature,
                "maximumContainmentTemperature", float.MinValue);
            Scribe_Values.Look(ref minimumReadyTemperature,
                "minimumReadyTemperature", float.MaxValue);
            Scribe_Values.Look(ref maximumReadyTemperature,
                "maximumReadyTemperature", float.MinValue);
            Scribe_Collections.Look(ref authoredFootprint,
                "authoredFootprint", LookMode.Value);
            Scribe_Collections.Look(ref reachableContainmentCells,
                "reachableContainmentCells", LookMode.Value);
            Scribe_Collections.Look(ref containmentHaulValidCells,
                "containmentHaulValidCells", LookMode.Value);
            Scribe_Collections.Look(ref readyCells, "readyCells",
                LookMode.Value);
            Scribe_Collections.Look(ref haulValidCells, "haulValidCells",
                LookMode.Value);
            if (authoredFootprint == null)
                authoredFootprint = new List<IntVec3>();
            if (reachableContainmentCells == null)
                reachableContainmentCells = new List<IntVec3>();
            if (containmentHaulValidCells == null)
                containmentHaulValidCells = new List<IntVec3>();
            if (readyCells == null) readyCells = new List<IntVec3>();
            if (haulValidCells == null)
                haulValidCells = new List<IntVec3>();
        }

        internal bool IsContainmentReadyFor(int requiredUnits)
        {
            return requiredUnits > 0 && beds == 0
                && roofedStorageCells > 0
                && reachableRoofedStorageCells > 0
                && containmentHaulValidCapacityUnits >= requiredUnits;
        }

        internal bool IsFrozenReadyFor(int requiredUnits)
        {
            return requiredUnits > 0 && beds == 0
                && readyStorageCells > 0 && reachableReadyCells > 0
                && haulValidCapacityUnits >= requiredUnits;
        }

        internal string Receipt(int requiredUnits)
        {
            int deficit = Mathf.Max(0, requiredUnits
                - containmentHaulValidCapacityUnits);
            string containmentTemperature = roofedStorageCells == 0
                ? "none"
                : minimumContainmentTemperature.ToString("F1") + " to "
                    + maximumContainmentTemperature.ToString("F1") + " C";
            string frozenTemperature = readyStorageCells == 0
                ? "none"
                : minimumReadyTemperature.ToString("F1") + " to "
                    + maximumReadyTemperature.ToString("F1") + " C";
            return "    candidate program " + programLabel + " #"
                + programId + " (" + CASpacePurposeInfo.Label(purpose)
                + "): authored cells " + authoredCells
                + "; native Wastepack-accepting storage cells "
                + acceptedStorageCells + "; roofed containment cells "
                + roofedStorageCells + " (reachable "
                + reachableRoofedStorageCells + ", current temperature "
                + containmentTemperature + "); containment physical slot "
                + "capacity " + containmentCapacityUnits + " unit(s), free "
                + freeContainmentCapacityUnits + "; reachable containment "
                + "capacity " + reachableContainmentCapacityUnits + ", free "
                + freeReachableContainmentCapacityUnits
                + "; native containment-haul-valid cells "
                + containmentHaulValidStorageCells + ", capacity "
                + containmentHaulValidCapacityUnits + ", free "
                + freeContainmentHaulValidCapacityUnits + ", deficit "
                + deficit + "; thermal snapshot <= 0 C cells "
                + readyStorageCells + " (reachable "
                + reachableReadyCells + ", temperature " + frozenTemperature
                + "); frozen physical slot capacity "
                + readyCapacityUnits + " unit(s), free "
                + freeReadyCapacityUnits + "; reachable frozen "
                + "physical slot capacity " + reachableReadyCapacityUnits
                + ", free " + freeReachableReadyCapacityUnits
                + "; native frozen-haul-valid cells " + haulValidStorageCells
                + ", capacity " + haulValidCapacityUnits + ", free "
                + freeHaulValidCapacityUnits + ", actively reserved "
                + reservedHaulCapacityUnits + "; eligible Hauling pawns "
                + eligibleHaulers + "; waste in footprint "
                + wasteUnitsInFootprint + ", in roofed containment "
                + wasteUnitsInContainmentStorage + " (reachable "
                + wasteUnitsInReachableContainmentStorage
                + "), frozen in native storage "
                + wasteUnitsInReadyStorage + " (reachable "
                + wasteUnitsInReachableReadyStorage
                + "), co-stored non-waste units "
                + coStoredUnits + "; beds " + beds + "; grid cells "
                + gridCells + ", nearest grid " + Distance(nearestGrid)
                + "; habitation " + Distance(nearestHabitation)
                + "; Defense program " + Distance(nearestDefenseProgram)
                + "; turret " + Distance(nearestTurret)
                + " (proximity only); containment ready "
                + (IsContainmentReadyFor(requiredUnits) ? "yes" : "no")
                + "; frozen-ready snapshot "
                + (IsFrozenReadyFor(requiredUnits) ? "yes" : "no")
                + ". Temperature is dynamic evidence; cooling or freezing is "
                + "a capability-dependent tactic, not the containment gate.";
        }

        private static string Distance(int value)
        {
            return value == int.MaxValue ? "none observed"
                : value + " cell(s)";
        }
    }

    internal sealed class CAToxicWasteCapacityRecord : IExposable
    {
        public int requiredUnits;
        public int selectedProgramId;
        public int boundedCapacityUnits;
        public int freeCapacityUnits;
        public int deficitUnits;
        public List<CAToxicWasteStagingCandidateRecord> candidates =
            new List<CAToxicWasteStagingCandidateRecord>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref requiredUnits, "requiredUnits", 0);
            Scribe_Values.Look(ref selectedProgramId,
                "selectedProgramId", 0);
            Scribe_Values.Look(ref boundedCapacityUnits,
                "boundedCapacityUnits", 0);
            Scribe_Values.Look(ref freeCapacityUnits,
                "freeCapacityUnits", 0);
            Scribe_Values.Look(ref deficitUnits, "deficitUnits", 0);
            Scribe_Collections.Look(ref candidates, "candidates",
                LookMode.Deep);
            if (candidates == null)
                candidates = new List<CAToxicWasteStagingCandidateRecord>();
        }
    }

    internal sealed class CAToxicWasteFreezingRecord : IExposable
    {
        public float dissolutionThreshold = 0f;
        public int frozenUnits;
        public int canDissolveUnits;
        public int atomizerProtectedUnits;
        public int selectedStagingUnits;
        public int selectedStagingCapacityUnits;
        public string status;

        public void ExposeData()
        {
            Scribe_Values.Look(ref dissolutionThreshold,
                "dissolutionThreshold", 0f);
            Scribe_Values.Look(ref frozenUnits, "frozenUnits", 0);
            Scribe_Values.Look(ref canDissolveUnits,
                "canDissolveUnits", 0);
            Scribe_Values.Look(ref atomizerProtectedUnits,
                "atomizerProtectedUnits", 0);
            Scribe_Values.Look(ref selectedStagingUnits,
                "selectedStagingUnits", 0);
            Scribe_Values.Look(ref selectedStagingCapacityUnits,
                "selectedStagingCapacityUnits", 0);
            Scribe_Values.Look(ref status, "status");
        }
    }

    internal sealed class CAToxicWasteAuthorizedSourceRecord : IExposable
    {
        public string thingId;
        public int authorizedUnits;
        public int movedUnits;

        public int RemainingUnits => Mathf.Max(0,
            authorizedUnits - movedUnits);

        public void ExposeData()
        {
            Scribe_Values.Look(ref thingId, "thingId");
            Scribe_Values.Look(ref authorizedUnits, "authorizedUnits", 0);
            Scribe_Values.Look(ref movedUnits, "movedUnits", 0);
        }
    }

    internal sealed class CAToxicWasteRelocationRecord : IExposable
    {
        public int requiredUnits;
        public int alreadyAtDestinationUnits;
        public int reservedUnits;
        public int playerForbiddenUnits;
        public string operationId;
        public string authorizationId;
        public string authorizationOrigin;
        public bool authorizationActive;
        public int authorizedProgramId;
        public string authorizedProgramSignature;
        public int authorizedUnits;
        public int movedUnitsByAuthorizedJobs;
        public int authorizedTick = -1;
        public int revokedTick = -1;
        public int completedTick = -1;
        public int jobsStarted;
        public int jobsSucceeded;
        public int jobsFailed;
        public int activeJobLoadId = -1;
        public int lastJobStartedTick = -1;
        public int lastJobFinishedTick = -1;
        public string lastJobPawnId;
        public string lastJobThingId;
        public string lastCarriedThingId;
        public IntVec3 lastJobDestination = IntVec3.Invalid;
        public string lastJobCondition;
        public int lastReservedUnits;
        public int lastCarriedUnits;
        public bool lastSourceReservationObserved;
        public bool lastDestinationReservationObserved;
        public string status;
        public List<string> pendingThingIds = new List<string>();
        public List<string> succeededThingIds = new List<string>();
        public List<CAToxicWasteAuthorizedSourceRecord> authorizedSources =
            new List<CAToxicWasteAuthorizedSourceRecord>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref requiredUnits, "requiredUnits", 0);
            Scribe_Values.Look(ref alreadyAtDestinationUnits,
                "alreadyAtDestinationUnits", 0);
            Scribe_Values.Look(ref reservedUnits, "reservedUnits", 0);
            Scribe_Values.Look(ref playerForbiddenUnits,
                "playerForbiddenUnits", 0);
            Scribe_Values.Look(ref operationId, "operationId");
            Scribe_Values.Look(ref authorizationId, "authorizationId");
            Scribe_Values.Look(ref authorizationOrigin,
                "authorizationOrigin");
            Scribe_Values.Look(ref authorizationActive,
                "authorizationActive", false);
            Scribe_Values.Look(ref authorizedProgramId,
                "authorizedProgramId", 0);
            Scribe_Values.Look(ref authorizedProgramSignature,
                "authorizedProgramSignature");
            Scribe_Values.Look(ref authorizedUnits, "authorizedUnits", 0);
            Scribe_Values.Look(ref movedUnitsByAuthorizedJobs,
                "movedUnitsByAuthorizedJobs", 0);
            Scribe_Values.Look(ref authorizedTick, "authorizedTick", -1);
            Scribe_Values.Look(ref revokedTick, "revokedTick", -1);
            Scribe_Values.Look(ref completedTick, "completedTick", -1);
            Scribe_Values.Look(ref jobsStarted, "jobsStarted", 0);
            Scribe_Values.Look(ref jobsSucceeded, "jobsSucceeded", 0);
            Scribe_Values.Look(ref jobsFailed, "jobsFailed", 0);
            Scribe_Values.Look(ref activeJobLoadId,
                "activeJobLoadId", -1);
            Scribe_Values.Look(ref lastJobStartedTick,
                "lastJobStartedTick", -1);
            Scribe_Values.Look(ref lastJobFinishedTick,
                "lastJobFinishedTick", -1);
            Scribe_Values.Look(ref lastJobPawnId, "lastJobPawnId");
            Scribe_Values.Look(ref lastJobThingId, "lastJobThingId");
            Scribe_Values.Look(ref lastCarriedThingId,
                "lastCarriedThingId");
            Scribe_Values.Look(ref lastJobDestination,
                "lastJobDestination", IntVec3.Invalid);
            Scribe_Values.Look(ref lastJobCondition, "lastJobCondition");
            Scribe_Values.Look(ref lastReservedUnits,
                "lastReservedUnits", 0);
            Scribe_Values.Look(ref lastCarriedUnits,
                "lastCarriedUnits", 0);
            Scribe_Values.Look(ref lastSourceReservationObserved,
                "lastSourceReservationObserved", false);
            Scribe_Values.Look(ref lastDestinationReservationObserved,
                "lastDestinationReservationObserved", false);
            Scribe_Values.Look(ref status, "status");
            Scribe_Collections.Look(ref pendingThingIds,
                "pendingThingIds", LookMode.Value);
            Scribe_Collections.Look(ref succeededThingIds,
                "succeededThingIds", LookMode.Value);
            Scribe_Collections.Look(ref authorizedSources,
                "authorizedSources", LookMode.Deep);
            if (pendingThingIds == null)
                pendingThingIds = new List<string>();
            if (succeededThingIds == null)
                succeededThingIds = new List<string>();
            if (authorizedSources == null)
                authorizedSources =
                    new List<CAToxicWasteAuthorizedSourceRecord>();
        }
    }

    internal sealed class CAToxicWasteTransportRecord : IExposable
    {
        public string shipmentId;
        public string operationId;
        public string mode;
        public int capacityUnits;
        public int reservedUnits;
        public string status;

        public void ExposeData()
        {
            Scribe_Values.Look(ref shipmentId, "shipmentId");
            Scribe_Values.Look(ref operationId, "operationId");
            Scribe_Values.Look(ref mode, "mode");
            Scribe_Values.Look(ref capacityUnits, "capacityUnits", 0);
            Scribe_Values.Look(ref reservedUnits, "reservedUnits", 0);
            Scribe_Values.Look(ref status, "status");
        }
    }

    internal sealed class CAToxicWasteDestinationRecord : IExposable
    {
        public string kind;
        public int mapId = -1;
        public int programId;
        public string programLabel;
        public CASpacePurpose purpose;
        public string externalSettlementId;
        public string externalWorldTile;
        public string status;
        public List<IntVec3> cells = new List<IntVec3>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref programId, "programId", 0);
            Scribe_Values.Look(ref programLabel, "programLabel");
            Scribe_Values.Look(ref purpose, "purpose", CASpacePurpose.None);
            Scribe_Values.Look(ref externalSettlementId,
                "externalSettlementId");
            Scribe_Values.Look(ref externalWorldTile, "externalWorldTile");
            Scribe_Values.Look(ref status, "status");
            Scribe_Collections.Look(ref cells, "cells", LookMode.Value);
            if (cells == null) cells = new List<IntVec3>();
        }
    }

    internal sealed class CAToxicWasteConsequenceRecord : IExposable
    {
        public string reconciledOperationId;
        public int inventoryDeltaUnits;
        public float pollutionDelta;
        public int goodwillDelta;
        public string status;
        public List<string> knowledgeRecordIds = new List<string>();
        public List<string> grievanceRecordIds = new List<string>();
        public List<string> conflictRecordIds = new List<string>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref reconciledOperationId,
                "reconciledOperationId");
            Scribe_Values.Look(ref inventoryDeltaUnits,
                "inventoryDeltaUnits", 0);
            Scribe_Values.Look(ref pollutionDelta, "pollutionDelta", 0f);
            Scribe_Values.Look(ref goodwillDelta, "goodwillDelta", 0);
            Scribe_Values.Look(ref status, "status");
            Scribe_Collections.Look(ref knowledgeRecordIds,
                "knowledgeRecordIds", LookMode.Value);
            Scribe_Collections.Look(ref grievanceRecordIds,
                "grievanceRecordIds", LookMode.Value);
            Scribe_Collections.Look(ref conflictRecordIds,
                "conflictRecordIds", LookMode.Value);
            if (knowledgeRecordIds == null)
                knowledgeRecordIds = new List<string>();
            if (grievanceRecordIds == null)
                grievanceRecordIds = new List<string>();
            if (conflictRecordIds == null)
                conflictRecordIds = new List<string>();
        }
    }

    internal sealed class CAToxicWasteNativeResponseRecord : IExposable
    {
        public string responseId;
        public string triggerOrigin;
        public string sourceThingId;
        public IntVec3 sourceCell = IntVec3.Invalid;
        public int observedTick = -1;
        public bool canDissolveAtObservation;
        public int dissolutionIntervalTicks = -1;
        public bool listedForNativeHaulingAtObservation;
        public string eligibleHaulerWitnessId;
        public bool allowedForWitnessAtObservation;
        public string authority;
        public bool autonomous;
        public string workGiverDef;
        public string jobDef;
        public int jobLoadId = -1;
        public string pawnId;
        public int requestedUnits;
        public int carriedUnits;
        public bool sourceReservationObserved;
        public bool destinationReservationObserved;
        public string destinationKind;
        public int destinationProgramId;
        public string destinationThingId;
        public IntVec3 destinationCell = IntVec3.Invalid;
        public int startedTick = -1;
        public int finishedTick = -1;
        public string jobCondition;
        public int completedUnits;
        public bool destinationStorageValidAtFinish;
        public float destinationTemperatureAtFinish = 999f;
        public bool destinationFrozenAtFinish;
        public bool dissolutionSnapshotObservedAtFinish;
        public bool sourceCanDissolveAtFinish;
        public bool trackedSourcePlacedAtFinish;
        public string status;

        public void ExposeData()
        {
            Scribe_Values.Look(ref responseId, "responseId");
            Scribe_Values.Look(ref triggerOrigin, "triggerOrigin");
            Scribe_Values.Look(ref sourceThingId, "sourceThingId");
            Scribe_Values.Look(ref sourceCell, "sourceCell", IntVec3.Invalid);
            Scribe_Values.Look(ref observedTick, "observedTick", -1);
            Scribe_Values.Look(ref canDissolveAtObservation,
                "canDissolveAtObservation", false);
            Scribe_Values.Look(ref dissolutionIntervalTicks,
                "dissolutionIntervalTicks", -1);
            Scribe_Values.Look(ref listedForNativeHaulingAtObservation,
                "listedForNativeHaulingAtObservation", false);
            Scribe_Values.Look(ref eligibleHaulerWitnessId,
                "eligibleHaulerWitnessId");
            Scribe_Values.Look(ref allowedForWitnessAtObservation,
                "allowedForWitnessAtObservation", false);
            Scribe_Values.Look(ref authority, "authority");
            Scribe_Values.Look(ref autonomous, "autonomous", false);
            Scribe_Values.Look(ref workGiverDef, "workGiverDef");
            Scribe_Values.Look(ref jobDef, "jobDef");
            Scribe_Values.Look(ref jobLoadId, "jobLoadId", -1);
            Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Values.Look(ref requestedUnits, "requestedUnits", 0);
            Scribe_Values.Look(ref carriedUnits, "carriedUnits", 0);
            Scribe_Values.Look(ref sourceReservationObserved,
                "sourceReservationObserved", false);
            Scribe_Values.Look(ref destinationReservationObserved,
                "destinationReservationObserved", false);
            Scribe_Values.Look(ref destinationKind, "destinationKind");
            Scribe_Values.Look(ref destinationProgramId,
                "destinationProgramId", 0);
            Scribe_Values.Look(ref destinationThingId,
                "destinationThingId");
            Scribe_Values.Look(ref destinationCell, "destinationCell",
                IntVec3.Invalid);
            Scribe_Values.Look(ref startedTick, "startedTick", -1);
            Scribe_Values.Look(ref finishedTick, "finishedTick", -1);
            Scribe_Values.Look(ref jobCondition, "jobCondition");
            Scribe_Values.Look(ref completedUnits, "completedUnits", 0);
            Scribe_Values.Look(ref destinationStorageValidAtFinish,
                "destinationStorageValidAtFinish", false);
            Scribe_Values.Look(ref destinationTemperatureAtFinish,
                "destinationTemperatureAtFinish", 999f);
            Scribe_Values.Look(ref destinationFrozenAtFinish,
                "destinationFrozenAtFinish", false);
            Scribe_Values.Look(ref dissolutionSnapshotObservedAtFinish,
                "dissolutionSnapshotObservedAtFinish", false);
            Scribe_Values.Look(ref sourceCanDissolveAtFinish,
                "sourceCanDissolveAtFinish", false);
            Scribe_Values.Look(ref trackedSourcePlacedAtFinish,
                "trackedSourcePlacedAtFinish", false);
            Scribe_Values.Look(ref status, "status");
        }
    }

    internal sealed class CAToxicWasteLifecycleObjective : IExposable
    {
        public string objectiveId;
        public int mapId = -1;
        public string strategy = "retain/contain";
        public string authority;
        public string provenance;
        public string evidenceSignature;
        public int createdTick = -1;
        public int updatedTick = -1;
        public int revision;
        public CAToxicWasteLifecycleState state;
        public CAToxicWasteInventoryRecord inventory =
            new CAToxicWasteInventoryRecord();
        public CAToxicWasteCapacityRecord capacity =
            new CAToxicWasteCapacityRecord();
        public CAToxicWasteFreezingRecord freezing =
            new CAToxicWasteFreezingRecord();
        public CAToxicWasteRelocationRecord relocation =
            new CAToxicWasteRelocationRecord();
        public CAToxicWasteTransportRecord transport =
            new CAToxicWasteTransportRecord();
        public CAToxicWasteDestinationRecord destination =
            new CAToxicWasteDestinationRecord();
        public CAToxicWasteConsequenceRecord consequence =
            new CAToxicWasteConsequenceRecord();
        public CAToxicWasteNativeResponseRecord nativeResponse;

        public void ExposeData()
        {
            Scribe_Values.Look(ref objectiveId, "objectiveId");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref strategy, "strategy", "retain/contain");
            Scribe_Values.Look(ref authority, "authority");
            Scribe_Values.Look(ref provenance, "provenance");
            Scribe_Values.Look(ref evidenceSignature, "evidenceSignature");
            Scribe_Values.Look(ref createdTick, "createdTick", -1);
            Scribe_Values.Look(ref updatedTick, "updatedTick", -1);
            Scribe_Values.Look(ref revision, "revision", 0);
            Scribe_Values.Look(ref state, "state",
                CAToxicWasteLifecycleState.Observed);
            Scribe_Deep.Look(ref inventory, "inventory");
            Scribe_Deep.Look(ref capacity, "capacity");
            Scribe_Deep.Look(ref freezing, "freezing");
            Scribe_Deep.Look(ref relocation, "relocation");
            Scribe_Deep.Look(ref transport, "transport");
            Scribe_Deep.Look(ref destination, "destination");
            Scribe_Deep.Look(ref consequence, "consequence");
            Scribe_Deep.Look(ref nativeResponse, "nativeResponse");
            if (inventory == null) inventory = new CAToxicWasteInventoryRecord();
            if (capacity == null) capacity = new CAToxicWasteCapacityRecord();
            if (freezing == null) freezing = new CAToxicWasteFreezingRecord();
            if (relocation == null)
                relocation = new CAToxicWasteRelocationRecord();
            if (transport == null) transport = new CAToxicWasteTransportRecord();
            if (destination == null)
                destination = new CAToxicWasteDestinationRecord();
            if (consequence == null)
                consequence = new CAToxicWasteConsequenceRecord();
        }
    }

    // One player-home objective records the current loaded-map toxic-waste
    // lifecycle. Evidence may bind staging to an existing player-authored
    // facility and native storage. An allowed source plus that native storage
    // policy already authorizes autonomous local Hauling. CA may prioritize a
    // valid response; the native job driver owns validation, reservations,
    // carrying, placement, and terminal condition.
    // The older explicit relocation seam remains only so preserved verification
    // checkpoints can load. Construction, shipment, pollution, and relationship
    // changes remain absent.
    internal sealed class CAToxicWasteLifecycleMapComponent : MapComponent
    {
        internal const string VerificationFixtureAuthorizationOrigin =
            "dev-only fixed-vocabulary fixture invoking the player authorization seam";
        internal const string VerificationFixtureArrivalOrigin =
            "dev-only fixed-vocabulary fixture emulating one allowed delivered Wastepack; no quest provenance claimed";

        private const int RefreshInterval = 2500;

        private sealed class ObservedLot
        {
            internal Thing thing;
            internal string holderId;
            internal IntVec3 cell = IntVec3.Invalid;
            internal bool spawned;
            internal bool roofed;
            internal bool frozen;
            internal bool canDissolveNow;
            internal bool inAtomizer;
            internal bool playerForbidden;
            internal bool inSelectedContainment;
        }

        private CAToxicWasteLifecycleObjective objective;
        private int nextRefreshTick;
        private int nextAuthorizationSequence;
        private string lastOutcome = "no toxic-waste inventory observed";

        public CAToxicWasteLifecycleMapComponent(Map map) : base(map) { }

        internal static CAToxicWasteLifecycleMapComponent For(Map map)
        {
            return map?.GetComponent<CAToxicWasteLifecycleMapComponent>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref objective, "CA_toxicWasteLifecycleObjective");
            Scribe_Values.Look(ref nextRefreshTick,
                "CA_toxicWasteLifecycleNextRefresh", 0);
            Scribe_Values.Look(ref nextAuthorizationSequence,
                "CA_toxicWasteLifecycleNextAuthorizationSequence", 0);
            Scribe_Values.Look(ref lastOutcome,
                "CA_toxicWasteLifecycleLastOutcome",
                "no toxic-waste inventory observed");
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            RefreshObjective();
        }

        public override void MapComponentTick()
        {
            AuditActiveRelocationJob();
            int now = Find.TickManager.TicksGame;
            if (now < nextRefreshTick) return;
            nextRefreshTick = now + RefreshInterval;
            RefreshObjective();
        }

        internal string RefreshAndReceipt()
        {
            RefreshObjective();
            string receipt = Census();
            Log.Message(receipt);
            return receipt;
        }

        internal bool RelocationAuthorizationActiveFor(CASpaceProgram program)
        {
            return program != null && objective?.relocation != null
                && objective.relocation.authorizationActive
                && objective.relocation.authorizedProgramId == program.id;
        }

        internal bool HasRelocationAuthorization =>
            objective?.relocation != null
            && !objective.relocation.authorizationId.NullOrEmpty();

        internal string CurrentAuthorizationIdForVerification =>
            objective?.relocation?.authorizationId;

        internal string CurrentAuthorizationOriginForVerification =>
            objective?.relocation?.authorizationOrigin;

        internal bool CurrentAuthorizationIsVerificationFixture =>
            objective?.relocation?.authorizationOrigin
                == VerificationFixtureAuthorizationOrigin;

        internal bool HasCompletedAuthorizedRelocation
        {
            get
            {
                CAToxicWasteRelocationRecord relocation = objective?.relocation;
                return relocation != null && !relocation.authorizationActive
                    && relocation.completedTick >= 0
                    && relocation.requiredUnits == 0
                    && relocation.authorizedUnits > 0
                    && relocation.movedUnitsByAuthorizedJobs
                        >= relocation.authorizedUnits
                    && relocation.jobsStarted > 0
                    && relocation.jobsSucceeded > 0
                    && relocation.jobsFailed == 0;
            }
        }

        internal bool HasPendingNativeWasteResponse
        {
            get
            {
                CAToxicWasteNativeResponseRecord response =
                    objective?.nativeResponse;
                return response != null && response.startedTick < 0
                    && response.finishedTick < 0;
            }
        }

        internal int ContainmentDestinationProgramId =>
            objective?.destination?.programId ?? 0;

        internal int ContainmentDestinationFreeCapacityUnits =>
            objective?.capacity?.freeCapacityUnits ?? 0;

        internal bool HasActiveNativeWasteResponse
        {
            get
            {
                CAToxicWasteNativeResponseRecord response =
                    objective?.nativeResponse;
                return response != null && response.startedTick >= 0
                    && response.finishedTick < 0;
            }
        }

        internal bool HasCompletedVerificationNativeWasteResponse
        {
            get
            {
                CAToxicWasteNativeResponseRecord response =
                    objective?.nativeResponse;
                return response != null
                    && response.triggerOrigin == VerificationFixtureArrivalOrigin
                    && response.autonomous
                    && response.workGiverDef != "CA_ToxicWasteRelocation"
                    && response.startedTick >= 0
                    && response.finishedTick >= response.startedTick
                    && response.jobCondition == JobCondition.Succeeded.ToString()
                    && response.completedUnits > 0
                    && response.destinationStorageValidAtFinish
                    && response.trackedSourcePlacedAtFinish
                    && !response.destinationKind.NullOrEmpty();
            }
        }

        internal bool HasActiveAuthorizedRelocation
        {
            get
            {
                CAToxicWasteRelocationRecord relocation = objective?.relocation;
                return relocation != null && relocation.authorizationActive
                    && relocation.authorizedUnits > 0
                    && relocation.movedUnitsByAuthorizedJobs == 0
                    && relocation.jobsStarted > 0
                    && relocation.jobsSucceeded == 0
                    && relocation.jobsFailed == 0
                    && relocation.activeJobLoadId >= 0
                    && relocation.reservedUnits > 0
                    && relocation.lastSourceReservationObserved
                    && relocation.lastDestinationReservationObserved;
            }
        }

        internal bool RelocationActionAvailableFor(CASpaceProgram program)
        {
            CAToxicWasteRelocationRecord relocation = objective?.relocation;
            if (program == null || relocation == null) return false;
            if (relocation.authorizationActive
                && relocation.authorizedProgramId == program.id)
                return true;
            return relocation.activeJobLoadId < 0
                && relocation.reservedUnits <= 0
                && relocation.requiredUnits > 0
                && objective.destination?.programId == program.id;
        }

        internal string RelocationAuthoritySummary(CASpaceProgram program)
        {
            if (program == null || !IsStagingPurpose(program.purpose))
                return "This purpose cannot receive local Wastepack containment.";
            CAToxicWasteRelocationRecord relocation = objective?.relocation;
            if (relocation == null)
                return "No current toxic-waste objective.";
            if (relocation.authorizedProgramId == program.id)
            {
                if (relocation.authorizationActive)
                    return "Legacy checkpoint operation "
                        + (relocation.operationId ?? "pending") + ": "
                        + relocation.requiredUnits + " unit(s) remain; native "
                        + "Hauling work is active. New arrivals do not require "
                        + "this extra authorization.";
                if (relocation.completedTick >= 0)
                    return "The legacy verification relocation completed at tick "
                        + relocation.completedTick + "; ordinary allowed arrivals "
                        + "now rely on this program's native storage policy.";
                if (relocation.revokedTick >= 0)
                    return "The legacy verification authorization was revoked at "
                        + "tick " + relocation.revokedTick + "; ordinary allowed "
                        + "arrivals rely on native storage policy.";
            }
            if (objective.destination?.programId == program.id
                && relocation.requiredUnits > 0)
                return relocation.requiredUnits + " unit(s) require relocation; "
                    + "allowed source state plus this player-authored, "
                    + "Wastepack-accepting storage policy already authorizes "
                    + "ordinary native Hauling. CA creates no second authorization.";
            if (objective.destination?.programId == program.id)
                return "All current non-atomizer waste is already contained here.";
            return "This program is not the current containment destination.";
        }

        internal string ToggleRelocationAuthorization(CASpaceProgram program)
        {
            return ToggleRelocationAuthorization(program, "player UI");
        }

        internal string ToggleRelocationAuthorization(CASpaceProgram program,
            string authorizationOrigin)
        {
            RefreshObjective();
            int now = Find.TickManager.TicksGame;
            CAToxicWasteRelocationRecord relocation = objective?.relocation;
            if (program == null || relocation == null)
                return "Colonist Awareness: no current toxic-waste relocation exists.";
            if (relocation.authorizationActive
                && relocation.authorizedProgramId == program.id)
            {
                relocation.authorizationActive = false;
                relocation.revokedTick = now;
                relocation.status = relocation.reservedUnits > 0
                    ? "authorization revoked for future relocation; the already-reserved "
                        + "native haul may finish"
                    : "authorization revoked before another native haul began";
                CommitSemanticMutation(now);
                return "Colonist Awareness: future toxic-waste relocation to "
                    + program.label + " is no longer authorized. An already-started "
                    + "native haul may finish.";
            }
            if (relocation.authorizationActive)
                return "Colonist Awareness: revoke the active relocation for program #"
                    + relocation.authorizedProgramId + " before authorizing another.";
            if (relocation.activeJobLoadId >= 0 || relocation.reservedUnits > 0)
                return "Colonist Awareness: an already-reserved native haul is "
                    + "still finishing. A new authorization cannot replace its "
                    + "operation record.";

            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            CASpaceProgram actual = programs?.FindProgram(program.id);
            if (actual == null || actual.author != CASpaceAuthor.Player
                || !IsStagingPurpose(actual.purpose))
                return "Colonist Awareness: the destination is no longer an eligible "
                    + "player-authored Freezer, Storage, or Utility program.";
            CAToxicWasteStagingCandidateRecord candidate = objective.capacity
                .candidates.FirstOrDefault(item => item.programId == actual.id);
            if (candidate == null || objective.destination?.programId != actual.id
                || !candidate.IsContainmentReadyFor(
                    objective.capacity.requiredUnits))
                return "Colonist Awareness: this preserved legacy operation "
                    + "requires its authored program to remain roofed, reachable, "
                    + "Wastepack-accepting, and native-haul-valid.";
            if (relocation.requiredUnits <= 0)
                return "Colonist Awareness: no current waste requires relocation.";

            nextAuthorizationSequence++;
            string authorizationId = objective.objectiveId + "-relocation-"
                + actual.id + "-" + now + "-" + nextAuthorizationSequence;
            relocation.operationId = authorizationId;
            relocation.authorizationId = authorizationId;
            relocation.authorizationOrigin = authorizationOrigin.NullOrEmpty()
                ? "unspecified authorization seam" : authorizationOrigin;
            relocation.authorizationActive = true;
            relocation.authorizedProgramId = actual.id;
            relocation.authorizedProgramSignature = ProgramSignature(actual);
            relocation.authorizedUnits = relocation.requiredUnits;
            relocation.movedUnitsByAuthorizedJobs = 0;
            relocation.authorizedSources.Clear();
            int unitsToAuthorize = relocation.authorizedUnits;
            for (int i = 0; i < relocation.pendingThingIds.Count
                && unitsToAuthorize > 0; i++)
            {
                string thingId = relocation.pendingThingIds[i];
                CAToxicWasteInventoryLotRecord lot = objective.inventory.lots
                    .FirstOrDefault(item => item.thingId == thingId);
                if (lot == null || lot.inAtomizer
                    || lot.inSelectedContainment || lot.units <= 0)
                    continue;
                int units = Mathf.Min(lot.units, unitsToAuthorize);
                relocation.authorizedSources.Add(
                    new CAToxicWasteAuthorizedSourceRecord
                    {
                        thingId = thingId,
                        authorizedUnits = units
                    });
                unitsToAuthorize -= units;
            }
            if (unitsToAuthorize > 0)
            {
                relocation.authorizedUnits -= unitsToAuthorize;
                if (relocation.authorizedUnits <= 0)
                {
                    relocation.operationId = null;
                    relocation.authorizationId = null;
                    relocation.authorizationOrigin = null;
                    relocation.authorizationActive = false;
                    relocation.authorizedProgramId = 0;
                    relocation.authorizedProgramSignature = null;
                    relocation.authorizedSources.Clear();
                    return "Colonist Awareness: no concrete pending source units "
                        + "could be bound to this authorization.";
                }
            }
            relocation.authorizedTick = now;
            relocation.revokedTick = -1;
            relocation.completedTick = -1;
            relocation.jobsStarted = 0;
            relocation.jobsSucceeded = 0;
            relocation.jobsFailed = 0;
            relocation.activeJobLoadId = -1;
            relocation.lastJobStartedTick = -1;
            relocation.lastJobFinishedTick = -1;
            relocation.lastJobPawnId = null;
            relocation.lastJobThingId = null;
            relocation.lastCarriedThingId = null;
            relocation.lastJobDestination = IntVec3.Invalid;
            relocation.lastJobCondition = null;
            relocation.lastReservedUnits = 0;
            relocation.lastCarriedUnits = 0;
            relocation.lastSourceReservationObserved = false;
            relocation.lastDestinationReservationObserved = false;
            relocation.succeededThingIds.Clear();
            relocation.reservedUnits = 0;
            relocation.status = "exact local relocation authorization recorded from "
                + relocation.authorizationOrigin + "; native "
                + "Hauling work, storage validation, reservation, carrying, and "
                + "placement remain authoritative";
            CommitSemanticMutation(now);
            return "Colonist Awareness: authorized native relocation of "
                + relocation.requiredUnits + " wastepack unit(s) to "
                + actual.label + ".";
        }

        internal string RegisterNativeWasteArrival(Thing thing,
            string triggerOrigin, Pawn eligibleHaulerWitness = null)
        {
            RefreshObjective();
            if (thing == null || thing.Destroyed || !thing.Spawned
                || thing.Map != map || thing.def?.defName != "Wastepack")
                return "Colonist Awareness: the arrival trigger is not one real "
                    + "spawned Wastepack on this map.";
            if (OperationalAccessComponent.IsPlayerForbidden(thing))
                return "Colonist Awareness: the arrived Wastepack is player-forbidden; "
                    + "the existing storage policy does not override that veto.";
            if (objective == null || objective.destination == null
                || objective.destination.programId <= 0
                || objective.relocation == null
                || objective.relocation.requiredUnits <= 0)
                return "Colonist Awareness: no loose Wastepack currently has an "
                    + "existing player-authored containment destination.";
            if (HasPendingNativeWasteResponse || HasActiveNativeWasteResponse)
                return "Colonist Awareness: an earlier native Wastepack response is "
                    + "still pending or active.";

            CAToxicWasteInventoryLotRecord lot = objective.inventory.lots
                .FirstOrDefault(item => item.thingId == thing.ThingID);
            if (lot == null || lot.inAtomizer || lot.inSelectedContainment)
                return "Colonist Awareness: the exact arrived Wastepack is not a "
                    + "loose source outside the existing response destinations.";
            bool listedForNativeHauling = map.listerHaulables
                .ThingsPotentiallyNeedingHauling().Contains(thing);
            if (!listedForNativeHauling)
                return "Colonist Awareness: the allowed arrived Wastepack is not "
                    + "currently listed by RimWorld as needing native Hauling.";
            bool witnessAllowed = eligibleHaulerWitness == null
                || EligibleHauler(eligibleHaulerWitness)
                    && eligibleHaulerWitness.Map == map
                    && !thing.IsForbidden(eligibleHaulerWitness)
                    && HaulAIUtility.PawnCanAutomaticallyHaulFast(
                        eligibleHaulerWitness, thing, forced: false);
            if (!witnessAllowed)
                return "Colonist Awareness: the supplied Hauling witness cannot "
                    + "autonomously act on the source under current player area "
                    + "and work restrictions.";

            int now = Find.TickManager.TicksGame;
            CompDissolution dissolution = thing.TryGetComp<CompDissolution>();
            objective.nativeResponse = new CAToxicWasteNativeResponseRecord
            {
                responseId = objective.objectiveId + "-native-response-"
                    + thing.ThingID + "-" + now,
                triggerOrigin = triggerOrigin.NullOrEmpty()
                    ? "direct loaded-map observation; no quest provenance available"
                    : triggerOrigin,
                sourceThingId = thing.ThingID,
                sourceCell = thing.Position,
                observedTick = now,
                canDissolveAtObservation = dissolution?.CanDissolveNow == true,
                dissolutionIntervalTicks = dissolution == null
                    ? -1 : dissolution.DissolutionIntervalTicks,
                listedForNativeHaulingAtObservation =
                    listedForNativeHauling,
                eligibleHaulerWitnessId = eligibleHaulerWitness?.ThingID,
                allowedForWitnessAtObservation = witnessAllowed,
                authority = "the source is allowed and the existing player-authored "
                    + "native Wastepack storage policy already authorizes ordinary "
                    + "Hauling; no second CA per-haul authorization was created",
                destinationProgramId = objective.destination.programId,
                destinationKind = "awaiting an autonomous destination within the "
                    + "existing authored storage",
                status = "real allowed Wastepack observed outside existing storage; "
                    + "awaiting an autonomous Hauling response"
            };
            CommitSemanticMutation(now);
            return "Colonist Awareness: recorded one real allowed Wastepack arrival; "
                + "existing native storage policy is the authority and the work "
                + "system may respond without another player action.";
        }

        internal void NotifyNativeWasteResponseStarted(Pawn pawn, Job job)
        {
            if (pawn?.Map != map || job == null || job.playerForced
                || job.workGiverDef?.defName == "CA_ToxicWasteRelocation")
                return;
            bool nativeWasteHaul = job.def == JobDefOf.HaulToCell
                    && job.targetA.Thing?.def?.defName == "Wastepack"
                || job.def == JobDefOf.HaulToAtomizer
                    && job.targetQueueB != null
                    && job.targetQueueB.Any(target =>
                        target.Thing?.def?.defName == "Wastepack");
            if (!nativeWasteHaul) return;
            RefreshObjective();

            Thing source;
            string destinationKind;
            int destinationProgramId;
            string destinationThingId;
            IntVec3 destinationCell;
            string authority;
            if (!TryResolveNativeWasteResponse(job, out source,
                out destinationKind, out destinationProgramId,
                out destinationThingId, out destinationCell, out authority))
                return;

            CAToxicWasteNativeResponseRecord response =
                objective?.nativeResponse;
            if (response != null && response.finishedTick < 0
                && response.sourceThingId != source.ThingID)
                return;
            if (response == null || response.finishedTick >= 0)
            {
                int observed = Find.TickManager.TicksGame;
                CompDissolution dissolution = source
                    .TryGetComp<CompDissolution>();
                response = new CAToxicWasteNativeResponseRecord
                {
                    responseId = objective.objectiveId + "-native-response-"
                        + source.ThingID + "-" + observed,
                    triggerOrigin = "direct loaded-map observation; no quest "
                        + "provenance available",
                    sourceThingId = source.ThingID,
                    sourceCell = source.Position,
                    observedTick = observed,
                    canDissolveAtObservation =
                        dissolution?.CanDissolveNow == true,
                    dissolutionIntervalTicks = dissolution == null
                        ? -1 : dissolution.DissolutionIntervalTicks,
                    listedForNativeHaulingAtObservation = map.listerHaulables
                        .ThingsPotentiallyNeedingHauling().Contains(source),
                    eligibleHaulerWitnessId = pawn.ThingID,
                    allowedForWitnessAtObservation =
                        !source.IsForbidden(pawn)
                };
                objective.nativeResponse = response;
            }

            bool sourceReserved;
            bool destinationReserved;
            NativeResponseReservationEvidence(job, source,
                out sourceReserved, out destinationReserved);
            response.authority = authority;
            response.autonomous = true;
            response.workGiverDef = job.workGiverDef?.defName ?? "unknown";
            response.jobDef = job.def?.defName ?? "unknown";
            response.jobLoadId = job.loadID;
            response.pawnId = pawn.ThingID;
            response.requestedUnits = Mathf.Max(0, Mathf.Min(
                source.stackCount, job.count > 0 ? job.count : source.stackCount));
            response.sourceReservationObserved = sourceReserved;
            response.destinationReservationObserved = destinationReserved;
            response.destinationKind = destinationKind;
            response.destinationProgramId = destinationProgramId;
            response.destinationThingId = destinationThingId;
            response.destinationCell = destinationCell;
            response.startedTick = Find.TickManager.TicksGame;
            response.finishedTick = -1;
            response.jobCondition = JobCondition.Ongoing.ToString();
            response.status = "autonomous work response started through "
                + response.workGiverDef + "; RimWorld's native job driver owns "
                + "reservation, carrying, and placement, while CA changed no "
                + "facility, storage setting, source permission, or destination "
                + "program";
            CommitSemanticMutation(response.startedTick);
        }

        internal void ObserveNativeWasteBeforeDrop(Pawn pawn, Job job)
        {
            CAToxicWasteNativeResponseRecord response =
                objective?.nativeResponse;
            if (pawn?.Map != map || job == null || response == null
                || response.jobLoadId != job.loadID || response.finishedTick >= 0)
                return;
            Thing carried = pawn.carryTracker?.CarriedThing;
            if (carried?.def?.defName != "Wastepack") return;
            int carriedUnits = Mathf.Max(0, carried.stackCount);
            if (response.carriedUnits == carriedUnits) return;
            response.carriedUnits = carriedUnits;
            CommitSemanticMutation(Find.TickManager.TicksGame);
        }

        internal void NotifyNativeWasteResponseFinished(Pawn pawn, Job job,
            JobCondition condition)
        {
            CAToxicWasteNativeResponseRecord response =
                objective?.nativeResponse;
            if (pawn?.Map != map || job == null || response == null
                || response.jobLoadId != job.loadID || response.finishedTick >= 0)
                return;
            int now = Find.TickManager.TicksGame;
            response.finishedTick = now;
            response.jobCondition = condition.ToString();
            RefreshObjective();
            response = objective?.nativeResponse;
            if (response == null) return;
            response.destinationStorageValidAtFinish =
                NativeResponseDestinationStorageValid(response);
            response.destinationTemperatureAtFinish =
                NativeResponseDestinationTemperature(response);
            response.destinationFrozenAtFinish = response.destinationKind
                    != "existing player-controlled auto-load atomizer"
                && response.destinationTemperatureAtFinish <= 0f;
            bool canDissolve;
            response.dissolutionSnapshotObservedAtFinish =
                TryNativeResponseDissolutionSnapshot(response,
                    out canDissolve);
            response.sourceCanDissolveAtFinish = canDissolve;
            response.trackedSourcePlacedAtFinish =
                condition == JobCondition.Succeeded
                && response.destinationStorageValidAtFinish
                && response.requestedUnits > 0
                && (response.carriedUnits > 0
                    || response.jobDef == JobDefOf.HaulToAtomizer.defName);
            bool completed = condition == JobCondition.Succeeded
                && response.trackedSourcePlacedAtFinish;
            response.completedUnits = completed
                ? Mathf.Max(response.carriedUnits, response.requestedUnits) : 0;
            response.status = completed
                ? "autonomous native response placed the tracked units into the "
                    + "existing destination; the finish temperature and dissolution "
                    + "state are a separate dynamic snapshot, not a placement "
                    + "success criterion; no construction or second authorization "
                    + "occurred"
                : (condition == JobCondition.Succeeded
                    ? "native job reported success, but existing authored storage "
                        + "and carried-unit placement evidence did not remain valid "
                        + "at finish; completion is not claimed"
                    : "native response ended " + condition
                        + "; no completion is claimed");
            CommitSemanticMutation(now);
        }

        private bool NativeResponseDestinationStorageValid(
            CAToxicWasteNativeResponseRecord response)
        {
            if (response == null) return false;
            if (response.destinationKind
                == "existing player-controlled auto-load atomizer")
            {
                List<Thing> atomizers = map.listerThings
                    .ThingsInGroup(ThingRequestGroup.Atomizer);
                Thing atomizerThing = atomizers.FirstOrDefault(thing =>
                    thing?.ThingID == response.destinationThingId);
                return atomizerThing?.Faction == Faction.OfPlayer
                    && atomizerThing.TryGetComp<CompAtomizer>() != null;
            }

            IntVec3 cell = response.destinationCell;
            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            CASpaceProgram program = programs?.FindProgram(
                response.destinationProgramId);
            ThingDef wastepack = DefDatabase<ThingDef>
                .GetNamedSilentFail("Wastepack");
            SlotGroup group = cell.IsValid && cell.InBounds(map)
                ? cell.GetSlotGroup(map) : null;
            return wastepack != null && program != null
                && program.author == CASpaceAuthor.Player
                && IsStagingPurpose(program.purpose)
                && program.cells != null && program.cells.Contains(cell)
                && cell.Roofed(map)
                && group != null && group.parent.HaulDestinationEnabled
                && group.Settings.AllowedToAccept(wastepack)
                && (!(group.parent is Thing parentThing)
                    || parentThing.Faction == Faction.OfPlayer);
        }

        private float NativeResponseDestinationTemperature(
            CAToxicWasteNativeResponseRecord response)
        {
            IntVec3 cell = response?.destinationCell ?? IntVec3.Invalid;
            return cell.IsValid && cell.InBounds(map)
                ? GenTemperature.GetTemperatureForCell(cell, map) : 999f;
        }

        private bool TryNativeResponseDissolutionSnapshot(
            CAToxicWasteNativeResponseRecord response, out bool canDissolve)
        {
            canDissolve = false;
            if (response == null) return false;
            if (response.destinationKind
                == "existing player-controlled auto-load atomizer")
                return true;
            IntVec3 cell = response.destinationCell;
            if (!cell.IsValid || !cell.InBounds(map)) return false;
            List<Thing> things = cell.GetThingList(map);
            bool observed = false;
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing?.def?.defName != "Wastepack") continue;
                CompDissolution dissolution = thing.TryGetComp<CompDissolution>();
                if (dissolution == null) continue;
                observed = true;
                canDissolve |= dissolution.CanDissolveNow;
            }
            return observed;
        }

        private bool TryResolveNativeWasteResponse(Job job, out Thing source,
            out string destinationKind, out int destinationProgramId,
            out string destinationThingId, out IntVec3 destinationCell,
            out string authority)
        {
            source = null;
            destinationKind = null;
            destinationProgramId = 0;
            destinationThingId = null;
            destinationCell = IntVec3.Invalid;
            authority = null;
            if (objective == null) return false;

            if (job.def == JobDefOf.HaulToCell)
            {
                source = job.targetA.Thing;
                destinationCell = job.targetB.Cell;
                if (source?.def?.defName != "Wastepack"
                    || OperationalAccessComponent.IsPlayerForbidden(source)
                    || objective.destination?.cells == null
                    || !objective.destination.cells.Contains(destinationCell))
                    return false;
                PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
                CASpaceProgram program = programs?.FindProgram(
                    objective.destination.programId);
                SlotGroup group = destinationCell.GetSlotGroup(map);
                if (program == null || program.author != CASpaceAuthor.Player
                    || group == null || !group.parent.HaulDestinationEnabled
                    || !group.Settings.AllowedToAccept(source))
                    return false;
                destinationKind = "existing player-authored roofed native storage";
                destinationProgramId = program.id;
                authority = "allowed source plus player-authored program #"
                    + program.id + " and its native Wastepack-accepting storage "
                    + "filter; ordinary RimWorld Hauling is sufficient authority";
                return true;
            }

            if (job.def != JobDefOf.HaulToAtomizer) return false;
            Thing atomizerThing = job.targetA.Thing;
            CompAtomizer atomizer = atomizerThing?.TryGetComp<CompAtomizer>();
            source = WastepackForNativeResponse(job.targetQueueB);
            if (source == null || atomizer == null
                || atomizerThing.Faction != Faction.OfPlayer
                || !atomizer.AutoLoad || atomizer.Full
                || OperationalAccessComponent.IsPlayerForbidden(source))
                return false;
            destinationKind = "existing player-controlled auto-load atomizer";
            destinationThingId = atomizerThing.ThingID;
            destinationCell = atomizerThing.Position;
            authority = "allowed source plus the existing player-controlled "
                + "atomizer's native Auto-load setting; RimWorld's atomizer "
                + "WorkGiver is the executor";
            return true;
        }

        private void NativeResponseReservationEvidence(Job job, Thing source,
            out bool sourceReserved, out bool destinationReserved)
        {
            sourceReserved = false;
            destinationReserved = false;
            List<ReservationManager.Reservation> reservations = map
                .reservationManager.ReservationsReadOnly;
            for (int i = 0; i < reservations.Count; i++)
            {
                ReservationManager.Reservation reservation = reservations[i];
                if (reservation.Job != job) continue;
                if (reservation.Target.Thing == source)
                    sourceReserved = true;
                if (job.def == JobDefOf.HaulToCell
                    && reservation.Target == job.targetB)
                    destinationReserved = true;
                else if (job.def == JobDefOf.HaulToAtomizer
                    && reservation.Target == job.targetA)
                    destinationReserved = true;
            }
        }

        private Thing WastepackForNativeResponse(
            List<LocalTargetInfo> targets)
        {
            if (targets == null) return null;
            CAToxicWasteNativeResponseRecord nativeResponse =
                objective != null ? objective.nativeResponse : null;
            string trackedId = nativeResponse != null && nativeResponse.finishedTick < 0
                ? nativeResponse.sourceThingId : null;
            if (!trackedId.NullOrEmpty())
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    Thing tracked = targets[i].Thing;
                    if (tracked?.def?.defName == "Wastepack"
                        && tracked.ThingID == trackedId)
                        return tracked;
                }
            }
            for (int i = 0; i < targets.Count; i++)
            {
                Thing thing = targets[i].Thing;
                if (thing?.def?.defName == "Wastepack") return thing;
            }
            return null;
        }

        internal string Census()
        {
            if (objective == null)
                return "[CA] toxic-waste lifecycle objective: none; "
                    + lastOutcome + ". No facility, relocation, shipment, or "
                    + "strategy was fabricated.";

            var builder = new StringBuilder();
            builder.Append("[CA] toxic-waste lifecycle objective: ")
                .Append(objective.objectiveId).Append("; state ")
                .Append(objective.state).Append("; revision ")
                .Append(objective.revision).Append("; evidence ")
                .Append(objective.evidenceSignature ?? "pending")
                .Append("; created tick ")
                .Append(objective.createdTick).Append("; updated tick ")
                .Append(objective.updatedTick).AppendLine()
                .Append("  authority: ").Append(objective.authority)
                .AppendLine()
                .Append("  provenance: ").Append(objective.provenance)
                .AppendLine()
                .Append("  strategy scope: ").Append(objective.strategy)
                .AppendLine(" is the current player-colony response because real "
                    + "authored containment exists. Cooling and freezing are "
                    + "separate capability-dependent tactics. No NPC or tribal "
                    + "strategy selection, expansion, remediation, bargain, export, "
                    + "coercive dump, retaliation, or hostile operation is built.")
                .Append("  inventory record: ")
                .Append(objective.inventory.stacks).Append(" real stack(s)/")
                .Append(objective.inventory.units).Append(" unit(s); spawned ")
                .Append(objective.inventory.spawnedUnits)
                .Append(", contained ").Append(objective.inventory.containedUnits)
                .Append(", inside atomizers ")
                .Append(objective.inventory.atomizerUnits)
                .Append(", player-forbidden ")
                .Append(objective.inventory.playerForbiddenUnits).AppendLine();
            for (int i = 0; i < objective.inventory.lots.Count; i++)
            {
                CAToxicWasteInventoryLotRecord lot = objective.inventory.lots[i];
                builder.Append("    lot ").Append(lot.thingId).Append(": ")
                    .Append(lot.units).Append(" unit(s), ")
                    .Append(lot.spawned ? "spawned at " + lot.cell
                        : "held by " + lot.holderId + " at " + lot.cell)
                    .Append(", roofed ").Append(YesNo(lot.roofed))
                    .Append(", frozen ").Append(YesNo(lot.frozen))
                    .Append(", can dissolve now ")
                    .Append(YesNo(lot.canDissolveNow))
                    .Append(", atomizer ").Append(YesNo(lot.inAtomizer))
                    .Append(", selected containment ")
                    .Append(YesNo(lot.inSelectedContainment)).AppendLine();
            }
            builder.Append("  containment capacity record: required bounded local ")
                .Append(objective.capacity.requiredUnits)
                .Append(" unit(s); selected player program ")
                .Append(objective.capacity.selectedProgramId == 0
                    ? "none" : "#" + objective.capacity.selectedProgramId)
                .Append("; native containment-haul-valid bounded capacity ")
                .Append(objective.capacity.boundedCapacityUnits)
                .Append(", free ").Append(objective.capacity.freeCapacityUnits)
                .Append(", deficit ").Append(objective.capacity.deficitUnits)
                .Append("; candidates ")
                .Append(objective.capacity.candidates.Count).AppendLine();
            for (int i = 0; i < objective.capacity.candidates.Count; i++)
                builder.AppendLine(objective.capacity.candidates[i]
                    .Receipt(objective.capacity.requiredUnits));
            builder.Append("  thermal snapshot: engine freeze threshold <= ")
                .Append(objective.freezing.dissolutionThreshold.ToString("F1"))
                .Append(" C; frozen inventory ")
                .Append(objective.freezing.frozenUnits)
                .Append(", currently able to dissolve ")
                .Append(objective.freezing.canDissolveUnits)
                .Append(", atomizer-protected ")
                .Append(objective.freezing.atomizerProtectedUnits)
                .Append(", frozen units in selected containment ")
                .Append(objective.freezing.selectedStagingUnits)
                .Append("/")
                .Append(objective.freezing.selectedStagingCapacityUnits)
                .Append("; ").Append(objective.freezing.status).AppendLine()
                .Append("  relocation record: required ")
                .Append(objective.relocation.requiredUnits)
                .Append(", already at destination ")
                .Append(objective.relocation.alreadyAtDestinationUnits)
                .Append(", reserved ").Append(objective.relocation.reservedUnits)
                .Append(", player-forbidden pending ")
                .Append(objective.relocation.playerForbiddenUnits)
                .Append(", operation ")
                .Append(objective.relocation.operationId ?? "none")
                .Append("; ").Append(objective.relocation.status)
                .AppendLine()
                .Append("    authorization: ")
                .Append(objective.relocation.authorizationActive
                    ? "active" : "inactive")
                .Append(", id ")
                .Append(objective.relocation.authorizationId ?? "none")
                .Append(", origin ")
                .Append(objective.relocation.authorizationOrigin ?? "none")
                .Append(", player program ")
                .Append(objective.relocation.authorizedProgramId == 0
                    ? "none" : "#" + objective.relocation.authorizedProgramId)
                .Append(", authorized/revoked/completed ticks ")
                .Append(objective.relocation.authorizedTick).Append("/")
                .Append(objective.relocation.revokedTick).Append("/")
                .Append(objective.relocation.completedTick)
                .Append(", authorized/moved units ")
                .Append(objective.relocation.authorizedUnits).Append("/")
                .Append(objective.relocation.movedUnitsByAuthorizedJobs)
                .Append(", jobs started/succeeded/failed ")
                .Append(objective.relocation.jobsStarted).Append("/")
                .Append(objective.relocation.jobsSucceeded).Append("/")
                .Append(objective.relocation.jobsFailed).AppendLine()
                .Append("    authorized source ledger: ")
                .Append(objective.relocation.authorizedSources.Count)
                .Append(" source(s)");
            for (int i = 0; i < objective.relocation.authorizedSources.Count; i++)
            {
                CAToxicWasteAuthorizedSourceRecord source =
                    objective.relocation.authorizedSources[i];
                builder.Append(i == 0 ? "; " : ", ")
                    .Append(source.thingId).Append(" ")
                    .Append(source.movedUnits).Append("/")
                    .Append(source.authorizedUnits);
            }
            builder.AppendLine()
                .Append("    last native haul: pawn ")
                .Append(objective.relocation.lastJobPawnId ?? "none")
                .Append(", thing ")
                .Append(objective.relocation.lastJobThingId ?? "none")
                .Append(", carried thing ")
                .Append(objective.relocation.lastCarriedThingId ?? "none")
                .Append(", destination ")
                .Append(objective.relocation.lastJobDestination.IsValid
                    ? objective.relocation.lastJobDestination.ToString()
                    : "none")
                .Append(", reserved units ")
                .Append(objective.relocation.lastReservedUnits)
                .Append(", carried units ")
                .Append(objective.relocation.lastCarriedUnits)
                .Append(", source/destination reservations observed ")
                .Append(YesNo(objective.relocation
                    .lastSourceReservationObserved)).Append("/")
                .Append(YesNo(objective.relocation
                    .lastDestinationReservationObserved))
                .Append(", condition ")
                .Append(objective.relocation.lastJobCondition ?? "none")
                .Append(", start/finish ticks ")
                .Append(objective.relocation.lastJobStartedTick).Append("/")
                .Append(objective.relocation.lastJobFinishedTick)
                .Append(", succeeded thing IDs ")
                .Append(objective.relocation.succeededThingIds.Count);
            CAToxicWasteNativeResponseRecord nativeResponse =
                objective.nativeResponse;
            if (nativeResponse == null)
            {
                builder.AppendLine()
                    .Append("  native arrival response: none; no allowed "
                        + "out-of-destination Wastepack arrival has been bound "
                        + "to an observed autonomous job in this objective");
            }
            else
            {
                builder.AppendLine()
                    .Append("  native arrival response: ")
                    .Append(nativeResponse.responseId ?? "none")
                    .Append("; trigger ")
                    .Append(nativeResponse.triggerOrigin ?? "unknown")
                    .Append("; source ")
                    .Append(nativeResponse.sourceThingId ?? "none")
                    .Append(" at ")
                    .Append(nativeResponse.sourceCell.IsValid
                        ? nativeResponse.sourceCell.ToString() : "unknown")
                    .Append("; observed tick ")
                    .Append(nativeResponse.observedTick).AppendLine()
                    .Append("    dissolution evidence: can dissolve now ")
                    .Append(YesNo(nativeResponse.canDissolveAtObservation))
                    .Append(", interval ticks ")
                    .Append(nativeResponse.dissolutionIntervalTicks)
                    .AppendLine()
                    .Append("    native eligibility at observation: haulable "
                        + "lister ")
                    .Append(YesNo(nativeResponse
                        .listedForNativeHaulingAtObservation))
                    .Append(", witness ")
                    .Append(nativeResponse.eligibleHaulerWitnessId ?? "none")
                    .Append(", allowed for witness ")
                    .Append(YesNo(nativeResponse
                        .allowedForWitnessAtObservation)).AppendLine()
                    .Append("    authority: ")
                    .Append(nativeResponse.authority ?? "not yet resolved")
                    .AppendLine()
                    .Append("    executor: autonomous ")
                    .Append(YesNo(nativeResponse.autonomous))
                    .Append(", work giver ")
                    .Append(nativeResponse.workGiverDef ?? "none")
                    .Append(", job ")
                    .Append(nativeResponse.jobDef ?? "none")
                    .Append(" #").Append(nativeResponse.jobLoadId)
                    .Append(", pawn ")
                    .Append(nativeResponse.pawnId ?? "none")
                    .Append(", start/finish ticks ")
                    .Append(nativeResponse.startedTick).Append("/")
                    .Append(nativeResponse.finishedTick).AppendLine()
                    .Append("    movement evidence: requested/carried/completed ")
                    .Append(nativeResponse.requestedUnits).Append("/")
                    .Append(nativeResponse.carriedUnits).Append("/")
                    .Append(nativeResponse.completedUnits)
                    .Append(", source/destination reservations observed ")
                    .Append(YesNo(nativeResponse.sourceReservationObserved))
                    .Append("/")
                    .Append(YesNo(nativeResponse
                        .destinationReservationObserved))
                    .Append(", destination storage valid/tracked source placed at "
                        + "finish ")
                    .Append(YesNo(nativeResponse
                        .destinationStorageValidAtFinish)).Append("/")
                    .Append(YesNo(nativeResponse
                        .trackedSourcePlacedAtFinish)).AppendLine()
                    .Append("    finish thermal snapshot: temperature ")
                    .Append(nativeResponse.destinationTemperatureAtFinish >= 999f
                        ? "unavailable"
                        : nativeResponse.destinationTemperatureAtFinish
                            .ToString("F1") + " C")
                    .Append(", frozen ")
                    .Append(YesNo(nativeResponse.destinationFrozenAtFinish))
                    .Append(", dissolution evidence observed ")
                    .Append(YesNo(nativeResponse
                        .dissolutionSnapshotObservedAtFinish))
                    .Append(", destination Wastepack can dissolve now ")
                    .Append(YesNo(nativeResponse.sourceCanDissolveAtFinish))
                    .AppendLine("; this timestamped state is not treated as a "
                        + "static or universal strategy criterion")
                    .Append("    destination: ")
                    .Append(nativeResponse.destinationKind ?? "not yet selected")
                    .Append(", player program ")
                    .Append(nativeResponse.destinationProgramId == 0
                        ? "none" : "#" + nativeResponse.destinationProgramId)
                    .Append(", thing ")
                    .Append(nativeResponse.destinationThingId ?? "none")
                    .Append(", cell ")
                    .Append(nativeResponse.destinationCell.IsValid
                        ? nativeResponse.destinationCell.ToString() : "unknown")
                    .Append(", condition ")
                    .Append(nativeResponse.jobCondition ?? "none")
                    .Append("; ").Append(nativeResponse.status ?? "pending")
                    .AppendLine()
                    .Append("    mutation boundary: CA created no facility, "
                        + "program, storage filter, storage priority, source "
                        + "permission, or second authorization. The CA response "
                        + "WorkGiver selected an existing valid storage cell; "
                        + "RimWorld's native job driver owned reservations, carrying, "
                        + "and placement");
            }
            builder.AppendLine()
                .Append("  transport record: shipment ")
                .Append(objective.transport.shipmentId ?? "none")
                .Append(", operation ")
                .Append(objective.transport.operationId ?? "none")
                .Append(", mode ").Append(objective.transport.mode ?? "none")
                .Append(", capacity ").Append(objective.transport.capacityUnits)
                .Append(", reserved ").Append(objective.transport.reservedUnits)
                .Append("; ").Append(objective.transport.status).AppendLine()
                .Append("  destination record: ")
                .Append(objective.destination.kind ?? "none")
                .Append("; map ").Append(objective.destination.mapId)
                .Append(", player program ")
                .Append(objective.destination.programId == 0
                    ? "none" : objective.destination.programLabel + " #"
                        + objective.destination.programId + " ("
                        + CASpacePurposeInfo.Label(objective.destination.purpose)
                        + ")")
                .Append(", bounded cells ")
                .Append(objective.destination.cells.Count)
                .Append(", external settlement ")
                .Append(objective.destination.externalSettlementId ?? "none")
                .Append(", world tile ")
                .Append(objective.destination.externalWorldTile ?? "none")
                .Append("; ").Append(objective.destination.status).AppendLine()
                .Append("  consequence record: reconciled operation ")
                .Append(objective.consequence.reconciledOperationId ?? "none")
                .Append(", inventory delta ")
                .Append(objective.consequence.inventoryDeltaUnits)
                .Append(", pollution delta ")
                .Append(objective.consequence.pollutionDelta.ToString("F3"))
                .Append(", goodwill delta ")
                .Append(objective.consequence.goodwillDelta)
                .Append(", knowledge/grievance/conflict records ")
                .Append(objective.consequence.knowledgeRecordIds.Count)
                .Append("/").Append(objective.consequence.grievanceRecordIds.Count)
                .Append("/").Append(objective.consequence.conflictRecordIds.Count)
                .Append("; ").Append(objective.consequence.status).AppendLine()
                .Append("  maturity boundary: grid, habitation, Defense-program, "
                    + "and turret evidence remain separate. Turret proximity "
                    + "does not prove a defended side; no candidate is declared "
                    + "a mature detached facility until an actual defensive "
                    + "topology relation and the other mature gates are proven. "
                    + "This player-map receipt proves no tribal or NPC access to "
                    + "refrigeration, generation, transport, or remediation.");
            return builder.ToString();
        }

        private void RefreshObjective()
        {
            ThingDef wastepackDef = DefDatabase<ThingDef>
                .GetNamedSilentFail("Wastepack");
            if (wastepackDef == null)
            {
                lastOutcome = "the loaded Def database has no Wastepack";
                return;
            }
            if (!map.IsPlayerHome)
            {
                lastOutcome = "the current map is not a player home";
                return;
            }

            List<ObservedLot> lots = ObserveLots(wastepackDef);
            int totalUnits = lots.Sum(lot => lot.thing.stackCount);
            int now = Find.TickManager.TicksGame;
            if (totalUnits <= 0 && objective == null)
            {
                lastOutcome = "current real inventory is 0 unit(s)";
                return;
            }

            string previousSignature = objective?.evidenceSignature;
            int previousRevision = objective?.revision ?? 0;
            int previousUpdatedTick = objective?.updatedTick ?? -1;
            int previousObservedTick = objective?.inventory?.observedTick ?? -1;
            CAToxicWasteRelocationRecord previousRelocation =
                objective?.relocation;

            if (objective == null)
            {
                objective = new CAToxicWasteLifecycleObjective
                {
                    objectiveId = "player-map-" + map.uniqueID
                        + "-toxic-waste-1",
                    mapId = map.uniqueID,
                    authority = "inventory-created player-settlement objective; "
                        + "facility authorship and native storage remain player "
                        + "authority; an allowed source plus player-authored native "
                        + "Wastepack storage authorizes ordinary local Hauling, "
                        + "while forbidden state remains a player veto",
                    provenance = "loaded-map Wastepack Def, spawned Thing IDs, "
                        + "direct holder IDs, CompDissolution state, existing "
                        + "CASpaceProgram footprints, native slot groups, "
                        + "StoreUtility validation, and ReservationManager evidence",
                    createdTick = now,
                    state = CAToxicWasteLifecycleState.Observed
                };
            }
            objective.authority = "inventory-created player-settlement objective; "
                + "facility authorship and native storage remain player authority; "
                + "an allowed source plus player-authored native Wastepack storage "
                + "authorizes ordinary local Hauling, while forbidden state remains "
                + "a player veto";
            objective.strategy = "retain/contain";
            objective.provenance = "loaded-map Wastepack Def, spawned Thing IDs, "
                + "direct holder IDs, CompDissolution state, existing "
                + "CASpaceProgram footprints, native slot groups, StoreUtility "
                + "validation, and ReservationManager evidence";

            if (totalUnits <= 0)
            {
                objective.inventory = BuildInventoryRecord(lots, now);
                objective.capacity = new CAToxicWasteCapacityRecord();
                objective.freezing = new CAToxicWasteFreezingRecord
                {
                    status = "no current inventory exposes a thermal or dissolution "
                        + "risk to observe"
                };
                CAToxicWasteRelocationRecord closedRelocation = NoRelocation(
                    "the real inventory reconciled to zero; no move is pending");
                if (previousRelocation != null
                    && !previousRelocation.authorizationId.NullOrEmpty())
                {
                    CopyRelocationHistory(previousRelocation, closedRelocation);
                    CompleteSatisfiedRelocation(closedRelocation, now);
                }
                objective.relocation = closedRelocation;
                objective.transport = NoTransport();
                objective.destination = NoDestination(
                    "the closed objective retains no current destination");
                PreserveNoOperationConsequence();
                objective.state = CAToxicWasteLifecycleState.Closed;
                CompleteSemanticRefresh(previousSignature, previousRevision,
                    previousUpdatedTick, previousObservedTick, now);
                lastOutcome = "the retained objective reconciled to zero inventory";
                return;
            }

            int atomizerUnits = lots.Where(lot => lot.inAtomizer)
                .Sum(lot => lot.thing.stackCount);
            int stagingRequired = Mathf.Max(0, totalUnits - atomizerUnits);
            List<CAToxicWasteStagingCandidateRecord> candidates =
                InspectCandidates(wastepackDef, lots, stagingRequired);
            bool priorOperationInForce = previousRelocation != null
                && (previousRelocation.authorizationActive
                    || previousRelocation.activeJobLoadId >= 0
                    || previousRelocation.reservedUnits > 0);
            CAToxicWasteStagingCandidateRecord authorizedCandidate =
                priorOperationInForce
                    ? candidates.FirstOrDefault(candidate => candidate.programId
                        == previousRelocation.authorizedProgramId)
                    : null;
            bool authorizedProgramUnchanged = authorizedCandidate != null
                && previousRelocation.authorizedProgramSignature
                    == ProgramSignature(authorizedCandidate.program);
            if (authorizedProgramUnchanged)
                ApplyActiveHaulCommitment(previousRelocation,
                    authorizedCandidate, wastepackDef);
            CAToxicWasteStagingCandidateRecord selected =
                authorizedProgramUnchanged
                    ? (authorizedCandidate.IsContainmentReadyFor(stagingRequired)
                        ? authorizedCandidate : null)
                    : candidates.FirstOrDefault(candidate => candidate
                        .IsContainmentReadyFor(stagingRequired));
            var selectedCells = selected == null
                ? new HashSet<IntVec3>()
                : new HashSet<IntVec3>(selected.containmentHaulValidCells);
            for (int i = 0; i < lots.Count; i++)
            {
                ObservedLot lot = lots[i];
                lot.inSelectedContainment = lot.spawned
                    && selectedCells.Contains(lot.cell)
                    && lot.roofed
                    && lot.cell.GetSlotGroup(map)?.Settings
                        .AllowedToAccept(wastepackDef) == true;
            }

            objective.inventory = BuildInventoryRecord(lots, now);
            objective.capacity = new CAToxicWasteCapacityRecord
            {
                requiredUnits = stagingRequired,
                selectedProgramId = selected?.programId ?? 0,
                boundedCapacityUnits = selected
                    ?.containmentHaulValidCapacityUnits ?? 0,
                freeCapacityUnits = selected
                    ?.freeContainmentHaulValidCapacityUnits ?? 0,
                deficitUnits = selected == null
                    ? stagingRequired
                    : Mathf.Max(0, stagingRequired
                        - selected.containmentHaulValidCapacityUnits),
                candidates = candidates
            };

            int selectedUnits = lots.Where(lot => lot.inSelectedContainment)
                .Sum(lot => lot.thing.stackCount);
            int selectedFrozenUnits = lots.Where(lot =>
                    lot.inSelectedContainment && lot.frozen)
                .Sum(lot => lot.thing.stackCount);
            int selectedCanDissolveUnits = lots.Where(lot =>
                    lot.inSelectedContainment && lot.canDissolveNow)
                .Sum(lot => lot.thing.stackCount);
            objective.freezing = new CAToxicWasteFreezingRecord
            {
                dissolutionThreshold = 0f,
                frozenUnits = lots.Where(lot => lot.frozen)
                    .Sum(lot => lot.thing.stackCount),
                canDissolveUnits = lots.Where(lot => lot.canDissolveNow)
                    .Sum(lot => lot.thing.stackCount),
                atomizerProtectedUnits = atomizerUnits,
                selectedStagingUnits = selectedFrozenUnits,
                selectedStagingCapacityUnits = selected
                    ?.haulValidCapacityUnits ?? 0,
                status = selected == null
                    ? "no bounded containment destination is selected, so the "
                        + "thermal snapshot does not fabricate one"
                    : "selected containment currently has " + selectedFrozenUnits
                        + " frozen unit(s) and " + selectedCanDissolveUnits
                        + " unit(s) able to dissolve. Temperature may cycle; "
                        + "freezing or cooling is a capability-dependent tactic, "
                        + "not the containment, hauling, or strategy invariant"
            };

            int relocationUnits = Mathf.Max(0, stagingRequired - selectedUnits);
            List<ObservedLot> pending = lots.Where(lot => !lot.inAtomizer
                && !lot.inSelectedContainment).ToList();
            var currentRelocation = new CAToxicWasteRelocationRecord
            {
                requiredUnits = relocationUnits,
                alreadyAtDestinationUnits = selectedUnits,
                reservedUnits = 0,
                playerForbiddenUnits = pending.Where(lot => lot.playerForbidden)
                    .Sum(lot => lot.thing.stackCount),
                status = relocationUnits == 0
                    ? "no relocation is presently required; no reservation or "
                        + "hauling operation was created"
                    : (selected == null
                        ? "relocation is blocked because no existing roofed, "
                            + "bounded native containment destination exists; "
                            + "nothing was reserved"
                        : "a real destination exists; allowed waste may enter "
                            + "through autonomous Hauling under the "
                            + "player-authored storage policy, and no second CA "
                            + "authorization is required. This evaluator reserved "
                            + "and moved nothing"),
                pendingThingIds = pending.Select(lot => lot.thing.ThingID)
                    .OrderBy(id => id, StringComparer.Ordinal).ToList()
            };
            objective.relocation = currentRelocation;
            objective.transport = NoTransport();
            objective.destination = selected == null
                ? NoDestination("no authored local containment destination was "
                    + "selected; no abstract or remote destination was invented")
                : new CAToxicWasteDestinationRecord
                {
                    kind = "existing player-authored local staging",
                    mapId = map.uniqueID,
                    programId = selected.programId,
                    programLabel = selected.programLabel,
                    purpose = selected.purpose,
                    status = "bounded to existing authored cells and existing "
                        + "roofed native Wastepack-accepting storage; current "
                        + "temperature remains separate evidence; no footprint, "
                        + "filter, priority, or inventory changed",
                    cells = selected.containmentHaulValidCells
                        .OrderBy(cell => cell.x)
                        .ThenBy(cell => cell.z).ToList()
                };
            PreserveRelocationAuthority(previousRelocation, currentRelocation,
                authorizedProgramUnchanged, relocationUnits, now);
            PreserveNoOperationConsequence();

            if (stagingRequired == 0)
                objective.state = CAToxicWasteLifecycleState
                    .NativeAtomizerContained;
            else if (selected == null)
                objective.state = CAToxicWasteLifecycleState.StagingBlocked;
            else if (relocationUnits > 0)
                objective.state = CAToxicWasteLifecycleState.ContainmentReady;
            else if (selectedCanDissolveUnits > 0)
                objective.state = CAToxicWasteLifecycleState
                    .ContainedWithThermalRisk;
            else
                objective.state = CAToxicWasteLifecycleState.Contained;
            CompleteSemanticRefresh(previousSignature, previousRevision,
                previousUpdatedTick, previousObservedTick, now);
            lastOutcome = "objective refreshed from " + lots.Count
                + " real stack(s)/" + totalUnits + " unit(s)";
        }

        private List<ObservedLot> ObserveLots(ThingDef wastepackDef)
        {
            var lots = new List<ObservedLot>();
            var counted = new HashSet<Thing>();
            var observed = new List<Thing>();
            ThingOwnerUtility.GetAllThingsRecursively(map,
                ThingRequest.ForDef(wastepackDef), observed,
                allowUnreal: false, passCheck: null,
                alsoGetSpawnedThings: true);
            for (int i = 0; i < observed.Count; i++)
            {
                Thing thing = observed[i];
                if (thing == null || !counted.Add(thing))
                    continue;
                Thing holder = thing.Spawned ? null
                    : ThingOwnerUtility.GetFirstParentThing(thing);
                Thing root = ThingOwnerUtility.GetFirstSpawnedParentThing(thing);
                lots.Add(ObserveLot(thing, holder, root));
            }
            lots.Sort((left, right) => string.Compare(left.thing.ThingID,
                right.thing.ThingID, StringComparison.Ordinal));
            return lots;
        }

        private ObservedLot ObserveLot(Thing thing, Thing holder, Thing root)
        {
            CompDissolution dissolution = thing.TryGetComp<CompDissolution>();
            IntVec3 cell = thing.Spawned ? thing.Position
                : root?.Position ?? ThingOwnerUtility.GetRootPosition(
                    thing.ParentHolder);
            bool cellOnMap = cell.IsValid && cell.InBounds(map);
            return new ObservedLot
            {
                thing = thing,
                holderId = holder?.ThingID,
                cell = cell,
                spawned = thing.Spawned,
                roofed = cellOnMap && cell.Roofed(map),
                frozen = dissolution?.IsFrozen == true,
                canDissolveNow = dissolution?.CanDissolveNow == true,
                inAtomizer = ThingOwnerUtility
                    .GetAnyParent<CompAtomizer>(thing) != null,
                playerForbidden = OperationalAccessComponent
                    .IsPlayerForbidden(thing)
            };
        }

        private CAToxicWasteInventoryRecord BuildInventoryRecord(
            List<ObservedLot> lots, int now)
        {
            var record = new CAToxicWasteInventoryRecord
            {
                observedTick = now,
                stacks = lots.Count,
                units = lots.Sum(lot => lot.thing.stackCount),
                spawnedUnits = lots.Where(lot => lot.spawned)
                    .Sum(lot => lot.thing.stackCount),
                containedUnits = lots.Where(lot => !lot.spawned)
                    .Sum(lot => lot.thing.stackCount),
                atomizerUnits = lots.Where(lot => lot.inAtomizer)
                    .Sum(lot => lot.thing.stackCount),
                playerForbiddenUnits = lots.Where(lot => lot.playerForbidden)
                    .Sum(lot => lot.thing.stackCount)
            };
            for (int i = 0; i < lots.Count; i++)
            {
                ObservedLot lot = lots[i];
                record.lots.Add(new CAToxicWasteInventoryLotRecord
                {
                    thingId = lot.thing.ThingID,
                    holderId = lot.holderId,
                    cell = lot.cell,
                    units = lot.thing.stackCount,
                    spawned = lot.spawned,
                    roofed = lot.roofed,
                    frozen = lot.frozen,
                    canDissolveNow = lot.canDissolveNow,
                    inAtomizer = lot.inAtomizer,
                    inSelectedContainment = lot.inSelectedContainment
                });
            }
            return record;
        }

        private List<CAToxicWasteStagingCandidateRecord> InspectCandidates(
            ThingDef wastepackDef, List<ObservedLot> lots, int requiredUnits)
        {
            PlannedUseMapComponent component = PlannedUseMapComponent.For(map);
            IReadOnlyList<CASpaceProgram> programs =
                component?.ProgramsForObservation
                ?? Array.Empty<CASpaceProgram>();
            var habitation = new HashSet<IntVec3>();
            var defenses = new HashSet<IntVec3>();
            for (int i = 0; i < programs.Count; i++)
            {
                CASpaceProgram program = programs[i];
                if (program?.cells == null) continue;
                HashSet<IntVec3> target = null;
                if (program.purpose == CASpacePurpose.Barracks
                    || program.purpose == CASpacePurpose.Bedroom)
                    target = habitation;
                else if (program.author == CASpaceAuthor.Player
                    && program.purpose == CASpacePurpose.Defense)
                    target = defenses;
                if (target == null) continue;
                for (int c = 0; c < program.cells.Count; c++)
                    if (program.cells[c].InBounds(map))
                        target.Add(program.cells[c]);
            }

            var turrets = new List<IntVec3>();
            var poweredCells = new List<IntVec3>();
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                if (building == null || !building.Spawned
                    || building.Faction != Faction.OfPlayer) continue;
                if (building is Building_Bed)
                    habitation.Add(building.Position);
                if (building.def.building?.IsTurret == true)
                    turrets.Add(building.Position);
            }
            foreach (IntVec3 cell in map.AllCells)
                if (map.powerNetGrid.TransmittedPowerNetAt(cell) != null)
                    poweredCells.Add(cell);

            List<Pawn> playerPawns = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn != null && pawn.Spawned && !pawn.Dead
                    && pawn.Faction == Faction.OfPlayer).ToList();
            List<Pawn> haulers = playerPawns.Where(EligibleHauler).ToList();
            var candidates = new List<CAToxicWasteStagingCandidateRecord>();
            for (int i = 0; i < programs.Count; i++)
            {
                CASpaceProgram program = programs[i];
                if (program == null || program.author != CASpaceAuthor.Player
                    || (program.purpose != CASpacePurpose.Freezer
                        && program.purpose != CASpacePurpose.Storage
                        && program.purpose != CASpacePurpose.Utility))
                    continue;
                candidates.Add(InspectCandidate(program, wastepackDef, lots,
                    playerPawns, haulers, habitation, defenses, turrets,
                    poweredCells));
            }

            return candidates
                .OrderByDescending(candidate =>
                    candidate.wasteUnitsInReachableContainmentStorage)
                .ThenBy(candidate => Mathf.Max(0, requiredUnits
                    - candidate.containmentHaulValidCapacityUnits))
                .ThenByDescending(candidate =>
                    candidate.containmentHaulValidCapacityUnits)
                .ThenByDescending(candidate => KnownDistance(
                    candidate.nearestHabitation))
                .ThenBy(candidate => candidate.programId)
                .ToList();
        }

        private CAToxicWasteStagingCandidateRecord InspectCandidate(
            CASpaceProgram program, ThingDef wastepackDef,
            List<ObservedLot> lots, List<Pawn> playerPawns, List<Pawn> haulers,
            HashSet<IntVec3> habitation, HashSet<IntVec3> defenses,
            List<IntVec3> turrets, List<IntVec3> poweredCells)
        {
            var footprint = new HashSet<IntVec3>(program.cells
                .Where(cell => cell.InBounds(map)));
            var evidence = new CAToxicWasteStagingCandidateRecord
            {
                program = program,
                programId = program.id,
                programLabel = program.label.NullOrEmpty()
                    ? CASpacePurposeInfo.Label(program.purpose) : program.label,
                purpose = program.purpose,
                eligibleHaulers = haulers.Count,
                authoredCells = program.cells.Count,
                authoredFootprint = footprint.OrderBy(cell => cell.x)
                    .ThenBy(cell => cell.z).ToList(),
                nearestGrid = NearestDistance(footprint, poweredCells),
                nearestHabitation = NearestDistance(footprint, habitation),
                nearestDefenseProgram = NearestDistance(footprint, defenses),
                nearestTurret = NearestDistance(footprint, turrets)
            };
            var countedBeds = new HashSet<string>();
            for (int i = 0; i < lots.Count; i++)
                if (lots[i].spawned && footprint.Contains(lots[i].cell))
                    evidence.wasteUnitsInFootprint += lots[i].thing.stackCount;

            foreach (IntVec3 cell in footprint)
            {
                if (map.powerNetGrid.TransmittedPowerNetAt(cell) != null)
                    evidence.gridCells++;
                Building_Bed bed = cell.GetEdifice(map) as Building_Bed;
                if (bed != null && countedBeds.Add(bed.ThingID))
                    evidence.beds++;

                SlotGroup group = cell.GetSlotGroup(map);
                if (group == null || !group.parent.HaulDestinationEnabled
                    || !group.Settings.AllowedToAccept(wastepackDef))
                    continue;
                Thing parentThing = group.parent as Thing;
                if (parentThing != null
                    && parentThing.Faction != Faction.OfPlayer)
                    continue;
                evidence.acceptedStorageCells++;
                if (!cell.Roofed(map)) continue;
                float temperature = GenTemperature.GetTemperatureForCell(
                    cell, map);
                evidence.roofedStorageCells++;
                evidence.minimumContainmentTemperature = Mathf.Min(
                    evidence.minimumContainmentTemperature, temperature);
                evidence.maximumContainmentTemperature = Mathf.Max(
                    evidence.maximumContainmentTemperature, temperature);
                bool reachable = false;
                for (int p = 0; p < playerPawns.Count; p++)
                    if (playerPawns[p].CanReach(cell, PathEndMode.OnCell,
                        Danger.Some))
                    {
                        reachable = true;
                        break;
                    }
                int currentWaste;
                int freeWaste;
                SpecificCapacity(cell, wastepackDef, out currentWaste,
                    out freeWaste, out int coStored);
                evidence.wasteUnitsInContainmentStorage += currentWaste;
                evidence.freeContainmentCapacityUnits += freeWaste;
                evidence.containmentCapacityUnits += currentWaste + freeWaste;
                evidence.coStoredUnits += coStored;
                if (reachable)
                {
                    evidence.reachableRoofedStorageCells++;
                    evidence.reachableContainmentCells.Add(cell);
                    evidence.wasteUnitsInReachableContainmentStorage +=
                        currentWaste;
                    evidence.freeReachableContainmentCapacityUnits += freeWaste;
                    evidence.reachableContainmentCapacityUnits += currentWaste
                        + freeWaste;
                    int containmentHaulValidFree = freeWaste > 0
                        && HasNativeHaulPath(cell, lots, haulers)
                            ? freeWaste : 0;
                    if (currentWaste > 0 || containmentHaulValidFree > 0)
                    {
                        evidence.containmentHaulValidStorageCells++;
                        evidence.containmentHaulValidCells.Add(cell);
                        evidence.containmentHaulValidCapacityUnits += currentWaste
                            + containmentHaulValidFree;
                        evidence.freeContainmentHaulValidCapacityUnits +=
                            containmentHaulValidFree;
                    }
                }

                if (temperature > 0f) continue;
                evidence.readyStorageCells++;
                evidence.minimumReadyTemperature = Mathf.Min(
                    evidence.minimumReadyTemperature, temperature);
                evidence.maximumReadyTemperature = Mathf.Max(
                    evidence.maximumReadyTemperature, temperature);
                evidence.wasteUnitsInReadyStorage += currentWaste;
                evidence.freeReadyCapacityUnits += freeWaste;
                evidence.readyCapacityUnits += currentWaste + freeWaste;
                if (!reachable) continue;
                evidence.reachableReadyCells++;
                evidence.readyCells.Add(cell);
                evidence.wasteUnitsInReachableReadyStorage += currentWaste;
                evidence.freeReachableReadyCapacityUnits += freeWaste;
                evidence.reachableReadyCapacityUnits += currentWaste
                    + freeWaste;
                int haulValidFree = freeWaste > 0
                    && HasNativeHaulPath(cell, lots, haulers)
                        ? freeWaste : 0;
                if (currentWaste > 0 || haulValidFree > 0)
                {
                    evidence.haulValidStorageCells++;
                    evidence.haulValidCells.Add(cell);
                    evidence.haulValidCapacityUnits += currentWaste
                        + haulValidFree;
                    evidence.freeHaulValidCapacityUnits += haulValidFree;
                }
            }
            evidence.reachableContainmentCells.Sort((left, right) =>
            {
                int x = left.x.CompareTo(right.x);
                return x != 0 ? x : left.z.CompareTo(right.z);
            });
            evidence.containmentHaulValidCells.Sort((left, right) =>
            {
                int x = left.x.CompareTo(right.x);
                return x != 0 ? x : left.z.CompareTo(right.z);
            });
            evidence.readyCells.Sort((left, right) =>
            {
                int x = left.x.CompareTo(right.x);
                return x != 0 ? x : left.z.CompareTo(right.z);
            });
            evidence.haulValidCells.Sort((left, right) =>
            {
                int x = left.x.CompareTo(right.x);
                return x != 0 ? x : left.z.CompareTo(right.z);
            });
            return evidence;
        }

        private bool HasNativeHaulPath(IntVec3 destination,
            List<ObservedLot> lots, List<Pawn> haulers)
        {
            for (int i = 0; i < lots.Count; i++)
            {
                Thing thing = lots[i].thing;
                if (!lots[i].spawned || lots[i].inAtomizer
                    || thing == null || thing.Destroyed
                    || thing.Position == destination) continue;
                for (int p = 0; p < haulers.Count; p++)
                {
                    Pawn pawn = haulers[p];
                    if (thing.IsForbidden(pawn)
                        || !HaulAIUtility.PawnCanAutomaticallyHaulFast(
                            pawn, thing, forced: false)
                        || !destination.IsValidStorageFor(map, thing)
                        || !StoreUtility.IsGoodStoreCell(destination, map,
                            thing, pawn, Faction.OfPlayer)) continue;
                    return true;
                }
            }
            return false;
        }

        private void ApplyActiveHaulCommitment(
            CAToxicWasteRelocationRecord relocation,
            CAToxicWasteStagingCandidateRecord candidate,
            ThingDef wastepackDef)
        {
            bool sourceReserved;
            bool destinationReserved;
            int units = ActiveReservationEvidence(relocation,
                out sourceReserved, out destinationReserved);
            if (units <= 0 || !destinationReserved
                || !relocation.lastSourceReservationObserved)
                return;
            Pawn pawn = ActiveRelocationPawn(relocation.activeJobLoadId);
            Job job = pawn?.CurJob;
            IntVec3 cell = job?.targetB.Cell ?? IntVec3.Invalid;
            Thing thing = job?.targetA.Thing ?? pawn?.carryTracker?.CarriedThing;
            if (job == null || thing == null || thing.def != wastepackDef
                || !cell.IsValid || !candidate.program.cells.Contains(cell)
                || !AuthorizedTargetStorageValid(candidate.program, cell, thing))
                return;

            candidate.containmentHaulValidCapacityUnits = Mathf.Min(
                candidate.reachableContainmentCapacityUnits,
                candidate.containmentHaulValidCapacityUnits + units);
            candidate.freeContainmentHaulValidCapacityUnits = Mathf.Min(
                candidate.freeReachableContainmentCapacityUnits,
                candidate.freeContainmentHaulValidCapacityUnits + units);
            if (!candidate.containmentHaulValidCells.Contains(cell))
            {
                candidate.containmentHaulValidStorageCells++;
                candidate.containmentHaulValidCells.Add(cell);
                candidate.containmentHaulValidCells.Sort((left, right) =>
                {
                    int x = left.x.CompareTo(right.x);
                    return x != 0 ? x : left.z.CompareTo(right.z);
                });
            }
            candidate.reservedHaulCapacityUnits = units;
            candidate.haulValidCapacityUnits = Mathf.Min(
                candidate.reachableReadyCapacityUnits,
                candidate.haulValidCapacityUnits + units);
            candidate.freeHaulValidCapacityUnits = Mathf.Min(
                candidate.freeReachableReadyCapacityUnits,
                candidate.freeHaulValidCapacityUnits + units);
            if (!candidate.haulValidCells.Contains(cell))
            {
                candidate.haulValidStorageCells++;
                candidate.haulValidCells.Add(cell);
                candidate.haulValidCells.Sort((left, right) =>
                {
                    int x = left.x.CompareTo(right.x);
                    return x != 0 ? x : left.z.CompareTo(right.z);
                });
            }
        }

        private static bool EligibleHauler(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed
                && !pawn.Drafted && !pawn.InMentalState
                && pawn.Faction == Faction.OfPlayer
                && (pawn.IsColonist || pawn.IsColonyMech
                    || pawn.IsColonySubhuman)
                && pawn.workSettings?.WorkIsActive(WorkTypeDefOf.Hauling) == true
                && pawn.health?.capacities.CapableOf(
                    PawnCapacityDefOf.Manipulation) == true;
        }

        private void SpecificCapacity(IntVec3 cell, ThingDef wastepackDef,
            out int currentWaste, out int freeWaste, out int coStored)
        {
            currentWaste = 0;
            freeWaste = 0;
            coStored = 0;
            int itemSlots = 0;
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing?.def?.category != ThingCategory.Item) continue;
                itemSlots++;
                if (thing.def == wastepackDef)
                {
                    currentWaste += thing.stackCount;
                    freeWaste += Mathf.Max(0,
                        wastepackDef.stackLimit - thing.stackCount);
                }
                else coStored += Mathf.Max(1, thing.stackCount);
            }
            int emptySlots = Mathf.Max(0,
                cell.GetMaxItemsAllowedInCell(map) - itemSlots);
            freeWaste += emptySlots * Mathf.Max(1, wastepackDef.stackLimit);
        }

        internal IEnumerable<Thing> AutonomousPendingWaste(Pawn pawn)
        {
            var result = new List<Thing>();
            CAToxicWasteLifecycleObjective currentObjective = objective;
            CAToxicWasteRelocationRecord relocation = currentObjective?.relocation;
            ThingDef wastepackDef = DefDatabase<ThingDef>
                .GetNamedSilentFail("Wastepack");
            if (!EligibleHauler(pawn) || wastepackDef == null
                || relocation == null || relocation.requiredUnits <= 0
                || relocation.pendingThingIds == null
                || currentObjective?.destination == null
                || currentObjective.destination.programId <= 0
                || HasActiveNativeWasteResponse)
                return result;

            string trackedSourceId = HasPendingNativeWasteResponse
                ? currentObjective.nativeResponse?.sourceThingId : null;
            List<Thing> things = map.listerThings.ThingsOfDef(wastepackDef);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing == null || !thing.Spawned || thing.Map != map
                    || !relocation.pendingThingIds.Contains(thing.ThingID)
                    || !trackedSourceId.NullOrEmpty()
                        && thing.ThingID != trackedSourceId
                    || OperationalAccessComponent.IsPlayerForbidden(thing)
                    || thing.IsForbidden(pawn)
                    || !HaulAIUtility.PawnCanAutomaticallyHaulFast(
                        pawn, thing, forced: false))
                    continue;
                result.Add(thing);
            }
            return result;
        }

        internal Job AutonomousWasteResponseJob(Pawn pawn, Thing thing)
        {
            if (thing == null
                || !AutonomousPendingWaste(pawn).Contains(thing))
                return null;

            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            CASpaceProgram program = programs?.FindProgram(
                objective.destination.programId);
            if (program == null || program.author != CASpaceAuthor.Player
                || !IsStagingPurpose(program.purpose))
                return null;

            List<IntVec3> destinations = objective.destination.cells;
            if (destinations == null) return null;
            foreach (IntVec3 cell in destinations
                .OrderBy(candidate => thing.Position.DistanceToSquared(candidate))
                .ThenBy(candidate => candidate.x)
                .ThenBy(candidate => candidate.z))
            {
                if (!program.cells.Contains(cell) || !cell.InBounds(map)
                    || !cell.Roofed(map))
                    continue;
                SlotGroup group = cell.GetSlotGroup(map);
                Thing parentThing = group?.parent as Thing;
                if (group == null || !group.parent.HaulDestinationEnabled
                    || !group.Settings.AllowedToAccept(thing)
                    || parentThing != null
                        && parentThing.Faction != Faction.OfPlayer
                    || !cell.IsValidStorageFor(map, thing)
                    || !StoreUtility.IsGoodStoreCell(cell, map, thing, pawn,
                        Faction.OfPlayer))
                    continue;
                int available = cell.GetItemStackSpaceLeftFor(map, thing.def);
                if (available <= 0) continue;
                Job job = HaulAIUtility.HaulToCellStorageJob(pawn, thing,
                    cell, fitInStoreCell: true);
                if (job == null) continue;
                job.count = Mathf.Min(thing.stackCount, available,
                    Mathf.Max(0, job.count));
                if (job.count <= 0) continue;
                job.haulOpportunisticDuplicates = false;
                CAInitiativeTier ceiling = CASpatialInitiativeMapComponent
                    .For(map)?.TierFor(program)
                    ?? CAInitiativeTier.Standard;
                Job current = pawn.CurJob;
                var context = new CABehaviorContext(pawn,
                    CAActorContext.PlayerPawn
                        | CAActorContext.PlayerSpatialAuthority,
                    AutonomyComponent.TierOf(pawn),
                    CAAuthorityOrigin.PlayerDelegated,
                    authoritySatisfied: program.author
                        == CASpaceAuthor.Player,
                    knowledgeSatisfied: objective?.relocation != null
                        && objective.relocation.requiredUnits > 0,
                    knowledgeFresh: true, liveValidated: thing.Spawned,
                    knowledgeRelayed: false, knowledgeAgeTicks: 0,
                    knowledgeConfidence: 1f,
                    knowledgeUncertainty: 0f,
                    capabilitySatisfied: EligibleHauler(pawn),
                    materialSatisfied: thing.Spawned && available > 0,
                    currentIntentCompatible: current == null
                        || !current.playerForced,
                    directPlayerOwnership: current != null
                        && current.playerForced,
                    authorityCeiling: ceiling,
                    authorityBasis: "player-authored waste staging program #"
                        + program.id,
                    knowledgeBasis: "persisted waste relocation deficit",
                    owner: "authored waste staging program");
                CABehaviorDecision decision;
                CAIntentContext intent;
                if (CABehaviorJobOrigin.TryAuthorizeAndRegister(pawn, job,
                        "hazard.toxic_waste_response",
                        CAIntentController.Logistics, context,
                        out decision, out intent,
                        thing.LabelShort + " to program #" + program.id,
                        "authored waste staging program", 6000))
                    return job;
            }
            return null;
        }

        internal IEnumerable<Thing> AuthorizedPendingWaste(Pawn pawn)
        {
            var result = new List<Thing>();
            CAToxicWasteRelocationRecord relocation = objective?.relocation;
            ThingDef wastepackDef = DefDatabase<ThingDef>
                .GetNamedSilentFail("Wastepack");
            if (!EligibleHauler(pawn) || wastepackDef == null
                || relocation?.authorizationActive != true
                || relocation.reservedUnits > 0
                || AuthorizedUnitsRemaining(relocation) <= 0
                || objective.destination?.programId
                    != relocation.authorizedProgramId)
                return result;
            List<Thing> things = map.listerThings.ThingsOfDef(wastepackDef);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                CAToxicWasteAuthorizedSourceRecord source = thing == null
                    ? null : AuthorizedSource(relocation, thing.ThingID);
                if (thing != null && thing.Spawned && source != null
                    && source.RemainingUnits > 0
                    && relocation.pendingThingIds.Contains(thing.ThingID))
                    result.Add(thing);
            }
            return result;
        }

        internal Job AuthorizedHaulJob(Pawn pawn, Thing thing)
        {
            CAToxicWasteRelocationRecord relocation = objective?.relocation;
            CAToxicWasteAuthorizedSourceRecord source = thing == null
                ? null : AuthorizedSource(relocation, thing.ThingID);
            if (!EligibleHauler(pawn) || thing == null || !thing.Spawned
                || thing.Map != map || thing.def.defName != "Wastepack"
                || relocation?.authorizationActive != true
                || relocation.reservedUnits > 0
                || source == null || source.RemainingUnits <= 0
                || AuthorizedUnitsRemaining(relocation) <= 0
                || !relocation.pendingThingIds.Contains(thing.ThingID)
                || thing.IsForbidden(pawn)
                || !HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, thing,
                    forced: false)) return null;

            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            CASpaceProgram program = programs?.FindProgram(
                relocation.authorizedProgramId);
            if (program == null || program.author != CASpaceAuthor.Player
                || !IsStagingPurpose(program.purpose)
                || ProgramSignature(program)
                    != relocation.authorizedProgramSignature)
                return null;

            List<IntVec3> destinations = objective.destination?.cells;
            if (destinations == null) return null;
            foreach (IntVec3 cell in destinations
                .OrderBy(candidate => thing.Position.DistanceToSquared(candidate))
                .ThenBy(candidate => candidate.x)
                .ThenBy(candidate => candidate.z))
            {
                if (!program.cells.Contains(cell) || !cell.InBounds(map)
                    || !cell.Roofed(map))
                    continue;
                SlotGroup group = cell.GetSlotGroup(map);
                Thing parentThing = group?.parent as Thing;
                if (group == null || !group.parent.HaulDestinationEnabled
                    || !group.Settings.AllowedToAccept(thing)
                    || parentThing != null
                        && parentThing.Faction != Faction.OfPlayer
                    || !cell.IsValidStorageFor(map, thing)
                    || !StoreUtility.IsGoodStoreCell(cell, map, thing, pawn,
                        Faction.OfPlayer)) continue;
                int available = cell.GetItemStackSpaceLeftFor(map, thing.def);
                if (available <= 0) continue;
                Job job = HaulAIUtility.HaulToCellStorageJob(pawn, thing,
                    cell, fitInStoreCell: true);
                if (job == null) continue;
                job.count = Mathf.Min(thing.stackCount, available,
                    Mathf.Max(0, job.count), source.RemainingUnits,
                    AuthorizedUnitsRemaining(relocation));
                if (job.count <= 0) continue;
                job.haulOpportunisticDuplicates = false;
                return job;
            }
            return null;
        }

        internal bool AuthorizedDropStillValid(Pawn pawn, Job job)
        {
            CAToxicWasteRelocationRecord relocation = objective?.relocation;
            if (!IsRelocationWorkJob(job) || pawn?.Map != map
                || relocation == null
                || relocation.activeJobLoadId != job.loadID)
                return false;
            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            CASpaceProgram program = programs?.FindProgram(
                relocation.authorizedProgramId);
            Thing carried = pawn.carryTracker?.CarriedThing;
            IntVec3 cell = job.targetB.Cell;
            bool safe = carried?.def?.defName == "Wastepack"
                && program != null && program.author == CASpaceAuthor.Player
                && IsStagingPurpose(program.purpose)
                && ProgramSignature(program)
                    == relocation.authorizedProgramSignature
                && program.cells.Contains(cell)
                && AuthorizedTargetStorageValid(program, cell, carried);
            if (!safe) return false;
            relocation.lastCarriedUnits = carried.stackCount;
            relocation.lastCarriedThingId = carried.ThingID;
            CommitSemanticMutation(Find.TickManager.TicksGame);
            return true;
        }

        internal void NotifyAuthorizedHaulStarted(Pawn pawn, Job job)
        {
            if (!IsRelocationWorkJob(job) || pawn?.Map != map) return;
            CAToxicWasteRelocationRecord relocation = objective?.relocation;
            if (relocation?.authorizationActive != true
                || job.targetB.Cell == IntVec3.Invalid
                || objective.destination?.programId
                    != relocation.authorizedProgramId
                || !objective.destination.cells.Contains(job.targetB.Cell))
                return;
            bool sourceReserved;
            bool destinationReserved;
            int reserved = ReservationEvidence(job, out sourceReserved,
                out destinationReserved);
            int now = Find.TickManager.TicksGame;
            relocation.jobsStarted++;
            relocation.activeJobLoadId = job.loadID;
            relocation.lastJobStartedTick = now;
            relocation.lastJobFinishedTick = -1;
            relocation.lastJobPawnId = pawn.ThingID;
            relocation.lastJobThingId = job.targetA.Thing?.ThingID;
            relocation.lastCarriedThingId = null;
            relocation.lastJobDestination = job.targetB.Cell;
            relocation.lastJobCondition = "Ongoing";
            relocation.lastReservedUnits = reserved;
            relocation.lastCarriedUnits = 0;
            relocation.lastSourceReservationObserved = sourceReserved;
            relocation.lastDestinationReservationObserved =
                destinationReserved;
            relocation.reservedUnits = reserved;
            relocation.status = sourceReserved && destinationReserved
                ? "saved authorized native haul started with real source and "
                    + "destination reservations"
                : "native haul started, but complete source/destination "
                    + "reservation evidence was not observed";
            CommitSemanticMutation(now);
        }

        internal void NotifyAuthorizedHaulFinished(Pawn pawn, Job job,
            JobCondition condition)
        {
            if (!IsRelocationWorkJob(job) || pawn?.Map != map) return;
            CAToxicWasteRelocationRecord relocation = objective?.relocation;
            if (relocation == null || relocation.activeJobLoadId != job.loadID)
                return;
            relocation.reservedUnits = 0;
            relocation.activeJobLoadId = -1;
            relocation.lastJobFinishedTick = Find.TickManager.TicksGame;
            relocation.lastJobCondition = condition.ToString();
            if (condition == JobCondition.Succeeded)
            {
                relocation.jobsSucceeded++;
                CAToxicWasteAuthorizedSourceRecord source = AuthorizedSource(
                    relocation, relocation.lastJobThingId);
                int moved = Mathf.Min(relocation.lastCarriedUnits,
                    source?.RemainingUnits ?? 0,
                    AuthorizedUnitsRemaining(relocation));
                if (source != null) source.movedUnits += moved;
                relocation.movedUnitsByAuthorizedJobs += moved;
                if (!relocation.lastJobThingId.NullOrEmpty()
                    && !relocation.succeededThingIds.Contains(
                        relocation.lastJobThingId))
                    relocation.succeededThingIds.Add(
                        relocation.lastJobThingId);
            }
            else relocation.jobsFailed++;
            RefreshObjective();
        }

        private void AuditActiveRelocationJob()
        {
            CAToxicWasteRelocationRecord relocation = objective?.relocation;
            if (relocation == null || relocation.activeJobLoadId < 0) return;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                Job job = pawn.CurJob;
                if (job == null || job.loadID != relocation.activeJobLoadId)
                    continue;
                PlannedUseMapComponent programs =
                    PlannedUseMapComponent.For(map);
                CASpaceProgram program = programs?.FindProgram(
                    relocation.authorizedProgramId);
                IntVec3 cell = job.targetB.Cell;
                Thing carried = pawn.carryTracker?.CarriedThing
                    ?? job.targetA.Thing;
                ThingDef carriedDef = carried?.def
                    ?? DefDatabase<ThingDef>.GetNamedSilentFail("Wastepack");
                bool safe = carriedDef != null && program != null
                    && program.author == CASpaceAuthor.Player
                    && ProgramSignature(program)
                        == relocation.authorizedProgramSignature
                    && program.cells.Contains(cell) && cell.InBounds(map)
                    && cell.Roofed(map)
                    && carried != null
                    && AuthorizedTargetStorageValid(program, cell, carried);
                if (!safe)
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                return;
            }
        }

        private void PreserveRelocationAuthority(
            CAToxicWasteRelocationRecord previous,
            CAToxicWasteRelocationRecord current,
            bool authorizedProgramUnchanged, int relocationUnits, int now)
        {
            if (previous == null || previous.authorizationId.NullOrEmpty())
                return;
            CopyRelocationHistory(previous, current);
            bool sourceReserved;
            bool destinationReserved;
            current.reservedUnits = ActiveReservationEvidence(current,
                out sourceReserved, out destinationReserved);
            if (current.reservedUnits > 0)
            {
                current.lastSourceReservationObserved |= sourceReserved;
                current.lastDestinationReservationObserved |=
                    destinationReserved;
            }

            if (previous.authorizationActive && !authorizedProgramUnchanged)
            {
                current.authorizationActive = false;
                current.revokedTick = now;
                bool cancellationPending = previous.activeJobLoadId >= 0;
                if (!cancellationPending)
                {
                    current.reservedUnits = 0;
                    current.activeJobLoadId = -1;
                }
                current.status = cancellationPending
                    ? "saved authorization invalidated because the exact authored "
                        + "program identity or footprint changed; the tracked native "
                        + "job is cancellation-pending and no replacement operation "
                        + "may begin before Cleanup"
                    : "saved authorization invalidated because the exact authored "
                        + "program identity or footprint changed";
                return;
            }
            if (relocationUnits <= 0 && previous.operationId != null)
            {
                CompleteSatisfiedRelocation(current, now);
                return;
            }
            if (previous.authorizationActive
                && current.authorizedUnits > 0
                && AuthorizedUnitsRemaining(current) <= 0)
            {
                current.authorizationActive = false;
                if (current.completedTick < 0) current.completedTick = now;
                current.reservedUnits = 0;
                current.activeJobLoadId = -1;
                current.status = "the legacy authorized source/unit scope completed "
                    + "through native Hauling; current allowed unstaged waste "
                    + "outside that scope remains eligible for ordinary native "
                    + "Hauling without a new CA authorization";
                return;
            }
            if (!previous.authorizationActive) return;
            current.authorizationActive = true;
            current.status = objective.destination?.programId
                    == previous.authorizedProgramId
                ? "saved authorization remains active; native Hauling may issue "
                    + "the next exact local move"
                : "saved authorization is paused because the authored destination "
                    + "is not presently roofed, reachable, accepting, and haul-valid";
        }

        private static void CopyRelocationHistory(
            CAToxicWasteRelocationRecord source,
            CAToxicWasteRelocationRecord target)
        {
            target.operationId = source.operationId;
            target.authorizationId = source.authorizationId;
            target.authorizationOrigin = source.authorizationOrigin;
            target.authorizationActive = source.authorizationActive;
            target.authorizedProgramId = source.authorizedProgramId;
            target.authorizedProgramSignature =
                source.authorizedProgramSignature;
            target.authorizedUnits = source.authorizedUnits;
            target.movedUnitsByAuthorizedJobs =
                source.movedUnitsByAuthorizedJobs;
            target.authorizedTick = source.authorizedTick;
            target.revokedTick = source.revokedTick;
            target.completedTick = source.completedTick;
            target.jobsStarted = source.jobsStarted;
            target.jobsSucceeded = source.jobsSucceeded;
            target.jobsFailed = source.jobsFailed;
            target.activeJobLoadId = source.activeJobLoadId;
            target.lastJobStartedTick = source.lastJobStartedTick;
            target.lastJobFinishedTick = source.lastJobFinishedTick;
            target.lastJobPawnId = source.lastJobPawnId;
            target.lastJobThingId = source.lastJobThingId;
            target.lastCarriedThingId = source.lastCarriedThingId;
            target.lastJobDestination = source.lastJobDestination;
            target.lastJobCondition = source.lastJobCondition;
            target.lastReservedUnits = source.lastReservedUnits;
            target.lastCarriedUnits = source.lastCarriedUnits;
            target.lastSourceReservationObserved =
                source.lastSourceReservationObserved;
            target.lastDestinationReservationObserved =
                source.lastDestinationReservationObserved;
            target.succeededThingIds = source.succeededThingIds == null
                ? new List<string>() : new List<string>(source.succeededThingIds);
            target.authorizedSources = source.authorizedSources == null
                ? new List<CAToxicWasteAuthorizedSourceRecord>()
                : source.authorizedSources.Select(item =>
                    new CAToxicWasteAuthorizedSourceRecord
                    {
                        thingId = item.thingId,
                        authorizedUnits = item.authorizedUnits,
                        movedUnits = item.movedUnits
                    }).ToList();
        }

        private static void CompleteSatisfiedRelocation(
            CAToxicWasteRelocationRecord relocation, int now)
        {
            relocation.authorizationActive = false;
            if (relocation.completedTick < 0) relocation.completedTick = now;
            relocation.reservedUnits = 0;
            relocation.activeJobLoadId = -1;
            bool completedByAuthorizedJobs = relocation.authorizedUnits > 0
                && relocation.movedUnitsByAuthorizedJobs
                    >= relocation.authorizedUnits;
            relocation.status = completedByAuthorizedJobs
                ? "saved relocation authorization completed through native Hauling; "
                    + relocation.movedUnitsByAuthorizedJobs + "/"
                    + relocation.authorizedUnits
                    + " authorized unit(s) were placed by CA jobs"
                : "the destination became satisfied outside complete CA job "
                    + "execution; CA jobs placed "
                    + relocation.movedUnitsByAuthorizedJobs + "/"
                    + relocation.authorizedUnits
                    + " authorized unit(s), so the operation is terminally "
                    + "classified as externally superseded";
        }

        private int ActiveReservationEvidence(
            CAToxicWasteRelocationRecord relocation, out bool sourceReserved,
            out bool destinationReserved)
        {
            sourceReserved = false;
            destinationReserved = false;
            if (relocation.activeJobLoadId < 0) return 0;
            List<ReservationManager.Reservation> reservations = map
                .reservationManager.ReservationsReadOnly;
            Job active = null;
            for (int i = 0; i < reservations.Count; i++)
            {
                Job job = reservations[i].Job;
                if (job == null || job.loadID != relocation.activeJobLoadId
                    || !IsRelocationWorkJob(job)) continue;
                active = job;
                if (reservations[i].Target == job.targetA)
                    sourceReserved = true;
                if (reservations[i].Target == job.targetB)
                    destinationReserved = true;
            }
            Pawn claimant = ActiveRelocationPawn(relocation.activeJobLoadId);
            Thing activeThing = active?.targetA.Thing;
            bool carriedByClaimant = activeThing != null
                && !activeThing.Spawned
                && claimant?.carryTracker?.CarriedThing == activeThing;
            if (active == null || !destinationReserved
                || (!sourceReserved
                    && !(relocation.lastSourceReservationObserved
                        && carriedByClaimant)))
                return 0;
            int activeUnits = carriedByClaimant ? activeThing.stackCount
                : (active.count > 0 ? active.count
                    : (activeThing?.stackCount ?? relocation.lastCarriedUnits));
            return Mathf.Max(0, activeUnits);
        }

        private Pawn ActiveRelocationPawn(int jobLoadId)
        {
            if (jobLoadId < 0) return null;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].CurJob?.loadID == jobLoadId
                    && IsRelocationWorkJob(pawns[i].CurJob))
                    return pawns[i];
            return null;
        }

        private int ReservationEvidence(Job job, out bool sourceReserved,
            out bool destinationReserved)
        {
            sourceReserved = false;
            destinationReserved = false;
            List<ReservationManager.Reservation> reservations = map
                .reservationManager.ReservationsReadOnly;
            for (int i = 0; i < reservations.Count; i++)
            {
                if (reservations[i].Job != job) continue;
                if (reservations[i].Target == job.targetA)
                    sourceReserved = true;
                if (reservations[i].Target == job.targetB)
                    destinationReserved = true;
            }
            return sourceReserved && destinationReserved
                ? Mathf.Max(0, job.count) : 0;
        }

        internal static bool IsRelocationWorkJob(Job job)
        {
            return job?.def == JobDefOf.HaulToCell
                && job.workGiverDef?.defName == "CA_ToxicWasteRelocation";
        }

        private static CAToxicWasteAuthorizedSourceRecord AuthorizedSource(
            CAToxicWasteRelocationRecord relocation, string thingId)
        {
            if (relocation?.authorizedSources == null || thingId.NullOrEmpty())
                return null;
            return relocation.authorizedSources.FirstOrDefault(source =>
                source.thingId == thingId);
        }

        private static int AuthorizedUnitsRemaining(
            CAToxicWasteRelocationRecord relocation)
        {
            return relocation == null ? 0 : Mathf.Max(0,
                relocation.authorizedUnits
                    - relocation.movedUnitsByAuthorizedJobs);
        }

        private bool AuthorizedTargetStorageValid(CASpaceProgram program,
            IntVec3 cell, Thing thing)
        {
            if (program?.cells == null || thing == null
                || !program.cells.Contains(cell) || !cell.InBounds(map)
                || !cell.Roofed(map))
                return false;
            SlotGroup group = cell.GetSlotGroup(map);
            Thing parentThing = group?.parent as Thing;
            return group != null && group.parent.HaulDestinationEnabled
                && group.Settings.AllowedToAccept(thing)
                && (parentThing == null
                    || parentThing.Faction == Faction.OfPlayer)
                && cell.IsValidStorageFor(map, thing);
        }

        private static bool IsStagingPurpose(CASpacePurpose purpose)
        {
            return purpose == CASpacePurpose.Freezer
                || purpose == CASpacePurpose.Storage
                || purpose == CASpacePurpose.Utility;
        }

        private static string ProgramSignature(CASpaceProgram program)
        {
            if (program == null || program.cells == null) return null;
            var canonical = new StringBuilder()
                .Append(program.id).Append('|').Append((int)program.author)
                .Append('|').Append((int)program.purpose).Append('|');
            List<IntVec3> cells = program.cells.OrderBy(cell => cell.x)
                .ThenBy(cell => cell.z).ToList();
            AppendCells(canonical, cells);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(
                    canonical.ToString()));
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        private void CommitSemanticMutation(int now)
        {
            if (objective == null) return;
            string current = SemanticSignature();
            if (current == objective.evidenceSignature) return;
            objective.revision++;
            objective.updatedTick = now;
            objective.evidenceSignature = current;
        }

        private CAToxicWasteRelocationRecord NoRelocation(string status)
        {
            return new CAToxicWasteRelocationRecord
            {
                status = status,
                pendingThingIds = new List<string>()
            };
        }

        private CAToxicWasteTransportRecord NoTransport()
        {
            return new CAToxicWasteTransportRecord
            {
                mode = "none",
                status = "not authorized; no waste is reserved and no "
                    + "shipment, carrier, route, or operation exists"
            };
        }

        private CAToxicWasteDestinationRecord NoDestination(string status)
        {
            return new CAToxicWasteDestinationRecord
            {
                kind = "none",
                mapId = map.uniqueID,
                status = status
            };
        }

        private void PreserveNoOperationConsequence()
        {
            if (objective.consequence == null)
                objective.consequence = new CAToxicWasteConsequenceRecord();
            if (!objective.consequence.reconciledOperationId.NullOrEmpty())
                return;
            objective.consequence.inventoryDeltaUnits = 0;
            objective.consequence.pollutionDelta = 0f;
            objective.consequence.goodwillDelta = 0;
            objective.consequence.knowledgeRecordIds.Clear();
            objective.consequence.grievanceRecordIds.Clear();
            objective.consequence.conflictRecordIds.Clear();
            objective.consequence.status = "no CA shipment or operation exists, "
                + "so no operation consequence is claimed or reconciled; this "
                + "does not assert that unrelated native pollution never occurred";
        }

        private void CompleteSemanticRefresh(string previousSignature,
            int previousRevision, int previousUpdatedTick,
            int previousObservedTick, int now)
        {
            string currentSignature = SemanticSignature();
            if (previousSignature == currentSignature)
            {
                objective.revision = previousRevision;
                objective.updatedTick = previousUpdatedTick;
                objective.inventory.observedTick = previousObservedTick;
            }
            else
            {
                objective.revision = previousRevision + 1;
                objective.updatedTick = now;
                objective.inventory.observedTick = now;
            }
            objective.evidenceSignature = currentSignature;
        }

        private string SemanticSignature()
        {
            int revision = objective.revision;
            int updatedTick = objective.updatedTick;
            string evidenceSignature = objective.evidenceSignature;
            objective.revision = 0;
            objective.updatedTick = 0;
            objective.evidenceSignature = null;
            string census;
            try
            {
                census = Census();
            }
            finally
            {
                objective.revision = revision;
                objective.updatedTick = updatedTick;
                objective.evidenceSignature = evidenceSignature;
            }

            var canonical = new StringBuilder(census);
            for (int i = 0; i < objective.capacity.candidates.Count; i++)
            {
                CAToxicWasteStagingCandidateRecord candidate =
                    objective.capacity.candidates[i];
                canonical.Append("|candidate:").Append(candidate.programId)
                    .Append("|footprint:");
                AppendCells(canonical, candidate.authoredFootprint);
                canonical.Append("|reachable-containment:");
                AppendCells(canonical, candidate.reachableContainmentCells);
                canonical.Append("|containment-haul-valid:");
                AppendCells(canonical, candidate.containmentHaulValidCells);
                canonical.Append("|reachable-ready:");
                AppendCells(canonical, candidate.readyCells);
                canonical.Append("|haul-valid:");
                AppendCells(canonical, candidate.haulValidCells);
            }
            canonical.Append("|destination:");
            AppendCells(canonical, objective.destination.cells);
            canonical.Append("|pending:")
                .Append(string.Join(",",
                    objective.relocation.pendingThingIds.ToArray()))
                .Append("|authorized-program-signature:")
                .Append(objective.relocation.authorizedProgramSignature
                    ?? "none")
                .Append("|active-job:")
                .Append(objective.relocation.activeJobLoadId)
                .Append("|succeeded-things:")
                .Append(string.Join(",",
                    objective.relocation.succeededThingIds.ToArray()))
                .Append("|knowledge:")
                .Append(string.Join(",",
                    objective.consequence.knowledgeRecordIds.ToArray()))
                .Append("|grievance:")
                .Append(string.Join(",",
                    objective.consequence.grievanceRecordIds.ToArray()))
                .Append("|conflict:")
                .Append(string.Join(",",
                    objective.consequence.conflictRecordIds.ToArray()));
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(
                    canonical.ToString()));
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        private static void AppendCells(StringBuilder builder,
            List<IntVec3> cells)
        {
            if (cells == null) return;
            for (int i = 0; i < cells.Count; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(cells[i].x).Append(':').Append(cells[i].z);
            }
        }

        private static int KnownDistance(int distance)
        {
            return distance == int.MaxValue ? -1 : distance;
        }

        private static int NearestDistance(IEnumerable<IntVec3> roots,
            IEnumerable<IntVec3> targets)
        {
            if (roots == null || targets == null) return int.MaxValue;
            List<IntVec3> targetList = targets.Where(cell => cell.IsValid)
                .ToList();
            if (targetList.Count == 0) return int.MaxValue;
            int best = int.MaxValue;
            foreach (IntVec3 root in roots)
            {
                if (!root.IsValid) continue;
                for (int i = 0; i < targetList.Count; i++)
                {
                    int distance = root.DistanceToSquared(targetList[i]);
                    if (distance < best) best = distance;
                }
            }
            return best == int.MaxValue ? int.MaxValue
                : Mathf.RoundToInt(Mathf.Sqrt(best));
        }

        private static string YesNo(bool value)
        {
            return value ? "yes" : "no";
        }
    }

    public sealed class WorkGiver_CAToxicWasteRelocation : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                ThingDef wastepack = DefDatabase<ThingDef>
                    .GetNamedSilentFail("Wastepack");
                return wastepack == null
                    ? ThingRequest.ForGroup(ThingRequestGroup.Undefined)
                    : ThingRequest.ForDef(wastepack);
            }
        }

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return CAToxicWasteLifecycleMapComponent.For(pawn?.Map)
                ?.AuthorizedPendingWaste(pawn) ?? Enumerable.Empty<Thing>();
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !PotentialWorkThingsGlobal(pawn).Any();
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t,
            bool forced = false)
        {
            return JobOnThing(pawn, t, forced) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t,
            bool forced = false)
        {
            return CAToxicWasteLifecycleMapComponent.For(pawn?.Map)
                ?.AuthorizedHaulJob(pawn, t);
        }
    }

    public sealed class WorkGiver_CAToxicWasteResponse : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                ThingDef wastepack = DefDatabase<ThingDef>
                    .GetNamedSilentFail("Wastepack");
                return wastepack == null
                    ? ThingRequest.ForGroup(ThingRequestGroup.Undefined)
                    : ThingRequest.ForDef(wastepack);
            }
        }

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return CAToxicWasteLifecycleMapComponent.For(pawn?.Map)
                ?.AutonomousPendingWaste(pawn) ?? Enumerable.Empty<Thing>();
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !PotentialWorkThingsGlobal(pawn).Any();
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t,
            bool forced = false)
        {
            return JobOnThing(pawn, t, forced) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t,
            bool forced = false)
        {
            return CAToxicWasteLifecycleMapComponent.For(pawn?.Map)
                ?.AutonomousWasteResponseJob(pawn, t);
        }
    }

    [HarmonyPatch(typeof(JobDriver_HaulToCell), "BeforeDrop")]
    public static class Patch_CAToxicWasteRelocationBeforeDrop
    {
        public static void Postfix(JobDriver_HaulToCell __instance,
            ref Toil __result)
        {
            if (__result == null) return;
            __result.AddPreInitAction(() =>
                CAToxicWasteLifecycleMapComponent.For(__instance.pawn?.Map)
                    ?.ObserveNativeWasteBeforeDrop(__instance.pawn,
                        __instance.job));
            if (!CAToxicWasteLifecycleMapComponent.IsRelocationWorkJob(
                __instance?.job)) return;
            __result.AddFailCondition(() =>
                CAToxicWasteLifecycleMapComponent.For(__instance.pawn?.Map)
                    ?.AuthorizedDropStillValid(__instance.pawn,
                        __instance.job) != true);
        }
    }

    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Notify_Starting))]
    public static class Patch_CAToxicWasteResponseStarted
    {
        public static void Postfix(JobDriver __instance)
        {
            CAToxicWasteLifecycleMapComponent lifecycle =
                CAToxicWasteLifecycleMapComponent.For(__instance?.pawn?.Map);
            lifecycle?.NotifyNativeWasteResponseStarted(__instance.pawn,
                __instance.job);
            if (__instance is JobDriver_HaulToCell)
                lifecycle?.NotifyAuthorizedHaulStarted(__instance.pawn,
                    __instance.job);
        }
    }

    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
    public static class Patch_CAToxicWasteResponseFinished
    {
        public static void Postfix(JobDriver __instance,
            JobCondition condition)
        {
            CAToxicWasteLifecycleMapComponent lifecycle =
                CAToxicWasteLifecycleMapComponent.For(__instance.pawn?.Map);
            lifecycle?.NotifyNativeWasteResponseFinished(__instance.pawn,
                __instance.job, condition);
            if (__instance is JobDriver_HaulToCell)
                lifecycle?.NotifyAuthorizedHaulFinished(__instance.pawn,
                    __instance.job, condition);
        }
    }
}
