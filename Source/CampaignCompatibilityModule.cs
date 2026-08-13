using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    public sealed class CACampaignSchemaRecord : IExposable
    {
        public string schemaKey;
        public int version;
        public int initializedAtTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaKey, "schemaKey");
            Scribe_Values.Look(ref version, "version", 0);
            Scribe_Values.Look(ref initializedAtTick, "initializedAtTick",
                -1);
        }
    }

    public sealed class CACampaignMigrationReceipt : IExposable
    {
        public string schemaKey;
        public int fromVersion;
        public int toVersion;
        public int appliedAtTick = -1;
        public string provenance;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaKey, "schemaKey");
            Scribe_Values.Look(ref fromVersion, "fromVersion", 0);
            Scribe_Values.Look(ref toVersion, "toVersion", 0);
            Scribe_Values.Look(ref appliedAtTick, "appliedAtTick", -1);
            Scribe_Values.Look(ref provenance, "provenance");
        }
    }

    // One manifest owns campaign schema versions and truthful upgrade-time
    // receipts. It contains no gameplay facts and never asks a creation
    // derivation to rewrite established history.
    public sealed class CACampaignCompatibilityWorldComponent : WorldComponent
    {
        private int boundaryVersion =
            CACampaignCompatibilityKernel.CurrentBoundaryVersion;
        private int catalogVersion =
            CACampaignSchemaCatalog.CurrentCatalogVersion;
        private List<CACampaignSchemaRecord> schemas =
            new List<CACampaignSchemaRecord>();
        private List<CACampaignMigrationReceipt> migrations =
            new List<CACampaignMigrationReceipt>();

        public CACampaignCompatibilityWorldComponent(World world)
            : base(world) { }

        internal static CACampaignCompatibilityWorldComponent Current =>
            Find.World?.GetComponent<CACampaignCompatibilityWorldComponent>();

        internal IReadOnlyList<CACampaignSchemaRecord> Schemas => schemas;
        internal IReadOnlyList<CACampaignMigrationReceipt> Migrations =>
            migrations;

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
                EnsureCatalog("save preflight");
            Scribe_Values.Look(ref boundaryVersion,
                "CA_campaignBoundaryVersion", 0);
            Scribe_Values.Look(ref catalogVersion,
                "CA_campaignCatalogVersion", 0);
            Scribe_Collections.Look(ref schemas, "CA_campaignSchemas",
                LookMode.Deep);
            Scribe_Collections.Look(ref migrations, "CA_campaignMigrations",
                LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;

            if (schemas == null) schemas = new List<CACampaignSchemaRecord>();
            if (migrations == null)
                migrations = new List<CACampaignMigrationReceipt>();
            CACampaignCompatibilityDecision decision =
                CACampaignCompatibilityKernel.EvaluateBoundary(
                    boundaryVersion,
                    CACampaignCompatibility.LastPreflightLegacyEpoch,
                    CACampaignCompatibility.LastPreflightContainsCAState);
            if (!decision.CanLoad)
            {
                CACampaignCompatibility.BlockRuntime(decision.Reason);
                return;
            }
            if (boundaryVersion == 0)
            {
                int from = boundaryVersion;
                boundaryVersion =
                    CACampaignCompatibilityKernel.CurrentBoundaryVersion;
                RecordMigration("campaign.boundary", from, boundaryVersion,
                    CACampaignCompatibility.ProvenanceFor(decision.Kind));
            }
            if (catalogVersion == 0)
                catalogVersion = CACampaignSchemaCatalog.CurrentCatalogVersion;
            EnsureCatalog(CACampaignCompatibility.ProvenanceFor(
                decision.Kind));
            CACampaignCompatibility.DrainPending(this);
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            string provenance = fromLoad
                ? CACampaignCompatibility.ProvenanceFor(
                    CACampaignCompatibility.LastPreflightKind)
                : "new campaign initialization";
            if (fromLoad && CACampaignCompatibility.LastPreflightKind
                != CACampaignCompatibilityKind.Current)
                RecordMigration("campaign.boundary", 0,
                    CACampaignCompatibilityKernel.CurrentBoundaryVersion,
                    provenance);
            EnsureCatalog(provenance);
            CACampaignCompatibility.DrainPending(this);
        }

        internal void RecordMigration(string schemaKey, int fromVersion,
            int toVersion, string provenance)
        {
            if (schemaKey.NullOrEmpty() || toVersion < 1) return;
            if (migrations == null)
                migrations = new List<CACampaignMigrationReceipt>();
            if (migrations.Any(item => item != null
                    && item.schemaKey == schemaKey
                    && item.fromVersion == fromVersion
                    && item.toVersion == toVersion)) return;
            migrations.Add(new CACampaignMigrationReceipt
            {
                schemaKey = schemaKey,
                fromVersion = fromVersion,
                toVersion = toVersion,
                appliedAtTick = Find.TickManager?.TicksGame ?? 0,
                provenance = provenance
            });
        }

        private void EnsureCatalog(string provenance)
        {
            if (schemas == null) schemas = new List<CACampaignSchemaRecord>();
            if (!CACampaignSchemaCatalog.ValidateDefinitions(
                    out string definitionFailure))
            {
                CACampaignCompatibility.BlockRuntime(definitionFailure);
                return;
            }
            for (int i = 0; i < schemas.Count; i++)
            {
                CACampaignSchemaRecord saved = schemas[i];
                if (saved == null || saved.schemaKey.NullOrEmpty())
                {
                    CACampaignCompatibility.BlockRuntime(
                        "campaign schema manifest contains an empty record");
                    return;
                }
                CACampaignCompatibilityDecision decision =
                    CACampaignCompatibilityKernel.EvaluateSchema(
                        saved.schemaKey, saved.version);
                if (!decision.CanLoad)
                {
                    CACampaignCompatibility.BlockRuntime(decision.Reason);
                    return;
                }
            }
            for (int i = 0; i < CACampaignSchemaCatalog.All.Length; i++)
            {
                CACampaignSchemaDefinition definition =
                    CACampaignSchemaCatalog.All[i];
                CACampaignSchemaRecord saved = schemas.FirstOrDefault(item =>
                    item != null && item.schemaKey == definition.Key);
                if (saved != null) continue;
                schemas.Add(new CACampaignSchemaRecord
                {
                    schemaKey = definition.Key,
                    version = definition.CurrentVersion,
                    initializedAtTick = Find.TickManager?.TicksGame ?? 0
                });
                RecordMigration(definition.Key, 0,
                    definition.CurrentVersion, provenance);
            }
            schemas.Sort((left, right) => string.CompareOrdinal(
                left?.schemaKey, right?.schemaKey));
            // The catalog number is published only after every schema
            // introduced by this executable exists and has an initialization
            // receipt. A later catalog therefore cannot strand new entries
            // behind an older persisted catalog number.
            catalogVersion = CACampaignSchemaCatalog.CurrentCatalogVersion;
        }

        internal string Report()
        {
            var text = new StringBuilder();
            text.Append("[CA][Compatibility] boundary=")
                .Append(boundaryVersion)
                .Append("; schemas=").Append(schemas?.Count ?? 0)
                .Append("; migrations=").Append(migrations?.Count ?? 0)
                .Append("; blocked=")
                .Append(CACampaignCompatibility.BlockedReason ?? "no");
            if (migrations != null)
            {
                foreach (CACampaignMigrationReceipt receipt in migrations
                    .Where(item => item != null)
                    .OrderBy(item => item.appliedAtTick)
                    .ThenBy(item => item.schemaKey,
                        StringComparer.Ordinal))
                    text.AppendLine().Append("  ")
                        .Append(receipt.schemaKey).Append(' ')
                        .Append(receipt.fromVersion).Append(" -> ")
                        .Append(receipt.toVersion).Append(" at tick ")
                        .Append(receipt.appliedAtTick).Append(": ")
                        .Append(receipt.provenance);
            }
            return text.ToString();
        }
    }

    internal static class CACampaignCompatibility
    {
        private sealed class PendingMigration
        {
            internal string Key;
            internal int From;
            internal int To;
            internal string Provenance;
        }

        private static readonly List<PendingMigration> Pending =
            new List<PendingMigration>();
        private static bool warningScheduled;
        private static bool rejectionWarningScheduled;
        private static string pendingEntryRejection;
        private static string armedSavePreflight;
        private static long armedSaveLength;
        private static long armedSaveWriteTicks;
        private static string armedSavePayloadDigest;
        private static string lastPreflightPayloadDigest;
        [ThreadStatic] private static string emittedSavePath;
        [ThreadStatic] private static string emittedDocumentRoot;

        internal static string BlockedReason { get; private set; }
        internal static string LastRejectedLoadReason { get; private set; }
        internal static int LastPreflightLegacyEpoch { get; private set; }
        internal static bool LastPreflightContainsCAState { get; private set; }
        internal static CACampaignCompatibilityKind LastPreflightKind
        {
            get; private set;
        }

        internal static bool PreflightSaveFile(string saveFileName)
        {
            using (CAModuleProfiler.Measure(
                CAModuleProfileKey.CompatibilityPreflight))
            {
                ResetPreflightEvidence();
                string path = GenFilePaths.FilePathForSavedGame(saveFileName);
                try
                {
                    CACampaignPreflightDocument document =
                        CACampaignPreflightReader.Read(path);
                int boundary = document.BoundaryVersion;
                    int catalogVersion = document.CatalogVersion;
                    int legacyEpoch = document.LegacyAuthoringEpoch;
                    bool containsCAState = document.ContainsCAState;
                    IReadOnlyDictionary<string, int> manifest =
                        document.Manifest;
                    LastPreflightLegacyEpoch = legacyEpoch;
                    LastPreflightContainsCAState = containsCAState;
                    lastPreflightPayloadDigest = document.PayloadDigest;
                    CACampaignCompatibilityDecision boundaryDecision =
                        CACampaignPreflightValidator.Evaluate(document);
                    LastPreflightKind = boundaryDecision.Kind;
                    if (!boundaryDecision.CanLoad)
                        return RejectLoadAttempt(boundaryDecision.Reason);
                    Log.Message("[CA][Compatibility] preflight accepted "
                        + saveFileName + ": " + boundaryDecision.Reason
                        + "; catalog " + catalogVersion
                        + "; manifest entries " + manifest.Count
                        + "; inline owners "
                        + document.OwnerVersions.Count + ".");
                    // A successful preflight is about to replace the current
                    // game. Any active block belonged to that outgoing game;
                    // a rejected attempt never reaches this line and therefore
                    // never disables saving a valid campaign that stayed open.
                    BlockedReason = null;
                    LastRejectedLoadReason = null;
                    pendingEntryRejection = null;
                    return true;
                }
                catch (Exception exception)
                {
                    CAModuleProfiler.RecordFailure(
                        CAModuleProfileKey.CompatibilityPreflight);
                    return RejectLoadAttempt("campaign compatibility preflight "
                        + "could not read " + saveFileName + ": "
                        + exception.Message);
                }
            }
        }

        // GameDataSaveLoader.LoadGame disposes the current game immediately
        // after queuing the Play transition. Gate that method first, then arm
        // one exact filename for the later SavedGameLoaderNow call. The late
        // guard still covers engine-direct/autostart load paths without reading
        // the same file twice during an ordinary load.
        internal static bool PreflightBeforeGameDisposal(string saveFileName)
        {
            bool accepted = PreflightSaveFile(saveFileName);
            armedSavePreflight = accepted ? saveFileName : null;
            if (accepted)
            {
                var file = new FileInfo(
                    GenFilePaths.FilePathForSavedGame(saveFileName));
                armedSaveLength = file.Length;
                armedSaveWriteTicks = file.LastWriteTimeUtc.Ticks;
                armedSavePayloadDigest = lastPreflightPayloadDigest;
            }
            return accepted;
        }

        internal static bool ConsumePreflightOrValidate(string saveFileName)
        {
            if (!armedSavePreflight.NullOrEmpty()
                && string.Equals(armedSavePreflight, saveFileName,
                    StringComparison.Ordinal))
            {
                var file = new FileInfo(
                    GenFilePaths.FilePathForSavedGame(saveFileName));
                bool sameBytes = file.Exists
                    && file.Length == armedSaveLength
                    && file.LastWriteTimeUtc.Ticks == armedSaveWriteTicks
                    && CACampaignPreflightReader.TryVerifyPayloadDigest(
                        file.FullName, out bool valid, out _,
                        out string payloadDigest) && valid
                    && string.Equals(payloadDigest, armedSavePayloadDigest,
                        StringComparison.Ordinal);
                armedSavePreflight = null;
                armedSaveLength = 0;
                armedSaveWriteTicks = 0;
                armedSavePayloadDigest = null;
                if (sameBytes) return true;
            }
            armedSavePreflight = null;
            armedSaveLength = 0;
            armedSaveWriteTicks = 0;
            armedSavePayloadDigest = null;
            return PreflightSaveFile(saveFileName);
        }

        internal static bool ShouldReadLiveState(string schemaKey,
            int savedOwnerVersion, int legacyAuthoringEpoch)
        {
            if (Scribe.mode == LoadSaveMode.Saving) return !IsBlocked;
            if (IsBlocked) return false;
            if (!CACampaignSchemaCatalog.TryFind(schemaKey,
                    out CACampaignSchemaDefinition definition))
            {
                BlockRuntime("unknown live owner schema " + schemaKey);
                throw new InvalidOperationException(BlockedReason);
            }
            if (savedOwnerVersion >= definition.MinimumCompatibleVersion
                && savedOwnerVersion <= definition.CurrentVersion)
                return true;
            if (savedOwnerVersion == 0
                && legacyAuthoringEpoch ==
                    CACampaignCompatibilityKernel.LegacyB10AuthoringEpoch)
                return true;
            BlockRuntime(schemaKey + " owner schema " + savedOwnerVersion
                + " with legacy epoch " + legacyAuthoringEpoch
                + " is unsupported");
            throw new InvalidOperationException(BlockedReason);
        }

        internal static void CompleteOwnerLoad(string schemaKey,
            ref int ownerVersion, int legacyAuthoringEpoch,
            Func<string> validateState, Func<string> migrateState = null)
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit || IsBlocked) return;
            if (!CACampaignSchemaCatalog.TryFind(schemaKey,
                    out CACampaignSchemaDefinition definition))
                throw new InvalidOperationException(
                    "unknown live owner schema " + schemaKey);
            if (ownerVersion == definition.CurrentVersion)
            {
                ValidateOwnerState(schemaKey, ownerVersion, "loaded",
                    validateState);
                return;
            }
            int from = ownerVersion;
            if (migrateState != null)
            {
                string migrationFailure = migrateState();
                if (!migrationFailure.NullOrEmpty())
                {
                    BlockRuntime(schemaKey + " owner schema " + ownerVersion
                        + " migration failed without committing partial state: "
                        + migrationFailure);
                    throw new InvalidOperationException(BlockedReason);
                }
            }
            else
                ValidateOwnerState(schemaKey, ownerVersion, "loaded",
                    validateState);
            ownerVersion = definition.CurrentVersion;
            ValidateOwnerState(schemaKey, ownerVersion, "migrated",
                validateState);
            CACampaignCompatibilityKind kind = legacyAuthoringEpoch
                    == CACampaignCompatibilityKernel.LegacyB10AuthoringEpoch
                ? CACampaignCompatibilityKind.MigrateB10
                : CACampaignCompatibilityKind.AdditiveBootstrap;
            RecordOwnerMigration(schemaKey, from, ownerVersion,
                ProvenanceFor(kind));
        }

        private static void ValidateOwnerState(string schemaKey,
            int ownerVersion, string phase, Func<string> validateState)
        {
            string failure = validateState?.Invoke();
            if (failure.NullOrEmpty()) return;
            BlockRuntime(schemaKey + " owner schema " + ownerVersion + " "
                + phase + " state is invalid: " + failure);
            throw new InvalidOperationException(BlockedReason);
        }

        internal static void RecordOwnerMigration(string schemaKey,
            int fromVersion, int toVersion, string provenance)
        {
            CACampaignCompatibilityWorldComponent current =
                CACampaignCompatibilityWorldComponent.Current;
            if (current != null)
            {
                current.RecordMigration(schemaKey, fromVersion, toVersion,
                    provenance);
                return;
            }
            if (Pending.Any(item => item.Key == schemaKey
                    && item.From == fromVersion && item.To == toVersion))
                return;
            Pending.Add(new PendingMigration
            {
                Key = schemaKey,
                From = fromVersion,
                To = toVersion,
                Provenance = provenance
            });
        }

        internal static void DrainPending(
            CACampaignCompatibilityWorldComponent component)
        {
            if (component == null || Pending.Count == 0) return;
            for (int i = 0; i < Pending.Count; i++)
            {
                PendingMigration item = Pending[i];
                component.RecordMigration(item.Key, item.From, item.To,
                    item.Provenance);
            }
            Pending.Clear();
        }

        internal static bool BlockRuntime(string reason)
        {
            if (reason.NullOrEmpty()) reason = "unspecified incompatibility";
            if (BlockedReason.NullOrEmpty()) BlockedReason = reason;
            Log.Error("[CA][Compatibility] LOAD BLOCKED: " + reason
                + ". The source save was not rewritten.");
            if (!warningScheduled)
            {
                warningScheduled = true;
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    warningScheduled = false;
                    Find.WindowStack?.Add(new Dialog_MessageBox(
                        "Colonist Awareness did not load this campaign.\n\n"
                        + BlockedReason
                        + "\n\nThe save was not changed. Restore the matching "
                        + "DLL or use an explicitly supported migration before "
                        + "loading it again.", "Close"));
                });
            }
            return false;
        }

        private static bool RejectLoadAttempt(string reason)
        {
            if (reason.NullOrEmpty()) reason = "unspecified incompatibility";
            LastRejectedLoadReason = reason;
            Log.Error("[CA][Compatibility] LOAD REJECTED: " + reason
                + ". The active game and source save were not changed.");
            if (!rejectionWarningScheduled)
            {
                rejectionWarningScheduled = true;
                string captured = reason;
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    rejectionWarningScheduled = false;
                    if (!pendingEntryRejection.NullOrEmpty()) return;
                    Find.WindowStack?.Add(new Dialog_MessageBox(
                        "Colonist Awareness did not load this campaign.\n\n"
                        + captured
                        + "\n\nThe current game and selected save were not "
                        + "changed. Restore the matching DLL or use an "
                        + "explicitly supported migration before loading it.",
                        "Close"));
                });
            }
            return false;
        }

        internal static void CarryRejectedNoticeToEntry()
        {
            if (!LastRejectedLoadReason.NullOrEmpty())
                pendingEntryRejection = LastRejectedLoadReason;
        }

        internal static void ShowEntryRejectionIfPending()
        {
            if (pendingEntryRejection.NullOrEmpty()
                || Find.WindowStack == null) return;
            string reason = pendingEntryRejection;
            pendingEntryRejection = null;
            rejectionWarningScheduled = false;
            Find.WindowStack.Add(new Dialog_MessageBox(
                "Colonist Awareness did not load this campaign.\n\n"
                + reason + "\n\nThe selected save was not changed. Restore "
                + "the matching DLL or use an explicitly supported migration "
                + "before loading it.", "Close"));
        }

        internal static bool IsBlocked => !BlockedReason.NullOrEmpty();

        internal static void CaptureEmittedSave(string path,
            string documentRoot)
        {
            emittedSavePath = path;
            emittedDocumentRoot = documentRoot;
        }

        internal static void SealEmittedSave()
        {
            string path = emittedSavePath;
            string root = emittedDocumentRoot;
            ClearEmittedSaveCapture();
            if (!string.Equals(root, "savegame", StringComparison.Ordinal))
                return;
            if (path.NullOrEmpty() || !File.Exists(path))
                throw new InvalidDataException(
                    "current campaign save candidate is missing");
            CACampaignPreflightDocument candidate =
                CACampaignPreflightReader.Read(path);
            CACampaignCompatibilityDecision decision =
                CACampaignPreflightValidator.EvaluateEmittedCurrent(candidate);
            if (!decision.CanLoad)
                throw new InvalidDataException(
                    "current campaign save candidate is incomplete: "
                    + decision.Reason);
            CACampaignPreflightReader.AppendPayloadDigest(path);
            if (!CACampaignPreflightReader.TryVerifyPayloadDigest(path,
                    out bool valid, out _) || !valid)
                throw new InvalidDataException(
                    "current campaign save candidate could not be sealed");
        }

        internal static void ClearEmittedSaveCapture()
        {
            emittedSavePath = null;
            emittedDocumentRoot = null;
        }

        internal static string ProvenanceFor(
            CACampaignCompatibilityKind kind)
        {
            switch (kind)
            {
                case CACampaignCompatibilityKind.MigrateB10:
                    return "initialized at B11 upgrade from represented B10 "
                        + "state; no earlier history inferred";
                case CACampaignCompatibilityKind.AdditiveBootstrap:
                    return "initialized at upgrade from current represented "
                        + "facts; no earlier history inferred";
                default:
                    return "validated current campaign schema";
            }
        }

        internal static void ResetForNewGame()
        {
            ResetTransient();
            LastPreflightKind =
                CACampaignCompatibilityKind.AdditiveBootstrap;
        }

        private static void ResetTransient()
        {
            BlockedReason = null;
            LastRejectedLoadReason = null;
            pendingEntryRejection = null;
            ResetPreflightEvidence();
            rejectionWarningScheduled = false;
            warningScheduled = false;
        }

        private static void ResetPreflightEvidence()
        {
            LastPreflightLegacyEpoch = 0;
            LastPreflightContainsCAState = false;
            LastPreflightKind =
                CACampaignCompatibilityKind.AdditiveBootstrap;
            Pending.Clear();
            armedSavePreflight = null;
            armedSaveLength = 0;
            armedSaveWriteTicks = 0;
            armedSavePayloadDigest = null;
            lastPreflightPayloadDigest = null;
        }

    }

    [HarmonyPatch(typeof(GameDataSaveLoader),
        nameof(GameDataSaveLoader.LoadGame), new[] { typeof(string) })]
    internal static class Patch_CACampaignCompatibilityPreDisposalPreflight
    {
        private static bool Prefix(string saveFileName)
        {
            return CACampaignCompatibility.PreflightBeforeGameDisposal(
                saveFileName);
        }
    }

    [HarmonyPatch(typeof(SavedGameLoaderNow),
        nameof(SavedGameLoaderNow.LoadGameFromSaveFileNow))]
    internal static class Patch_CACampaignCompatibilityDirectLoadGuard
    {
        private static bool Prefix(string fileName)
        {
            bool accepted = CACampaignCompatibility
                .ConsumePreflightOrValidate(fileName);
            // An engine-direct/autostart call has already entered the Play
            // scene. A rejected load needs a valid root; the ordinary UI path
            // is rejected by the earlier pre-disposal patch and stays in its
            // current game instead.
            if (!accepted && GenScene.InPlayScene)
            {
                CACampaignCompatibility.CarryRejectedNoticeToEntry();
                GenScene.GoToMainMenu();
            }
            return accepted;
        }
    }

    [HarmonyPatch(typeof(Root_Entry), "Update")]
    internal static class Patch_CACampaignCompatibilityEntryNotice
    {
        private static void Postfix()
        {
            CACampaignCompatibility.ShowEntryRejectionIfPending();
        }
    }

    [HarmonyPatch(typeof(GameDataSaveLoader),
        nameof(GameDataSaveLoader.SaveGame))]
    internal static class Patch_CACampaignCompatibilitySaveGuard
    {
        private static bool Prefix(string fileName)
        {
            if (!CACampaignCompatibility.IsBlocked) return true;
            Messages.Message("Colonist Awareness blocked this save because "
                + "the loaded campaign schema is unsupported. Restore the "
                + "matching save and DLL; no file was written.",
                MessageTypeDefOf.RejectInput, false);
            Log.Error("[CA][Compatibility] save " + fileName
                + " refused while load compatibility is blocked: "
                + CACampaignCompatibility.BlockedReason);
            return false;
        }
    }

    [HarmonyPatch(typeof(ScribeSaver), nameof(ScribeSaver.InitSaving))]
    internal static class Patch_CACampaignCompatibilityCaptureSaveCandidate
    {
        private static void Prefix(string filePath,
            string documentElementName)
        {
            CACampaignCompatibility.CaptureEmittedSave(filePath,
                documentElementName);
        }
    }

    [HarmonyPatch(typeof(ScribeSaver), nameof(ScribeSaver.FinalizeSaving))]
    internal static class Patch_CACampaignCompatibilitySealSaveCandidate
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            CACampaignCompatibility.SealEmittedSave();
        }
    }

    [HarmonyPatch(typeof(ScribeSaver), nameof(ScribeSaver.ForceStop))]
    internal static class Patch_CACampaignCompatibilityClearSaveCandidate
    {
        private static void Postfix()
        {
            CACampaignCompatibility.ClearEmittedSaveCapture();
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.InitNewGame))]
    internal static class Patch_CACampaignCompatibilityNewGameReset
    {
        private static void Prefix()
        {
            CACampaignCompatibility.ResetForNewGame();
        }
    }

    public static partial class CADebugActions
    {
        [DebugAction("Colonist Awareness", "Campaign compatibility report",
            actionType = DebugActionType.Action)]
        private static void CampaignCompatibilityReport()
        {
            Log.Message(CACampaignCompatibilityWorldComponent.Current
                ?.Report() ?? "[CA][Compatibility] no loaded world manifest");
        }
    }
}
