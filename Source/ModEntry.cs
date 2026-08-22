using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    public class AwarenessSettings : ModSettings
    {
        private int authoringDataEpoch = CAPendingAuthoringDataEpoch.Current;
        public bool eatSmart = true;
        public bool criticalHauling = true;
        public bool lifeSafety = true;
        public bool rescueOutsiders = false;
        public bool areaDuty = true;
        public bool autoUnforbidCorpses = true;
        public bool autoDigGraves = true;
        public bool raidResponse = true;
        public bool draftChain = true;
        public bool bodyWeight = true;
        public bool enemyRestraint = true;
        public bool buddyCarry = true;
        public bool commsSystem = true;
        public bool gossip = true;
        public CAInitiativeTier defaultInitiative =
            CAInitiativeTier.Proactive;
        private int initiativeSchema =
            AutonomyComponent.CurrentInitiativeSchema;
        private int legacyDefaultAutonomy = 2;
        public bool survivalResponses = true;
        public bool fieldMedicine = true;
        public bool ambushStrikes = true;
        public bool animalCare = true;
        public bool fireResponse = true;
        public bool weaponTransitions = true;
        public bool withdrawals = true;
        public bool movingFire = true;
        public bool renderArms = true;
        public bool dualWield = true;
        public bool holdOrders = true;
        public bool dragOrders = true;
        public bool trapAwareness = true;
        public bool battleDrills = true;
        public bool knowledgeContacts = true;
        public bool authorityObedience = true;
        public bool operationalAccess = true;
        public bool autonomousHomePlanning = false;
        public bool experimentalDistributedKnowledge = false;
        public bool experimentalBroaderPawnKnowledge = false;
        public int autonomousHomePlanningResetGeneration;
        public List<CAUserCultureProfile> cultureProfiles =
            new List<CAUserCultureProfile>();
        public List<CAUserPoliticalOrderProfile> politicalOrderProfiles =
            new List<CAUserPoliticalOrderProfile>();
        public List<CAUserSocietyProfile> societyProfiles =
            new List<CAUserSocietyProfile>();
        // A behavior system without its causal receipts cannot be evaluated during
        // ordinary play. Tracing is therefore the default operating posture; the
        // player may still disable it explicitly from the mod settings.
        public bool traceBehavior = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref authoringDataEpoch,
                "authoringDataEpoch", 0);
            Scribe_Values.Look(ref eatSmart, "eatSmart", true);
            Scribe_Values.Look(ref criticalHauling, "criticalHauling", true);
            Scribe_Values.Look(ref lifeSafety, "lifeSafety", true);
            Scribe_Values.Look(ref rescueOutsiders, "rescueOutsiders", false);
            Scribe_Values.Look(ref areaDuty, "areaDuty", true);
            Scribe_Values.Look(ref autoUnforbidCorpses, "autoUnforbidCorpses", true);
            Scribe_Values.Look(ref autoDigGraves, "autoDigGraves", true);
            Scribe_Values.Look(ref raidResponse, "raidResponse", true);
            Scribe_Values.Look(ref draftChain, "draftChain", true);
            Scribe_Values.Look(ref bodyWeight, "bodyWeight", true);
            Scribe_Values.Look(ref enemyRestraint, "enemyRestraint", true);
            Scribe_Values.Look(ref buddyCarry, "buddyCarry", true);
            Scribe_Values.Look(ref commsSystem, "commsSystem", true);
            Scribe_Values.Look(ref gossip, "gossip", true);
            Scribe_Values.Look(ref initiativeSchema, "initiativeSchema", 0);
            if (Scribe.mode == LoadSaveMode.Saving
                || initiativeSchema >= AutonomyComponent.CurrentInitiativeSchema)
            {
                Scribe_Values.Look(ref defaultInitiative,
                    "defaultInitiative", CAInitiativeTier.Proactive);
            }
            else
            {
                Scribe_Values.Look(ref legacyDefaultAutonomy,
                    "defaultAutonomy", 2);
            }
            Scribe_Values.Look(ref survivalResponses, "survivalResponses", true);
            Scribe_Values.Look(ref fieldMedicine, "fieldMedicine", true);
            Scribe_Values.Look(ref ambushStrikes, "ambushStrikes", true);
            Scribe_Values.Look(ref animalCare, "animalCare", true);
            Scribe_Values.Look(ref fireResponse, "fireResponse", true);
            Scribe_Values.Look(ref weaponTransitions, "weaponTransitions", true);
            Scribe_Values.Look(ref withdrawals, "withdrawals", true);
            Scribe_Values.Look(ref movingFire, "movingFire", true);
            Scribe_Values.Look(ref renderArms, "renderArms", true);
            Scribe_Values.Look(ref dualWield, "dualWield", true);
            Scribe_Values.Look(ref holdOrders, "holdOrders", true);
            Scribe_Values.Look(ref dragOrders, "dragOrders", true);
            Scribe_Values.Look(ref trapAwareness, "trapAwareness", true);
            Scribe_Values.Look(ref battleDrills, "battleDrills", true);
            Scribe_Values.Look(ref knowledgeContacts, "knowledgeContacts", true);
            Scribe_Values.Look(ref authorityObedience, "authorityObedience", true);
            Scribe_Values.Look(ref operationalAccess, "operationalAccess", true);
            Scribe_Values.Look(ref autonomousHomePlanning, "autonomousHomePlanning", false);
            Scribe_Values.Look(ref experimentalDistributedKnowledge,
                "experimentalDistributedKnowledge", false);
            Scribe_Values.Look(ref experimentalBroaderPawnKnowledge,
                "experimentalBroaderPawnKnowledge", false);
            Scribe_Values.Look(ref autonomousHomePlanningResetGeneration,
                "autonomousHomePlanningResetGeneration", 0);
            Scribe_Collections.Look(ref cultureProfiles,
                "cultureProfiles", LookMode.Deep);
            Scribe_Collections.Look(ref politicalOrderProfiles,
                "politicalOrderProfiles", LookMode.Deep);
            bool currentSocietyProfiles = Scribe.mode == LoadSaveMode.Saving
                || CAPendingAuthoringDataEpoch.CanRead(authoringDataEpoch);
            if (currentSocietyProfiles)
                Scribe_Collections.Look(ref societyProfiles,
                    "societyProfiles", LookMode.Deep);
            Scribe_Values.Look(ref traceBehavior, "traceBehavior", true);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (!CAPendingAuthoringDataEpoch.CanRead(authoringDataEpoch))
                {
                    societyProfiles = new List<CAUserSocietyProfile>();
                    CAPendingAuthoringDataEpoch.RecordDiscard(
                        "saved Society profiles");
                }
                if (initiativeSchema
                    < AutonomyComponent.CurrentInitiativeSchema)
                {
                    defaultInitiative = AutonomyComponent
                        .MigrateLegacyLevel(legacyDefaultAutonomy);
                    initiativeSchema =
                        AutonomyComponent.CurrentInitiativeSchema;
                }
                authoringDataEpoch = CAPendingAuthoringDataEpoch.Current;
                defaultInitiative = AutonomyComponent.Normalize(
                    defaultInitiative);
                CAAuthoringProfileLibrary.Normalize(this);
                CABehaviorRevisions.SettingsChanged();
            }
        }
    }

    public class AwarenessMod : Mod
    {
        internal static AwarenessMod Instance;
        public static AwarenessSettings Settings;

        private Vector2 settingsScrollPosition;
        private float settingsScrollHeight;

        // WHICH BUILD IS ACTUALLY RUNNING. The mod has been debugged
        // against a stale assembly before: the source was fixed, the
        // build succeeded, and the game went on loading an older DLL
        // from a path nobody had checked. Nothing in a log distinguishes
        // those two runs, so hours went into a defect that had already
        // been repaired.
        //
        // The hash printed here is the SHA-256 of the file the game
        // actually loaded, from the path it actually loaded it from -
        // the same hash a deployment prints for the file it wrote. If
        // the two do not match, the run under discussion is not the
        // build under discussion, and that is now the first line in the
        // log rather than a conclusion reached late.
        private static void ReportLoadedBuild()
        {
            try
            {
                System.Reflection.Assembly assembly = System.Reflection
                    .Assembly.GetExecutingAssembly();
                string path = assembly.Location;
                if (path.NullOrEmpty() || !System.IO.File.Exists(path))
                {
                    Log.Message("[CA][Build] loaded from a location the "
                        + "assembly does not report; identity "
                        + assembly.ManifestModule.ModuleVersionId);
                    return;
                }
                string hash;
                using (var sha = System.Security.Cryptography.SHA256
                    .Create())
                using (System.IO.FileStream stream =
                    System.IO.File.OpenRead(path))
                    hash = BitConverter.ToString(sha.ComputeHash(stream))
                        .Replace("-", string.Empty);
                var info = new System.IO.FileInfo(path);
                Log.Message("[CA][Build] sha256=" + hash + " bytes="
                    + info.Length + " written="
                    + info.LastWriteTimeUtc.ToString("s") + "Z path="
                    + path);
            }
            catch (Exception ex)
            {
                Log.Message("[CA][Build] could not identify the loaded "
                    + "assembly: " + ex.Message);
            }
        }

        public AwarenessMod(ModContentPack content) : base(content)
        {
            Instance = this;
            ReportLoadedBuild();
            Settings = GetSettings<AwarenessSettings>();
            var harmony = new Harmony("ellyj3rain.colonistawareness");
            // Per-class application instead of PatchAll: one broken patch
            // class logs loudly and stands down for the session while
            // every other patch still applies. A single bad target
            // aborting PatchAll silently disabled the ENTIRE mod once
            // (2026-08-20); that failure mode must not exist.
            //
            // The class filter is PatchAll's own and is load-bearing:
            // Harmony reads Prepare/Cleanup/Prefix/Postfix by NAME, so
            // handing it every type in the assembly makes it mistake
            // ordinary classes -- every JobDriver's Cleanup, every
            // manually-installed helper's Prefix -- for patch
            // directives and report 62 phantom failures.
            int patchFailures = 0;
            foreach (Type patchType in AccessTools.GetTypesFromAssembly(
                         System.Reflection.Assembly.GetExecutingAssembly()))
            {
                bool annotated;
                try
                {
                    annotated = patchType.GetCustomAttributes(true)
                        .Any(attribute => attribute is HarmonyPatch);
                }
                catch (Exception) { continue; }
                if (!annotated) continue;
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                }
                catch (Exception patchException)
                {
                    patchFailures++;
                    Log.Error("[CA] patch class " + patchType.FullName
                        + " failed to apply and is disabled this "
                        + "session: " + patchException);
                }
            }
            if (patchFailures > 0)
                Log.Error("[CA] " + patchFailures + " patch class(es) "
                    + "failed to apply; every other patch is active. "
                    + "The failing surfaces are degraded until fixed.");
            PortraitMenuPatch.TryInstall(harmony);
            BioWeightBadge.TryInstall(harmony);
            EnemyRestraintModule.TryInstall(harmony);
            StealExclusionPatch.TryInstall(harmony);
            StashPreservationPatch.TryInstall(harmony);
            // Arm nodes parked: operator is authoring arm art manually. Machinery lives in
            // ArmsNodeModule.cs - drop the textures in and restore this install to revive.
            // ArmsNodePatch.TryInstall(harmony);
            OffhandPatches.TryInstall(harmony);
            GearTabOffhandPatch.TryInstall(harmony);
            TrapAwarenessPatch.TryInstall(harmony);
            StackDisciplinePatch.TryInstall(harmony);
            KnowledgePatch.TryInstall(harmony);
            CAViolencePatches.TryInstall(harmony);
            EquipTransitionDespawnPatch.TryInstall(harmony);
            GunshotKnowledgePatch.TryInstall(harmony);
            AssaultAwarenessPatch.TryInstall(harmony);
            CARegionalCompatibility.TryInstall();
            LongEventHandler.ExecuteWhenFinished(
                CARegionalCompatibility.TryInstall);
            Log.Message("[Colonist Awareness] loaded; behavior trace "
                + (Settings != null && Settings.traceBehavior ? "on" : "off")
                + ".");
            Log.Message("[CA][Regional] starting-region authoring, projected "
                + "geography, strategic overview, and saved settlement "
                + "overlays installed.");
        }

        public override string SettingsCategory()
        {
            return "Colonist Awareness";
        }

        internal static void SaveSettings()
        {
            CABehaviorRevisions.SettingsChanged();
            Instance?.WriteSettings();
        }

        private void DrawBehaviorSettings(Rect inRect)
        {
            var viewRect = new Rect(0f, 0f, inRect.width - 16f,
                Mathf.Max(settingsScrollHeight, inRect.height));
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition,
                viewRect);
            var listing = new Listing_Standard();
            Rect listingRect = viewRect;
            listingRect.height = 99999f;
            listing.Begin(listingRect);

            listing.Label("Technological knowledge");
            bool distributed = Settings.experimentalDistributedKnowledge;
            listing.CheckboxLabeled("Experimental distributed knowledge",
                ref distributed,
                "Store the faction's technological knowledge through living carriers and represented records. Loss or incapacity can make knowledge unavailable without erasing its history.");
            if (distributed != Settings.experimentalDistributedKnowledge)
            {
                Settings.experimentalDistributedKnowledge = distributed;
                WriteSettings();
                if (distributed)
                    CATechnologicalKnowledgeRuntime
                        .InitializeCurrentFactionDistribution();
            }
            listing.Label(distributed
                ? "Practical capability follows the knowledge currently available through the faction's pawns and retained records."
                : "Faction knowledge is socially available. Individual carriers are not required.");
            bool broader = Settings.experimentalBroaderPawnKnowledge;
            listing.CheckboxLabeled("Experimental broader pawn knowledge",
                ref broader,
                "Pawns keep separate, revisable records of represented social, political, geographic, and settlement facts. Reports carry the teller's remembered version through a real communication route.");
            if (broader != Settings.experimentalBroaderPawnKnowledge)
            {
                Settings.experimentalBroaderPawnKnowledge = broader;
                WriteSettings();
            }
            listing.Label(broader
                ? "Expanded pawn knowledge is active. This does not hide information from the player."
                : "Existing tactical and welfare knowledge remains active; broader social and world knowledge is not simulated per pawn.");
            listing.GapLine(10f);

            listing.Label("Pawn initiative");
            Rect tierRow = listing.GetRect(34f);
            Widgets.Label(new Rect(tierRow.x, tierRow.y + 6f,
                tierRow.width * 0.42f, 28f), "Default for new and undialed pawns");
            Rect tierButton = new Rect(tierRow.x + tierRow.width * 0.44f,
                tierRow.y, tierRow.width * 0.56f, 30f);
            if (Widgets.ButtonText(tierButton,
                    CAInitiativePresentation.Label(Settings.defaultInitiative)))
            {
                var options = new List<FloatMenuOption>();
                for (int i = 0;
                    i < CAInitiativePresentation.ActiveTiers.Length; i++)
                {
                    CAInitiativeTier tier =
                        CAInitiativePresentation.ActiveTiers[i];
                    options.Add(new FloatMenuOption(
                        CAInitiativePresentation.Label(tier), delegate
                        {
                            Settings.defaultInitiative = tier;
                            CABehaviorRevisions.AutonomyChanged(null);
                            WriteSettings();
                        }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            listing.Label(CAInitiativePresentation.Description(
                Settings.defaultInitiative));
            listing.Label("Initiative controls origination. Feature permission, authority, knowledge, capability, material feasibility, and direct player ownership remain separate checks.");
            listing.GapLine(10f);

            string section = null;
            CASettingKind? kind = null;
            for (int i = 0; i < CABehaviorSettings.All.Length; i++)
            {
                CASettingDefinition setting = CABehaviorSettings.All[i];
                if (setting.Section != section)
                {
                    if (section != null) listing.GapLine(10f);
                    section = setting.Section;
                    kind = null;
                    listing.Label(section);
                }
                if (kind != setting.Kind)
                {
                    kind = setting.Kind;
                    listing.Label(CABehaviorSettings.KindLabel(setting.Kind));
                }
                bool enabled = CABehaviorSettings.IsEnabled(setting.Key,
                    Settings);
                bool previous = enabled;
                string description = CABehaviorSettings.Description(setting);
                listing.CheckboxLabeled(setting.Label, ref enabled,
                    description);
                if (enabled != previous)
                    CABehaviorSettings.SetEnabled(setting.Key, Settings,
                        enabled);
                listing.Label(description);
                listing.Gap(4f);
            }

            settingsScrollHeight = listing.CurHeight;
            listing.End();
            Widgets.EndScrollView();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            DrawBehaviorSettings(inRect);
            base.DoSettingsWindowContents(inRect);
        }
    }
}
