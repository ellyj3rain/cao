using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Pawn autonomy owns one typed initiative tier. The schema marker separates the
    // legacy four-value save representation from the current three-value ladder.
    public class AutonomyComponent : GameComponent
    {
        internal const int CurrentInitiativeSchema = 1;
        private Dictionary<int, CAInitiativeTier> tiers =
            new Dictionary<int, CAInitiativeTier>();
        private Dictionary<int, int> legacyLevels;
        private int initiativeSchema = CurrentInitiativeSchema;
        public static AutonomyComponent Instance;

        public AutonomyComponent(Game game) { Instance = this; }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref initiativeSchema, "CA_initiativeSchema", 0);
            if (Scribe.mode == LoadSaveMode.Saving
                || initiativeSchema >= CurrentInitiativeSchema)
            {
                Scribe_Collections.Look(ref tiers, "CA_initiativeTiers",
                    LookMode.Value, LookMode.Value);
            }
            else
            {
                Scribe_Collections.Look(ref legacyLevels, "CA_autonomyLevels",
                    LookMode.Value, LookMode.Value);
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (initiativeSchema < CurrentInitiativeSchema)
                {
                    tiers = new Dictionary<int, CAInitiativeTier>();
                    if (legacyLevels != null)
                    {
                        foreach (KeyValuePair<int, int> pair in legacyLevels)
                            tiers[pair.Key] = MigrateLegacyLevel(pair.Value);
                    }
                    legacyLevels = null;
                    initiativeSchema = CurrentInitiativeSchema;
                    CATrace.Log("initiative schema migrated: Directed and Standard -> Standard; Proactive and Autonomous retained");
                }
                NormalizeStoredTiers();
            }
            if (tiers == null)
                tiers = new Dictionary<int, CAInitiativeTier>();
            Instance = this;
        }

        public static CAInitiativeTier TierOf(Pawn p)
        {
            CAInitiativeTier fallback = AwarenessMod.Settings != null
                ? AwarenessMod.Settings.defaultInitiative
                : CAInitiativeTier.Standard;
            var inst = Instance;
            if (p == null || inst == null) return fallback;
            CAInitiativeTier tier;
            if (inst.tiers.TryGetValue(p.thingIDNumber, out tier))
                return Normalize(tier);
            return Normalize(fallback);
        }

        public static bool AtLeast(Pawn pawn, CAInitiativeTier minimum)
        {
            return TierOf(pawn) >= minimum;
        }

        public static void SetTier(Pawn pawn, CAInitiativeTier tier)
        {
            if (pawn == null || Instance == null) return;
            tier = Normalize(tier);
            if (TierOf(pawn) == tier
                && Instance.tiers.ContainsKey(pawn.thingIDNumber)) return;
            Instance.tiers[pawn.thingIDNumber] = tier;
            CABehaviorRevisions.AutonomyChanged(pawn);
            OperationalAccessComponent.NotifyAutonomyChanged(pawn);
        }

        public static CAInitiativeTier MigrateLegacyLevel(int legacy)
        {
            if (legacy >= 3) return CAInitiativeTier.Autonomous;
            if (legacy >= 2) return CAInitiativeTier.Proactive;
            return CAInitiativeTier.Standard;
        }

        public static CAInitiativeTier Normalize(CAInitiativeTier tier)
        {
            if (tier < CAInitiativeTier.Standard)
                return CAInitiativeTier.Standard;
            if (tier > CAInitiativeTier.Autonomous)
                return CAInitiativeTier.Autonomous;
            return tier;
        }

        private void NormalizeStoredTiers()
        {
            if (tiers == null) return;
            var keys = tiers.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
                tiers[keys[i]] = Normalize(tiers[keys[i]]);
        }
    }

    // The one context menu for a colonist's awareness setup - shared by the colonist-bar
    // portraits and the Squads board.
    public static class AwarenessPawnMenu
    {
        public static void Open(Pawn colonist)
        {
            if (colonist == null) return;
            var opts = new List<FloatMenuOption>();
            for (int i = 0; i < CAInitiativePresentation.ActiveTiers.Length; i++)
            {
                CAInitiativeTier tier = CAInitiativePresentation.ActiveTiers[i];
                string label = (AutonomyComponent.TierOf(colonist) == tier
                    ? "* " : "") + "Initiative: "
                    + CAInitiativePresentation.Label(tier);
                opts.Add(new FloatMenuOption(label,
                    delegate { AutonomyComponent.SetTier(colonist, tier); }));
            }
            opts.Add(new FloatMenuOption("Behavior scope...",
                delegate { Find.WindowStack.Add(new CABehaviorScopeWindow(colonist)); }));
            int cur = SquadComponent.SquadOf(colonist);
            for (int i = 1; i <= 4; i++)
            {
                int sq = i;
                string label = (cur == sq ? "* " : "") + "Squad " + sq;
                opts.Add(new FloatMenuOption(label, delegate { SquadComponent.Assign(colonist, sq); }));
            }
            if (cur > 0)
            {
                bool isLead = SquadComponent.IsLeader(colonist);
                opts.Add(new FloatMenuOption(isLead ? "* Squad leader" : "Make squad leader",
                    delegate { SquadComponent.SetLeader(colonist); }));
                int ft = SquadComponent.FireteamOf(colonist);
                opts.Add(new FloatMenuOption((ft == 1 ? "* " : "") + "Fire team A", delegate { SquadComponent.SetFireteam(colonist, 1); }));
                opts.Add(new FloatMenuOption((ft == 2 ? "* " : "") + "Fire team B", delegate { SquadComponent.SetFireteam(colonist, 2); }));
                if (ft > 0)
                {
                    bool ftLead = SquadComponent.IsFireteamLeader(colonist);
                    opts.Add(new FloatMenuOption(ftLead ? "* Fire team leader" : "Make fire team leader",
                        delegate { SquadComponent.SetFireteamLeader(colonist); }));
                    opts.Add(new FloatMenuOption("No fire team", delegate { SquadComponent.SetFireteam(colonist, 0); }));
                }
                opts.Add(new FloatMenuOption("Leave squad", delegate { SquadComponent.Leave(colonist); }));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }
    }

    public class Command_Autonomy : Command_Action
    {
        public Pawn pawn;

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get
            {
                for (int i = 0;
                    i < CAInitiativePresentation.ActiveTiers.Length; i++)
                {
                    CAInitiativeTier tier =
                        CAInitiativePresentation.ActiveTiers[i];
                    string label = (AutonomyComponent.TierOf(pawn) == tier
                        ? "* " : "") + CAInitiativePresentation.Label(tier);
                    yield return new FloatMenuOption(label,
                        delegate { AutonomyComponent.SetTier(pawn, tier); });
                }
                yield return new FloatMenuOption("Behavior scope...",
                    delegate { Find.WindowStack.Add(new CABehaviorScopeWindow(pawn)); });
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_AutonomyGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn __instance)
        {
            foreach (var g in gizmos) yield return g;
            var p = __instance;
            if (p == null || !p.IsColonistPlayerControlled) yield break;
            CAInitiativeTier tier = AutonomyComponent.TierOf(p);
            CAInformationDetail detail = AwarenessMod.Settings != null
                ? AwarenessMod.Settings.informationDetail
                : CAInformationDetail.Standard;
            yield return new Command_Autonomy
            {
                pawn = p,
                defaultLabel = "Initiative: "
                    + CAInitiativePresentation.Label(tier),
                defaultDesc = CAInitiativePresentation.Description(tier,
                    detail, p)
                    + "\n\nLeft-click cycles. Right-click chooses a tier or opens Behavior scope.",
                icon = TexCommand.HoldOpen,
                action = delegate
                {
                    int next = ((int)AutonomyComponent.TierOf(p) + 1)
                        % CAInitiativePresentation.ActiveTiers.Length;
                    AutonomyComponent.SetTier(p,
                        CAInitiativePresentation.ActiveTiers[next]);
                }
            };
        }
    }

    internal sealed class CABehaviorScopeWindow : Window
    {
        private readonly Pawn pawn;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(760f, 720f);

        public CABehaviorScopeWindow(Pawn pawn)
        {
            this.pawn = pawn;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (pawn == null)
            {
                Widgets.Label(inRect, "No pawn selected.");
                return;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f),
                pawn.LabelShort + " - behavior scope");
            Text.Font = GameFont.Small;
            Rect outer = new Rect(inRect.x, inRect.y + 42f, inRect.width,
                inRect.height - 42f);
            string text = BuildText();
            float height = Text.CalcHeight(text, outer.width - 20f) + 12f;
            Rect view = new Rect(0f, 0f, outer.width - 16f,
                Mathf.Max(outer.height, height));
            Widgets.BeginScrollView(outer, ref scroll, view);
            Widgets.Label(new Rect(0f, 0f, view.width, height), text);
            Widgets.EndScrollView();
        }

        private string BuildText()
        {
            CAEffectiveBehaviorProfile profile =
                CAEffectiveBehaviorProfileCache.Of(pawn);
            CAInitiativeTier tier = AutonomyComponent.TierOf(pawn);
            CAInformationDetail detail = AwarenessMod.Settings != null
                ? AwarenessMod.Settings.informationDetail
                : CAInformationDetail.Standard;
            var text = new StringBuilder();
            text.Append("Initiative: ").Append(
                CAInitiativePresentation.Label(tier)).Append("\n")
                .Append(CAInitiativePresentation.Description(tier, detail,
                    pawn)).Append("\n\n")
                .Append("Squad position: ").Append(profile?.Role ?? "none")
                .Append("\n");

            CAIntentContext intent;
            if (CATactical.TryGetContext(pawn, out intent) && intent.IsValid)
            {
                text.Append("Active intent: ").Append(intent.BehaviorKey)
                    .Append("; episode ").Append(intent.EpisodeId)
                    .Append("; origin ").Append(intent.Origin)
                    .Append("; controller ").Append(intent.Controller)
                    .Append("\n");
            }
            else if (CABehaviorIntentMapComponent.For(pawn.Map)?.TryGet(
                pawn, pawn.CurJob, out intent) == true && intent.IsValid)
            {
                text.Append("Active intent: ").Append(intent.BehaviorKey)
                    .Append("; episode ").Append(intent.EpisodeId)
                    .Append("; authority ")
                    .Append(intent.AuthorityIdentity ?? "unspecified")
                    .Append("\n");
            }
            else text.Append("Active intent: none\n");

            CASpatialInitiativeMapComponent spatial =
                CASpatialInitiativeMapComponent.For(pawn.Map);
            text.Append("Spatial authority: ")
                .Append(spatial != null
                    ? "map programs retain their own initiative ceilings"
                    : "none on this map")
                .Append("\n\nEnabled domains\n");

            CABehaviorDefinition[] definitions = profile?.Definitions()
                .Where(d => d.ExposeInUi).ToArray()
                ?? Array.Empty<CABehaviorDefinition>();
            foreach (IGrouping<CABehaviorDomain, CABehaviorDefinition> group
                in definitions.GroupBy(d => d.Domain))
            {
                text.Append("- ")
                    .Append(CAInitiativePresentation.DomainLabel(group.Key))
                    .Append(": ")
                    .Append(string.Join(", ", group.Select(d => d.Label)
                        .ToArray()))
                    .Append("\n");
            }

            CABehaviorDefinition blocked = CABehaviorCatalog.All.FirstOrDefault(
                d => d.ExposeInUi && d.AppliesTo(CAActorContext.PlayerPawn)
                    && CABehaviorSettings.IsEnabled(d,
                        AwarenessMod.Settings)
                    && !d.InitiativeIndependent
                    && d.MinimumInitiative > tier);
            if (blocked != null)
            {
                text.Append("\nTier boundary example\n")
                    .Append(blocked.Label).Append(": ")
                    .Append("initiative tier is too low. Requires ")
                    .Append(CAInitiativePresentation.Label(
                        blocked.MinimumInitiative)).Append(".\n");
            }
            text.Append("\nPermissions, initiative, authority, knowledge, capability, and material feasibility are evaluated independently.");
            return text.ToString();
        }
    }

    // Right-click a portrait in the colonist bar: the shared awareness menu.
    public static class PortraitMenuPatch
    {
        public static void TryInstall(Harmony harmony)
        {
            try
            {
                var target = AccessTools.Method(typeof(ColonistBarColonistDrawer), "HandleClicks");
                if (target == null) { Log.Message("[Colonist Awareness] portrait menu: HandleClicks not found, skipping"); return; }
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(PortraitMenuPatch), "Prefix"));
            }
            catch (System.Exception e)
            {
                Log.Message("[Colonist Awareness] portrait menu unavailable: " + e.Message);
            }
        }

        // Vanilla binds right-button DRAG on the bar to portrait reordering, so the
        // menu opens on right-button RELEASE without a drag: MouseDown flows through
        // to the reorder widget untouched, and a release near where it was pressed
        // means a click, not a drag.
        private static UnityEngine.Vector2 downAt = new UnityEngine.Vector2(-1000f, -1000f);

        public static bool Prefix(UnityEngine.Rect rect, Pawn colonist)
        {
            var ev = UnityEngine.Event.current;
            if (ev == null || ev.button != 1) return true;
            if (colonist == null || !colonist.IsColonistPlayerControlled) return true;
            if (ev.type == UnityEngine.EventType.MouseDown && rect.Contains(ev.mousePosition))
            {
                downAt = ev.mousePosition; // remember, but let vanilla see it (reorder)
                return true;
            }
            if (ev.type == UnityEngine.EventType.MouseUp && rect.Contains(ev.mousePosition)
                && (ev.mousePosition - downAt).sqrMagnitude <= 25f)
            {
                downAt = new UnityEngine.Vector2(-1000f, -1000f);
                AwarenessPawnMenu.Open(colonist);
                ev.Use();
                return false;
            }
            return true;
        }
    }
}
