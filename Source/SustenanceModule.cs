using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Module 9: eating has consequences. Rolling average fullness drives a per-pawn weight
    // value; weight drives the CA_BodyWeight hediff stages (speed, carrying, hunger rate)
    // and swaps the body graphic at the extremes. Weight also feeds ruck utilization.
    public class SustenanceComponent : GameComponent
    {
        private const int IntervalTicks = 2000;
        private Dictionary<int, float> weight = new Dictionary<int, float>();
        private Dictionary<int, float> fullness = new Dictionary<int, float>();
        private Dictionary<int, string> originalBody = new Dictionary<int, string>();
        public static SustenanceComponent Instance;

        public SustenanceComponent(Game game) { Instance = this; }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref weight, "CA_weight", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref fullness, "CA_fullness", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref originalBody, "CA_originalBody", LookMode.Value, LookMode.Value);
            if (weight == null) weight = new Dictionary<int, float>();
            if (fullness == null) fullness = new Dictionary<int, float>();
            if (originalBody == null) originalBody = new Dictionary<int, string>();
            Instance = this;
        }

        public static float WeightOf(Pawn p)
        {
            var inst = Instance;
            if (p == null || inst == null) return 0.5f;
            float w;
            return inst.weight.TryGetValue(p.thingIDNumber, out w) ? w : 0.5f;
        }

        public static string TrendOf(Pawn p)
        {
            var inst = Instance;
            if (p == null || inst == null) return "stable";
            float ema;
            if (!inst.fullness.TryGetValue(p.thingIDNumber, out ema)) return "stable";
            if (ema > 0.62f) return "gaining";
            if (ema < 0.33f) return "losing";
            return "stable";
        }

        public static string LabelFor(float w)
        {
            if (w < 0.15f) return "emaciated";
            if (w < 0.35f) return "underweight";
            if (w < 0.65f) return "fit";
            if (w < 0.85f) return "overweight";
            return "obese";
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % IntervalTicks != 0) return;
            var s = AwarenessMod.Settings;
            if (s == null || !s.bodyWeight) return;
            try
            {
                var maps = Find.Maps;
                for (int m = 0; m < maps.Count; m++)
                {
                    var colonists = maps[m].mapPawns.FreeColonistsSpawned;
                    for (int i = 0; i < colonists.Count; i++) UpdatePawn(colonists[i]);
                }
            }
            catch { }
        }

        private void UpdatePawn(Pawn p)
        {
            if (p == null || p.Dead || p.needs == null || p.needs.food == null) return;
            if (p.DevelopmentalStage != DevelopmentalStage.Adult) return;
            int id = p.thingIDNumber;

            // First sight: record the genetic frame and seed weight FROM it, so a pawn who
            // starts fat is actually overweight (and can slim), a thin one underweight
            // (and can fatten), and everyone else starts fit.
            if (!originalBody.ContainsKey(id) && p.story != null && p.story.bodyType != null)
                originalBody[id] = p.story.bodyType.defName;
            if (!weight.ContainsKey(id))
            {
                float seed = 0.5f;
                string ob;
                if (originalBody.TryGetValue(id, out ob))
                {
                    if (ob == "Fat") seed = 0.75f;
                    else if (ob == "Thin") seed = 0.25f;
                }
                weight[id] = seed;
            }

            float f = p.needs.food.CurLevelPercentage;
            float ema;
            if (!fullness.TryGetValue(id, out ema)) ema = f;
            ema = ema + (f - ema) * 0.15f;
            fullness[id] = ema;

            float w;
            if (!weight.TryGetValue(id, out w)) w = 0.5f;
            if (ema > 0.62f) w += 0.0035f;
            else if (ema < 0.33f) w -= 0.005f;
            else w += (0.5f - w) * 0.01f;
            w = UnityEngine.Mathf.Clamp01(w);
            weight[id] = w;

            var def = DefDatabase<HediffDef>.GetNamedSilentFail("CA_BodyWeight");
            if (def != null)
            {
                var h = p.health.hediffSet.GetFirstHediffOfDef(def);
                if (h == null)
                {
                    h = HediffMaker.MakeHediff(def, p);
                    p.health.AddHediff(h);
                }
                if (h.Severity != w) h.Severity = w;
            }

            ApplyBodyType(p, w);
        }

        public static string TooltipFor(Pawn p)
        {
            float w = WeightOf(p);
            string s = "Body weight: " + w.ToStringPercent() + " (" + LabelFor(w) + "), " + TrendOf(p) + ".\n\n";
            s += "Driven by long-run eating habits. Extremes cost move speed, carrying capacity, and ruck utilization; overweight also raises hunger rate. Body shape changes at the far ends.";
            return s;
        }

        private static bool GenotypeLocked(Pawn p, string original)
        {
            // Stocky frames stay stocky regardless of diet; gene-forced bodies are never touched.
            if (original == "Hulk") return true;
            try
            {
                if (p.genes != null)
                {
                    var list = p.genes.GenesListForReading;
                    for (int i = 0; i < list.Count; i++)
                        if (list[i].def != null && list[i].def.bodyType != null) return true;
                }
            }
            catch { }
            return false;
        }

        private void ApplyBodyType(Pawn p, float w)
        {
            if (p.story == null || p.story.bodyType == null) return;
            int id = p.thingIDNumber;
            string original;
            if (!originalBody.TryGetValue(id, out original)) return;
            if (GenotypeLocked(p, original)) return;

            // "Normal" for a frame that IS fat/thin by origin means the gender-standard body.
            string normal = (original == "Fat" || original == "Thin")
                ? (p.gender == Gender.Female ? "Female" : "Male")
                : original;

            string want;
            if (w >= 0.85f) want = "Fat";
            else if (w <= 0.15f) want = "Thin";
            else if (w >= 0.65f) want = original == "Fat" ? "Fat" : normal;
            else if (w <= 0.35f) want = original == "Thin" ? "Thin" : normal;
            else want = normal;

            if (p.story.bodyType.defName == want) return;
            var def = DefDatabase<BodyTypeDef>.GetNamedSilentFail(want);
            if (def == null) return;
            try
            {
                p.story.bodyType = def;
                p.Drawer.renderer.SetAllGraphicsDirty();
            }
            catch { }
        }
    }

    // Bio tab badge: weight label at the top right of the character card, hoverable.
    public static class BioWeightBadge
    {
        public static void TryInstall(HarmonyLib.Harmony harmony)
        {
            try
            {
                var m = HarmonyLib.AccessTools.Method(typeof(CharacterCardUtility), "DrawCharacterCard");
                if (m == null) { Log.Warning("[Colonist Awareness] DrawCharacterCard not found; bio badge off"); return; }
                harmony.Patch(m, postfix: new HarmonyLib.HarmonyMethod(typeof(BioWeightBadge), "Postfix"));
            }
            catch (System.Exception e)
            {
                Log.Warning("[Colonist Awareness] bio badge install failed: " + e.Message);
            }
        }

        public static void Postfix(UnityEngine.Rect rect, Pawn pawn)
        {
            try
            {
                var s = AwarenessMod.Settings;
                if (s == null || !s.bodyWeight) return;
                if (pawn == null || !pawn.RaceProps.Humanlike) return;
                if (pawn.DevelopmentalStage != DevelopmentalStage.Adult) return;
                float w = SustenanceComponent.WeightOf(pawn);
                var badge = new UnityEngine.Rect(rect.xMax - 112f, rect.y + 26f, 108f, 20f);
                UnityEngine.GUI.color = w < 0.15f || w >= 0.85f
                    ? new UnityEngine.Color(1f, 0.45f, 0.35f)
                    : (w < 0.35f || w >= 0.65f ? new UnityEngine.Color(1f, 0.85f, 0.4f) : new UnityEngine.Color(0.6f, 1f, 0.6f));
                Text.Font = GameFont.Tiny;
                Text.Anchor = UnityEngine.TextAnchor.MiddleRight;
                Widgets.Label(badge, SustenanceComponent.LabelFor(w));
                Text.Anchor = UnityEngine.TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                UnityEngine.GUI.color = UnityEngine.Color.white;
                TooltipHandler.TipRegion(badge, SustenanceComponent.TooltipFor(pawn));
            }
            catch { }
        }
    }
}
