using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Module 5: the master dial. Every colonist carries an autonomy level; every awareness
    // behavior consults it. 0 Directed, 1 Standard, 2 Proactive, 3 Autonomous.
    public class AutonomyComponent : GameComponent
    {
        private Dictionary<int, int> levels = new Dictionary<int, int>();
        public static AutonomyComponent Instance;

        public AutonomyComponent(Game game) { Instance = this; }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref levels, "CA_autonomyLevels", LookMode.Value, LookMode.Value);
            if (levels == null) levels = new Dictionary<int, int>();
            Instance = this;
        }

        public static int LevelOf(Pawn p)
        {
            int def = AwarenessMod.Settings != null ? AwarenessMod.Settings.defaultAutonomy : 1;
            var inst = Instance;
            if (p == null || inst == null) return def;
            int v;
            if (inst.levels.TryGetValue(p.thingIDNumber, out v)) return v;
            return def;
        }

        public static void SetLevel(Pawn p, int v)
        {
            if (p == null || Instance == null) return;
            Instance.levels[p.thingIDNumber] = v;
            OperationalAccessComponent.NotifyAutonomyChanged(p);
        }

        public static readonly string[] LevelNames = { "Directed", "Standard", "Proactive", "Autonomous" };
        public static readonly string[] LevelDescs =
        {
            "Directed: you control everything. Only the survival floor runs on its own - eat smart, life safety, criticality. No tactical behavior self-initiates. Your explicit orders are absolute and persistent.",
            "Standard: the survival floor plus normal game behavior - works, rests, and reacts like a vanilla colonist. Explicit orders still commit fully.",
            "Proactive: broader initiative with buffers before risky action; rescues downed outsiders and responds to known danger. With Operational access enabled, the colony keeps ordinary visible non-quest items allowed, while a threat-aware pawn may arm from suitable supplies and put on protection when danger is not close. With Home planning enabled, an eligible colonist may plan missing sleeping and eating essentials inside claimed shelter. A native stockpile whose own initiative ceiling also permits it may receive capacity furniture after stack pressure reaches 80%. Explicit orders and later forbids commit firmly; canceling an automatic blueprint pauses that kind for one day, while deconstructing a completed automatic furnishing vetoes it until its owning planning authority is renewed.",
            "Autonomous: full self-direction - takes positions and self-directs during raids on its own judgment, skill, and leadership. With Operational access enabled, the colony keeps ordinary visible non-quest items allowed, plus the Proactive threat-equipment behavior. With Home planning enabled, an eligible colonist may add modest seating, light, and usable recreation after essentials are covered. A native stockpile whose own initiative ceiling also permits it may receive capacity furniture after stack pressure reaches 50%. Explicit orders and later forbids remain authoritative; canceling an automatic blueprint pauses that kind for one day, while deconstructing a completed automatic furnishing vetoes it until its owning planning authority is renewed."
        };
    }

    // The one context menu for a colonist's awareness setup - shared by the colonist-bar
    // portraits and the Squads board.
    public static class AwarenessPawnMenu
    {
        public static void Open(Pawn colonist)
        {
            if (colonist == null) return;
            var opts = new List<FloatMenuOption>();
            for (int i = 0; i < 4; i++)
            {
                int lvl = i;
                string label = (AutonomyComponent.LevelOf(colonist) == lvl ? "* " : "") + "Autonomy: " + AutonomyComponent.LevelNames[lvl];
                opts.Add(new FloatMenuOption(label, delegate { AutonomyComponent.SetLevel(colonist, lvl); }));
            }
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
                for (int i = 0; i < 4; i++)
                {
                    int lvl = i;
                    string label = (AutonomyComponent.LevelOf(pawn) == lvl ? "* " : "") + AutonomyComponent.LevelNames[lvl];
                    yield return new FloatMenuOption(label, delegate { AutonomyComponent.SetLevel(pawn, lvl); });
                }
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
            int lvl = AutonomyComponent.LevelOf(p);
            yield return new Command_Autonomy
            {
                pawn = p,
                defaultLabel = "Autonomy: " + AutonomyComponent.LevelNames[lvl],
                defaultDesc = AutonomyComponent.LevelDescs[lvl] + "\n\nLeft-click cycles. Right-click picks directly.",
                icon = TexCommand.HoldOpen,
                action = delegate { AutonomyComponent.SetLevel(p, (AutonomyComponent.LevelOf(p) + 1) % 4); }
            };
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
