using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // The player authors a founding population, not an already mature NPC
    // faction. Culture, native Ideoligion, and political beliefs arrive with
    // the founders. The arrangement is what they establish at landing.
    public sealed class CAPlayerFoundingPlan : IExposable
    {
        public CACulture culture = new CACulture();
        public CAPoliticalBeliefs politicalBeliefs =
            new CAPoliticalBeliefs();
        public CAFoundingArrangement arrangement;
        public byte arrangementSource; // CAAxisSource
        public int nativeIdeoId = -1;
        public string nativeIdeoName;
        public string nativeIdeoSignature;
        public bool nativeIdeoNotified;
        public bool confirmed;

        public void ExposeData()
        {
            Scribe_Deep.Look(ref culture, "culture");
            Scribe_Deep.Look(ref politicalBeliefs, "politicalBeliefs");
            Scribe_Deep.Look(ref arrangement, "arrangement");
            Scribe_Values.Look(ref arrangementSource, "arrangementSource",
                (byte)CAAxisSource.Unset);
            Scribe_Values.Look(ref nativeIdeoId, "nativeIdeoId", -1);
            Scribe_Values.Look(ref nativeIdeoName, "nativeIdeoName");
            Scribe_Values.Look(ref nativeIdeoSignature,
                "nativeIdeoSignature");
            Scribe_Values.Look(ref nativeIdeoNotified,
                "nativeIdeoNotified", false);
            Scribe_Values.Look(ref confirmed, "confirmed", false);
            if (culture == null) culture = new CACulture();
            if (politicalBeliefs == null)
                politicalBeliefs = new CAPoliticalBeliefs();
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
                culture = culture?.Copy() ?? new CACulture(),
                politicalBeliefs = politicalBeliefs?.Copy()
                    ?? new CAPoliticalBeliefs(),
                arrangement = arrangement?.Copy(),
                arrangementSource = arrangementSource,
                nativeIdeoId = nativeIdeoId,
                nativeIdeoName = nativeIdeoName,
                nativeIdeoSignature = nativeIdeoSignature,
                nativeIdeoNotified = nativeIdeoNotified,
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
            Scribe_Deep.Look(ref founding, "CA_playerFounding");
            Scribe_Values.Look(ref appliedAtTick,
                "CA_playerFoundingAppliedAtTick", -1);
            if (founding == null) founding = new CAPlayerFoundingPlan();
            base.ExposeData();
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
        internal static void Ensure(CAPlayerFoundingPlan draft)
        {
            if (draft == null) return;
            if (draft.culture == null) draft.culture = new CACulture();
            if (draft.politicalBeliefs == null)
                draft.politicalBeliefs = new CAPoliticalBeliefs();

            string seed = Seed;
            CACultureModel.EnsureGenerated(draft.culture,
                seed + ":culture", PlayerCultureDef());
            CAPoliticalBeliefsModel.Ensure(draft.politicalBeliefs,
                seed + ":politics");
            CAPoliticalBeliefsModel.GenerateUnset(draft.politicalBeliefs,
                seed + ":politics");
            if (draft.arrangement == null
                || draft.ArrangementSource == CAAxisSource.Unset
                || draft.ArrangementSource == CAAxisSource.Generated)
                UseSuggestedArrangement(draft);
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
                .DefaultFor(StartingPawnCount() == 1, AncestralStart());
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
            draft.arrangementSource = (byte)CAAxisSource.Preset;
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

        internal static bool AncestralStart()
        {
            if (ArrivedViolently()) return false;
            return StartingPawnCount() >= 4
                && (Faction.OfPlayer?.def?.techLevel
                    ?? TechLevel.Industrial) <= TechLevel.Neolithic;
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
            if (AncestralStart())
                return "An ancestral group of " + count
                    + " brings mature traditions and chooses the rules in force at the new settlement.";
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
                failure = "Set the founders' cultural background.";
                return false;
            }
            string cultureFailure = CACultureModel.CompatibilityFailure(
                draft.culture);
            if (!cultureFailure.NullOrEmpty())
            {
                failure = "The founders' culture cannot be used: "
                    + cultureFailure;
                return false;
            }
            if (draft.politicalBeliefs == null
                || CAFactionAxes.CountByState(
                    draft.politicalBeliefs.positions,
                    CAAxisSource.Unset) > 0)
            {
                failure = "Set the founders' political beliefs or generate "
                    + "the remaining choices.";
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

        // Background and political beliefs are carried by the founders and
        // may therefore be available to map generation. This deliberately leaves
        // factionStructure alone: it records realized institutions, not the
        // four narrower landing terms in the founding arrangement.
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
                case CAAxisSource.Preset: return "Starting arrangement";
                case CAAxisSource.Generated: return "Suggested";
                default: return "Not set";
            }
        }
    }
}
