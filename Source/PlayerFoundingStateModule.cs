using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    public interface ICAEstablishedPlayerStart
    {
        string CATemporalBasis { get; }
    }

    // Scenario metadata owns the exceptional established-player boundary.
    // Similar class names do not opt a scenario into mature starting history.
    public sealed class ScenPart_CAEstablishedPlayerSettlement : ScenPart,
        ICAEstablishedPlayerStart
    {
        public string temporalBasis = "The scenario begins with an existing "
            + "player settlement.";

        public string CATemporalBasis => temporalBasis;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref temporalBasis, "temporalBasis",
                "The scenario begins with an existing player settlement.");
        }

        public override string Summary(Scenario scenario)
        {
            return temporalBasis;
        }
    }

    // The player authors a founding population, not an already mature NPC
    // faction. Culture, native Ideoligion, Political Order, and Technological
    // Knowledge arrive with
    // the founders. The arrangement is what they establish at landing.
    public sealed class CAPlayerFoundingPlan : IExposable
    {
        public const int CurrentSchemaVersion = 4;
        public int schemaVersion = CurrentSchemaVersion;
        public CACulture culture = new CACulture();
        public CAPoliticalBeliefs politicalBeliefs =
            new CAPoliticalBeliefs();
        public CATechnologicalKnowledge technologicalKnowledge =
            new CATechnologicalKnowledge();
        public CAFoundingArrangement arrangement;
        public byte arrangementSource; // CAAxisSource
        public int nativeIdeoId = -1;
        public string nativeIdeoName;
        public string nativeIdeoSignature;
        public bool nativeIdeoNotified;
        // A new landing and an already inhabited player settlement are
        // different historical situations. This boundary is derived from
        // scenario evidence and persisted; it is not inferred from faction
        // technology, pawn count, or whether the player owns the faction.
        public bool establishedStart;
        public string temporalBasis;
        public bool confirmed;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Deep.Look(ref culture, "culture");
            Scribe_Deep.Look(ref politicalBeliefs, "politicalBeliefs");
            Scribe_Deep.Look(ref technologicalKnowledge,
                "technologicalKnowledge");
            Scribe_Deep.Look(ref arrangement, "arrangement");
            Scribe_Values.Look(ref arrangementSource, "arrangementSource",
                (byte)CAAxisSource.Unset);
            Scribe_Values.Look(ref nativeIdeoId, "nativeIdeoId", -1);
            Scribe_Values.Look(ref nativeIdeoName, "nativeIdeoName");
            Scribe_Values.Look(ref nativeIdeoSignature,
                "nativeIdeoSignature");
            Scribe_Values.Look(ref nativeIdeoNotified,
                "nativeIdeoNotified", false);
            Scribe_Values.Look(ref establishedStart,
                "establishedStart", false);
            Scribe_Values.Look(ref temporalBasis, "temporalBasis");
            Scribe_Values.Look(ref confirmed, "confirmed", false);
            // The campaign owner validates and, when supported, migrates this
            // complete nested record. Loading does not repair collections or
            // stamp an unknown nested version current.
        }

        internal CAAxisSource ArrangementSource
        {
            get { return (CAAxisSource)arrangementSource; }
        }

        internal bool ArrangementChosen
        {
            get
            {
                return arrangement != null
                    && ArrangementSource != CAAxisSource.Unset;
            }
        }

        internal CAPlayerFoundingPlan Copy()
        {
            return new CAPlayerFoundingPlan
            {
                schemaVersion = CurrentSchemaVersion,
                culture = culture?.Copy() ?? new CACulture(),
                politicalBeliefs = politicalBeliefs?.Copy()
                    ?? new CAPoliticalBeliefs(),
                technologicalKnowledge = technologicalKnowledge?.Copy()
                    ?? new CATechnologicalKnowledge(),
                arrangement = arrangement?.Copy(),
                arrangementSource = arrangementSource,
                nativeIdeoId = nativeIdeoId,
                nativeIdeoName = nativeIdeoName,
                nativeIdeoSignature = nativeIdeoSignature,
                nativeIdeoNotified = nativeIdeoNotified,
                establishedStart = establishedStart,
                temporalBasis = temporalBasis,
                confirmed = confirmed
            };
        }
    }

    // The founding plan belongs to the generated world, not to regional
    // geography. Regional setup keeps a copy so its external fixture remains
    // complete, while this component also covers ordinary and forced-map
    // starts. appliedAtTick is the durable one-shot receipt: founding terms
    // may establish the colony once, but may never be replayed over the
    // institutions that subsequently develop through play.
    public sealed class CAPlayerFoundingWorldComponent : WorldComponent
    {
        private int campaignSchemaVersion = 4;
        private int legacyAuthoringDataEpoch =
            CACampaignCompatibilityKernel.LegacyB10AuthoringEpoch;
        private CAPlayerFoundingPlan founding =
            new CAPlayerFoundingPlan();
        private int appliedAtTick = -1;

        public CAPlayerFoundingWorldComponent(World world) : base(world) { }

        internal static CAPlayerFoundingWorldComponent Current
        {
            get
            {
                return Find.World
                    ?.GetComponent<CAPlayerFoundingWorldComponent>();
            }
        }

        internal CAPlayerFoundingPlan Founding
        {
            get
            {
                if (founding == null)
                    founding = new CAPlayerFoundingPlan();
                return founding;
            }
        }

        internal CAPlayerFoundingPlan FoundingOrNull => founding;

        internal bool Applied
        {
            get { return appliedAtTick >= 0; }
        }

        internal int AppliedAtTick
        {
            get { return appliedAtTick; }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref campaignSchemaVersion,
                "CA_playerFoundingSchemaVersion", 0);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref legacyAuthoringDataEpoch,
                    "CA_authoringDataEpoch", 0);
            bool readable = CACampaignCompatibility.ShouldReadLiveState(
                "world.player-founding", campaignSchemaVersion,
                legacyAuthoringDataEpoch);
            if (readable)
            {
                // The founding plan is created lazily; a save before
                // anything touched it must still carry the object the
                // preflight requires.
                if (Scribe.mode == LoadSaveMode.Saving && founding == null)
                    founding = new CAPlayerFoundingPlan();
                Scribe_Deep.Look(ref founding, "CA_playerFounding");
                Scribe_Values.Look(ref appliedAtTick,
                    "CA_playerFoundingAppliedAtTick", -1,
                    forceSave: true);
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && readable)
            {
                CACampaignCompatibility.CompleteOwnerLoad(
                    "world.player-founding", ref campaignSchemaVersion,
                    legacyAuthoringDataEpoch, ValidateCampaignState,
                    MigrateSupportedState);
            }
            base.ExposeData();
        }

        private string ValidateCampaignState()
        {
            return CAPlayerFoundingModel.ValidationFailure(founding,
                requireConfirmed: true);
        }

        private string MigrateSupportedState()
        {
            if (campaignSchemaVersion != 2
                && campaignSchemaVersion != 3)
                return "player-founding owner schema "
                    + campaignSchemaVersion + " has no supported migration";
            if (founding == null) return "founding plan is missing";
            int expectedFoundingSchema = campaignSchemaVersion == 2 ? 3 : 4;
            if (founding.schemaVersion != expectedFoundingSchema)
                return "founding plan schema is " + founding.schemaVersion
                    + ", expected " + expectedFoundingSchema;
            if (!CACultureModel.TryUpgradeToCurrent(founding.culture,
                    out CACulture culture, out string cultureFailure))
                return "founding Culture: " + cultureFailure;
            if (!CAPoliticalBeliefsModel.TryUpgradeFromB10(
                    founding.politicalBeliefs,
                    out CAPoliticalBeliefs beliefs,
                    out string beliefFailure))
                return "founding Political Order: " + beliefFailure;
            CAPlayerFoundingPlan candidate = founding.Copy();
            candidate.culture = culture;
            candidate.politicalBeliefs = beliefs;
            Faction player = Find.World?.factionManager?.OfPlayer;
            CATechnologicalKnowledgeModel.SeedFromEngineTemplate(
                candidate.technologicalKnowledge,
                player?.def,
                "player-founding:technology");
            CATechnologicalKnowledgeModel.Ensure(
                candidate.technologicalKnowledge,
                "player-founding:technology");
            candidate.schemaVersion = CAPlayerFoundingPlan.CurrentSchemaVersion;
            string candidateFailure = CAPlayerFoundingModel.ValidationFailure(
                candidate, requireConfirmed: true);
            if (!candidateFailure.NullOrEmpty())
                return candidateFailure;
            founding = candidate;
            return null;
        }

        internal void Stage(CAPlayerFoundingPlan draft)
        {
            // The world component owns the live non-regional draft. Keep that
            // object stable while the founding page and native Ideoligion
            // editors exchange control; replacing it here would leave the
            // visible page editing a detached copy. Regional plans cross an
            // ownership boundary, so their draft is copied into world state.
            if (draft == null)
                founding = new CAPlayerFoundingPlan();
            else if (!ReferenceEquals(founding, draft))
                founding = draft.Copy();
            appliedAtTick = -1;
        }

        internal void MarkApplied(int tick)
        {
            appliedAtTick = Math.Max(0, tick);
        }
    }

    internal static class CAPlayerFoundingSession
    {
        private static CAPlayerFoundingPlan fallback;
        private static string fallbackWorld;

        internal static CAPlayerFoundingPlan Current
        {
            get
            {
                CARegionalPlan plan = CARegionalSetupSession
                    .PendingForCurrentWorld;
                if (plan != null)
                {
                    if (plan.playerFounding == null)
                        plan.playerFounding = new CAPlayerFoundingPlan();
                    return plan.playerFounding;
                }

                CAPlayerFoundingWorldComponent component =
                    CAPlayerFoundingWorldComponent.Current;
                if (component != null) return component.Founding;

                string identity = WorldIdentity();
                if (fallback == null || fallbackWorld != identity)
                {
                    fallback = new CAPlayerFoundingPlan();
                    fallbackWorld = identity;
                }
                return fallback;
            }
        }

        // Inspection and diagnostics use this path so opening a census cannot
        // create a regional plan, world draft, or fallback state.
        internal static CAPlayerFoundingPlan CurrentOrNull()
        {
            CAPlayerFoundingPlan pending = CARegionalSetupSession
                .CurrentPlanOrNull()?.playerFounding;
            if (pending != null) return pending;
            CAPlayerFoundingPlan world = CAPlayerFoundingWorldComponent.Current
                ?.FoundingOrNull;
            if (world != null) return world;
            return fallbackWorld == WorldIdentity() ? fallback : null;
        }

        internal static void Save()
        {
            CARegionalPlan regional = CARegionalSetupSession
                .PendingForCurrentWorld;
            CAPlayerFoundingPlan staged = regional?.playerFounding
                ?? CAPlayerFoundingWorldComponent.Current?.Founding
                ?? fallback;
            if (staged != null)
                CAPlayerFoundingWorldComponent.Current?.Stage(staged);
            if (regional != null)
                CARegionalSetupSession.SavePending();
        }

        internal static void Confirm(CAPlayerFoundingPlan draft)
        {
            if (draft == null) return;
            draft.confirmed = true;
            CAPlayerFoundingWorldComponent.Current?.Stage(draft);
            if (CARegionalSetupSession.PendingForCurrentWorld != null)
                CARegionalSetupSession.SavePending();
        }

        internal static CAPlayerFoundingPlan ConfirmedForRuntime()
        {
            CAPlayerFoundingPlan world =
                CAPlayerFoundingWorldComponent.Current?.Founding;
            if (world?.confirmed == true) return world;

            CAPlayerFoundingPlan pending = CARegionalSetupSession
                .CurrentPlanOrNull()?.playerFounding;
            if (pending?.confirmed == true) return pending;

            Map map = Find.CurrentMap;
            CARegionalPlan registered = CARegionalWorldComponent.Current
                ?.FindRegionForMap(map);
            return registered?.playerFounding?.confirmed == true
                ? registered.playerFounding : null;
        }

        internal static string WorldIdentity()
        {
            var info = Find.World?.info;
            return info == null ? "no-world" : info.seedString + "|"
                + info.planetCoverage + "|" + info.name;
        }
    }

    internal static class CAPlayerFoundingModel
    {
        internal static string ValidationFailure(CAPlayerFoundingPlan draft,
            bool requireConfirmed)
        {
            if (draft == null) return "founding plan is missing";
            if (draft.schemaVersion != CAPlayerFoundingPlan.CurrentSchemaVersion)
                return "founding plan schema is " + draft.schemaVersion
                    + ", expected " + CAPlayerFoundingPlan.CurrentSchemaVersion;
            string cultureFailure = CACultureModel.ValidationFailure(
                draft.culture, requireSubstantive: true);
            if (!cultureFailure.NullOrEmpty())
                return "founding Culture: " + cultureFailure;
            string beliefFailure = CAPoliticalBeliefsModel.ValidationFailure(
                draft.politicalBeliefs, allowExactLegacy: false);
            if (!beliefFailure.NullOrEmpty())
                return "founding Political Order: " + beliefFailure;
            string technologyFailure = CATechnologicalKnowledgeModel
                .ValidationFailure(draft.technologicalKnowledge);
            if (!technologyFailure.NullOrEmpty())
                return "founding Technological Knowledge: "
                    + technologyFailure;
            if (draft.arrangementSource > (byte)CAAxisSource.Authored)
                return "founding arrangement source is invalid";
            if (draft.arrangement == null)
            {
                if (draft.ArrangementSource != CAAxisSource.Unset)
                    return "founding arrangement source has no arrangement";
                if (requireConfirmed || draft.confirmed)
                    return "confirmed founding arrangement is missing";
            }
            else
            {
                if (draft.ArrangementSource == CAAxisSource.Unset)
                    return "founding arrangement has no source";
                if (draft.arrangement.id.NullOrEmpty()
                    || draft.arrangement.label.NullOrEmpty()
                    || draft.arrangement.premise.NullOrEmpty())
                    return "founding arrangement identity is incomplete";
                if (draft.arrangement.leaderRule != "none"
                    && draft.arrangement.leaderRule != "chosen")
                    return "founding leadership rule is unsupported";
                if (draft.arrangement.leaderRule == "none"
                    && !draft.arrangement.foundersDecide)
                    return "founding arrangement has no decision authority";
                if (draft.arrangement.durationDays < -1)
                    return "founding duration is invalid";
            }
            if (draft.temporalBasis.NullOrEmpty())
                return "founding temporal basis is missing";
            if (requireConfirmed && !draft.confirmed)
                return "founding plan is not confirmed";
            if (draft.nativeIdeoId >= 0
                && (draft.nativeIdeoName.NullOrEmpty()
                    || draft.nativeIdeoSignature.NullOrEmpty()))
                return "native Ideoligion receipt is incomplete";
            return null;
        }

        internal static void Ensure(CAPlayerFoundingPlan draft)
        {
            if (draft == null) return;
            if (draft.culture == null) draft.culture = new CACulture();
            if (draft.politicalBeliefs == null)
                draft.politicalBeliefs = new CAPoliticalBeliefs();
            if (draft.technologicalKnowledge == null)
                draft.technologicalKnowledge =
                    new CATechnologicalKnowledge();
            draft.schemaVersion = CAPlayerFoundingPlan.CurrentSchemaVersion;
            if (draft.temporalBasis.NullOrEmpty())
                DetermineTemporalBoundary(draft);

            string seed = Seed;
            if (draft.culture.name.NullOrEmpty())
                draft.culture.name = "Founders' culture";
            CACultureModel.EnsureIdentity(draft.culture,
                seed + ":culture");
            CACultureModel.SynchronizeOwnIdentityLabel(draft.culture);
            CAPoliticalBeliefsModel.Ensure(draft.politicalBeliefs,
                seed + ":politics");
            CATechnologicalKnowledgeModel.SeedFromEngineTemplate(
                draft.technologicalKnowledge,
                Faction.OfPlayer?.def ?? FactionDefOf.PlayerColony,
                seed + ":technology");
            CATechnologicalKnowledgeModel.Ensure(
                draft.technologicalKnowledge, seed + ":technology");
            // Founding terms are authored or absent: the operator chooses
            // terms or a preset, and nothing is generated here. A draft
            // without terms lands without terms (the blank arrangement
            // fallback), and an already-authored arrangement is kept
            // byte-for-byte.
            CaptureNativeIdeo(draft, NativeIdeo, null);
        }

        internal static string Seed
        {
            get { return CAPlayerFoundingSession.WorldIdentity() + ":player"; }
        }

        internal static CultureDef PlayerCultureDef()
        {
            Ideo ideo = NativeIdeo;
            if (ideo?.culture != null) return ideo.culture;
            return CAFactionStartingState.DefaultCulture(Faction.OfPlayer,
                ideo);
        }

        internal static Ideo NativeIdeo
        {
            get { return Faction.OfPlayer?.ideos?.PrimaryIdeo; }
        }

        internal static void CaptureNativeIdeo(CAPlayerFoundingPlan draft,
            Ideo ideo, bool? notified)
        {
            if (draft == null) return;
            if (ideo == null)
            {
                draft.nativeIdeoId = -1;
                draft.nativeIdeoName = null;
                draft.nativeIdeoSignature = null;
                draft.nativeIdeoNotified = false;
                return;
            }
            string signature = NativeIdeoSignature(ideo);
            bool changed = draft.nativeIdeoId != ideo.id
                || draft.nativeIdeoSignature != signature;
            draft.nativeIdeoId = ideo.id;
            draft.nativeIdeoName = ideo.name;
            draft.nativeIdeoSignature = signature;
            if (changed) draft.confirmed = false;
            if (notified.HasValue)
                draft.nativeIdeoNotified = notified.Value;
            else if (changed)
                draft.nativeIdeoNotified = false;
        }

        // Native Ideoligion editing mutates the same Ideo object. Its load ID
        // therefore cannot prove that Scenario.PostIdeoChosen observed the
        // current content. The signature follows both the native revision and
        // the content that can affect generated founders.
        internal static string NativeIdeoSignature(Ideo ideo)
        {
            if (ideo == null) return null;
            var parts = new List<string>
            {
                ideo.id.ToString(),
                ideo.currentCacheId.ToString(),
                ideo.culture?.defName ?? "",
                ideo.Fluid ? "fluid" : "fixed",
                ideo.classicMode ? "classic" : "active",
                ideo.classicExtraMode ? "classic-extra" : "standard",
                ideo.foundation?.GetType().FullName ?? "",
                ideo.name ?? "",
                ideo.adjective ?? "",
                ideo.memberName ?? "",
                ideo.iconDef?.defName ?? "",
                ideo.colorDef?.defName ?? ""
            };
            parts.AddRange((ideo.memes ?? new List<MemeDef>())
                .Where(meme => meme != null)
                .Select(meme => "m:" + meme.defName)
                .OrderBy(value => value));
            foreach (Precept precept in ideo.PreceptsListForReading
                .Where(item => item != null)
                .OrderBy(item => item.def?.defName)
                .ThenBy(item => item.Id))
            {
                parts.Add("p:" + precept.GetType().FullName + ":"
                    + (precept.def?.defName ?? "") + ":" + precept.Id
                    + ":" + (precept.Label ?? ""));
                if (precept is Precept_Xenotype xenotype)
                {
                    parts.Add("x:" + (xenotype.xenotype?.defName ?? "")
                        + ":" + (xenotype.customXenotype?.name ?? "")
                        + ":" + (xenotype.customXenotype?.inheritable == true
                            ? "inheritable" : "noninheritable"));
                    if (xenotype.customXenotype?.genes != null)
                        parts.AddRange(xenotype.customXenotype.genes
                            .Where(gene => gene != null)
                            .Select(gene => "g:" + gene.defName)
                            .OrderBy(value => value));
                }
            }
            return string.Join("|", parts.Select(value => value.Length
                + ":" + value));
        }

        internal static void AssignNativeIdeo(CAPlayerFoundingPlan draft,
            Ideo ideo)
        {
            if (ideo == null || Faction.OfPlayer?.ideos == null
                || Find.IdeoManager == null) return;
            foreach (Ideo item in Find.IdeoManager.IdeosListForReading)
                if (item != null) item.initialPlayerIdeo = false;
            if (!Find.IdeoManager.IdeosListForReading.Contains(ideo))
                Find.IdeoManager.Add(ideo);
            Faction.OfPlayer.ideos.SetPrimary(ideo);
            ideo.initialPlayerIdeo = true;
            Find.IdeoManager.classicMode = false;
            Find.IdeoManager.RemoveUnusedStartingIdeos();
            CaptureNativeIdeo(draft, ideo, false);
        }

        internal static void UseSuggestedArrangement(
            CAPlayerFoundingPlan draft)
        {
            if (draft == null) return;
            CAFoundingArrangement situational = CAFoundingArrangements
                .DefaultFor(StartingPawnCount() == 1, AncestralGroup());
            draft.arrangement = CAPoliticalBeliefPractice.ShapeDefault(
                draft.politicalBeliefs, null, situational)?.Copy();
            draft.arrangementSource = (byte)CAAxisSource.Generated;
            draft.confirmed = false;
        }

        internal static void ChooseArrangement(CAPlayerFoundingPlan draft,
            CAFoundingArrangement arrangement)
        {
            if (draft == null || arrangement == null) return;
            draft.arrangement = arrangement.Copy();
            draft.arrangementSource = (byte)CAAxisSource.Authored;
            draft.confirmed = false;
        }

        internal static void MarkArrangementAuthored(
            CAPlayerFoundingPlan draft)
        {
            if (draft == null) return;
            if (draft.arrangement != null)
            {
                draft.arrangement.id = "custom";
                draft.arrangement.label = "custom founding terms";
                draft.arrangement.premise = "Rules chosen for this landing.";
            }
            draft.arrangementSource = (byte)CAAxisSource.Authored;
            draft.confirmed = false;
        }

        internal static int StartingPawnCount()
        {
            // The generated pawn set is the truth wherever it exists. A
            // scenario's configured total is only a premise until the pawn
            // set is generated, and customization systems (for example
            // Prepare Carefully) change the population there; consumers
            // reading after generation must see the actual current set,
            // never the presupposed count.
            int generated = Find.GameInitData?.startingAndOptionalPawns
                ?.Count ?? -1;
            if (generated > 0) return generated;

            int initialized = Find.GameInitData?.startingPawnCount ?? -1;
            if (initialized > 0) return initialized;

            // The founding page precedes RimWorld's starting-pawn page, so
            // PostIdeoChosen has not necessarily initialized GameInitData yet.
            // Read the scenario's own configured total without generating any
            // pawns or prematurely notifying the scenario.
            int configured = -1;
            foreach (ScenPart part in Find.Scenario?.AllParts
                ?? Enumerable.Empty<ScenPart>())
            {
                if (!(part is ScenPart_ConfigPage_ConfigureStartingPawnsBase))
                    continue;
                try
                {
                    object value = AccessTools.Property(part.GetType(),
                        "TotalPawnCount")?.GetValue(part, null);
                    if (value is int count && count > 0)
                        configured = count;
                }
                catch { }
            }
            return configured > 0 ? configured : 3;
        }

        internal static bool AncestralGroup()
        {
            if (ArrivedViolently()) return false;
            return StartingPawnCount() >= 4
                && CATechnologicalKnowledgeModel.CompatibilityTier(
                    CAPlayerFoundingSession.Current
                        ?.technologicalKnowledge) == 0;
        }

        private static void DetermineTemporalBoundary(
            CAPlayerFoundingPlan draft)
        {
            draft.establishedStart = false;
            draft.temporalBasis = "The scenario begins with founders at a "
                + "new landing; no prior player settlement history is "
                + "represented.";
            foreach (ScenPart part in Find.Scenario?.AllParts
                ?? Enumerable.Empty<ScenPart>())
            {
                ICAEstablishedPlayerStart established = part
                    as ICAEstablishedPlayerStart;
                if (established == null) continue;
                draft.establishedStart = true;
                draft.temporalBasis = established.CATemporalBasis.NullOrEmpty()
                    ? "The scenario explicitly supplies a pre-existing player "
                        + "settlement."
                    : established.CATemporalBasis;
                break;
            }
        }

        internal static bool ArrivedViolently()
        {
            if (Find.Scenario == null) return false;
            foreach (ScenPart part in Find.Scenario.AllParts)
            {
                var arrive = part as ScenPart_PlayerPawnsArriveMethod;
                if (arrive == null) continue;
                PlayerPawnsArriveMethod method = Traverse.Create(arrive)
                    .Field("method").GetValue<PlayerPawnsArriveMethod>();
                if (method != PlayerPawnsArriveMethod.Standing) return true;
            }
            return false;
        }

        internal static string StartingContextTitle()
        {
            return "Starting arrangements";
        }

        internal static string StartingContextSummary()
        {
            int count = StartingPawnCount();
            if (AncestralGroup())
                return "An ancestral group of " + count
                    + " brings inherited traditions and chooses the rules "
                    + "in force at the new settlement.";
            if (count == 1)
                return ArrivedViolently()
                    ? "One founder arrives violently with carried beliefs and adopts immediate survival rules."
                    : "One founder arrives with carried beliefs and chooses the settlement's first rules.";
            return ArrivedViolently()
                ? count + " founders arrive violently with carried traditions and adopt immediate landing rules."
                : count + " founders arrive with carried traditions and choose the rules in force at landing.";
        }

        internal static bool TryValidate(CAPlayerFoundingPlan draft,
            out string failure)
        {
            failure = null;
            if (draft == null)
            {
                failure = "The founding plan is unavailable.";
                return false;
            }
            if (draft.culture == null || draft.culture.id.NullOrEmpty())
            {
                failure = "Set the founders' culture.";
                return false;
            }
            string cultureFailure = CACultureModel.SubstantiveFailure(
                draft.culture);
            if (!cultureFailure.NullOrEmpty())
            {
                failure = "The founders' culture cannot be used: "
                    + cultureFailure;
                return false;
            }
            if (draft.politicalBeliefs == null)
            {
                failure = "The founders' political-belief record is unavailable.";
                return false;
            }
            string beliefFailure = CAPoliticalBeliefsModel.ValidationFailure(
                draft.politicalBeliefs, allowExactLegacy: false);
            if (!beliefFailure.NullOrEmpty())
            {
                failure = "The founders' Political Order cannot be used: "
                    + beliefFailure;
                return false;
            }
            string knowledgeFailure = CATechnologicalKnowledgeModel
                .ValidationFailure(draft.technologicalKnowledge);
            if (!knowledgeFailure.NullOrEmpty())
            {
                failure = "The founders' Technological Knowledge cannot be "
                    + "used: " + knowledgeFailure;
                return false;
            }
            CAFoundingArrangement arrangement = draft.arrangement;
            if (arrangement == null)
            {
                failure = "Choose or generate founding terms.";
                return false;
            }
            if (!draft.ArrangementChosen)
            {
                failure = "Choose or generate founding terms.";
                return false;
            }
            if (arrangement.leaderRule != "none"
                && arrangement.leaderRule != "chosen")
            {
                failure = "The founding terms have an unknown "
                    + "leadership rule.";
                return false;
            }
            if (arrangement.leaderRule == "none"
                && !arrangement.foundersDecide)
            {
                failure = "Choose a leader or let the founders decide.";
                return false;
            }
            if (ModsConfig.IdeologyActive && NativeIdeo == null)
            {
                failure = "Choose or generate an Ideoligion.";
                return false;
            }
            if (ModsConfig.IdeologyActive
                && !TryValidateNativeIdeo(NativeIdeo, out failure))
                return false;
            if (ModsConfig.IdeologyActive
                && draft.nativeIdeoId != NativeIdeo.id)
            {
                failure = "The saved Ideoligion does not match the current "
                    + "founding choice.";
                return false;
            }
            if (ModsConfig.IdeologyActive
                && draft.nativeIdeoSignature
                    != NativeIdeoSignature(NativeIdeo))
            {
                failure = "The saved Ideoligion does not match the current "
                    + "founding choice.";
                return false;
            }
            return true;
        }

        // Page_ConfigureIdeo performs these checks inside CanDoNext, together
        // with the side effect that notifies the scenario and generates
        // starting pawns. Editor Back preserves live native edits, so the
        // coordinated founding page repeats only the structural checks before
        // it performs that notification itself.
        internal static bool TryValidateNativeIdeo(Ideo ideo,
            out string failure)
        {
            failure = null;
            if (ideo == null)
            {
                failure = "Choose or generate an Ideoligion.";
                return false;
            }
            if (ideo.name.NullOrEmpty())
            {
                failure = "Name the Ideoligion.";
                return false;
            }
            Pair<Precept, Precept> incompatible =
                ideo.FirstIncompatiblePreceptPair();
            if (incompatible != default(Pair<Precept, Precept>))
            {
                failure = "Resolve the incompatible Ideoligion precepts: "
                    + incompatible.First.Label + " and "
                    + incompatible.Second.Label + ".";
                return false;
            }
            Tuple<Precept_Ritual, List<string>> missingTarget =
                ideo.FirstRitualMissingTarget();
            if (missingTarget != null)
            {
                failure = "Set a target for "
                    + missingTarget.Item1.LabelCap + ": "
                    + missingTarget.Item2.ToCommaList().CapitalizeFirst()
                    + ".";
                return false;
            }
            Precept_Building missingRitual =
                ideo.FirstConsumableBuildingMissingRitual();
            if (missingRitual != null)
            {
                failure = "Add a ritual for " + missingRitual.LabelCap
                    + ".";
                return false;
            }
            return true;
        }

        // Culture, Political Order, and Technological Knowledge are carried by the founders
        // and may therefore be available to map generation. This deliberately
        // leaves factionStructure alone: it records realized institutions,
        // not the four narrower landing terms in the founding arrangement.
        internal static void ApplyCarriedState(CAPlayerFoundingPlan draft,
            Faction player)
        {
            if (draft?.confirmed != true || player == null) return;
            CAFactionState record = CAFactionStateWorldComponent.Current
                ?.EnsureFor(player);
            if (record == null) return;
            record.culture = draft.culture?.Copy() ?? new CACulture();
            record.politicalBeliefs = draft.politicalBeliefs?.Copy()
                ?? new CAPoliticalBeliefs();
            record.technologicalKnowledge =
                draft.technologicalKnowledge?.Copy()
                    ?? new CATechnologicalKnowledge();
            if (record.factionStructure == null)
                record.factionStructure = new List<CAAxisEntry>();
            record.origin = CAOrigin.Authored("player founding");
        }

        internal static string ArrangementSourceWords(
            CAPlayerFoundingPlan draft)
        {
            switch (draft?.ArrangementSource ?? CAAxisSource.Unset)
            {
                case CAAxisSource.Authored: return "Custom";
                case CAAxisSource.Generated: return "Suggested";
                default: return "Not set";
            }
        }
    }
}
