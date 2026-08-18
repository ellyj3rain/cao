using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Arms as render-tree nodes: attached to the Body node through the game's own render
    // pipeline, so they appear in the world AND in portraits, on every facing, cached and
    // layered by the same machinery as everything else on the pawn. Skin-tinted.
    public static class ArmsNodePatch
    {
        private static System.Reflection.MethodInfo addChild;

        public static void TryInstall(Harmony harmony)
        {
            try
            {
                var setup = AccessTools.Method(typeof(PawnRenderTree), "SetupDynamicNodes");
                addChild = AccessTools.Method(typeof(PawnRenderTree), "AddChild");
                if (setup == null || addChild == null)
                {
                    Log.Warning("[Colonist Awareness] render tree seam not found; arm nodes off");
                    return;
                }
                harmony.Patch(setup, postfix: new HarmonyMethod(typeof(ArmsNodePatch), "Postfix"));
            }
            catch (System.Exception e)
            {
                Log.Warning("[Colonist Awareness] arm nodes install failed: " + e.Message);
            }
        }

        public static void Postfix(PawnRenderTree __instance)
        {
            try
            {
                var s = AwarenessMod.Settings;
                if (s == null || !s.renderArms) return;
                var pawn = __instance.pawn;
                if (pawn == null || pawn.RaceProps == null || !pawn.RaceProps.Humanlike) return;
                addChild.Invoke(__instance, new object[] {
                    MakeNode(pawn, __instance, PawnRenderNodeProperties.Side.Left), null });
                addChild.Invoke(__instance, new object[] {
                    MakeNode(pawn, __instance, PawnRenderNodeProperties.Side.Right), null });
            }
            catch { }
        }

        private static PawnRenderNode MakeNode(Pawn pawn, PawnRenderTree tree, PawnRenderNodeProperties.Side side)
        {
            var props = new PawnRenderNodeProperties
            {
                debugLabel = "CA_Arm",
                // Operator-authored art contract: square canvas, per-side, per-facing
                // (ArmLeft_south.png etc., west auto-mirrors east). White = skin-tinted.
                texPath = side == PawnRenderNodeProperties.Side.Left ? "CA/ArmLeft" : "CA/ArmRight",
                colorType = PawnRenderNodeProperties.AttachmentColorType.Skin,
                useSkinShader = true,
                parentTagDef = PawnRenderNodeTagDefOf.Body,
                workerClass = typeof(PawnRenderNodeWorker_CAArm),
                baseLayer = 35f,
                drawSize = new Vector2(0.5f, 0.5f),
                side = side
            };
            return new PawnRenderNode(pawn, props, tree);
        }
    }

    public class PawnRenderNodeWorker_CAArm : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms)) return false;
            var s = AwarenessMod.Settings;
            if (s == null || !s.renderArms) return false;
            if (parms.dead || parms.rotDrawMode == RotDrawMode.Dessicated) return false;
            if (parms.pawn != null && parms.pawn.DevelopmentalStage == DevelopmentalStage.Baby) return false;
            return true;
        }

        // Children get child-sized arms, per the game's own life-stage proportions.
        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            var v = base.ScaleFor(node, parms);
            var ls = parms.pawn != null && parms.pawn.ageTracker != null
                ? parms.pawn.ageTracker.CurLifeStage : null;
            if (ls != null)
            {
                v.x *= ls.bodySizeFactor;
                v.z *= ls.bodySizeFactor;
            }
            return v;
        }

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            var off = base.OffsetFor(node, parms, out pivot);
            bool left = node.Props.side == PawnRenderNodeProperties.Side.Left;
            float x;
            var facing = parms.facing;
            if (facing == Rot4.East) x = left ? -0.08f : 0.15f;
            else if (facing == Rot4.West) x = left ? -0.15f : 0.08f;
            else x = left ? -0.27f : 0.27f;
            off.x += x;
            off.z -= 0.05f;
            return off;
        }

        public override float LayerFor(PawnRenderNode node, PawnDrawParms parms)
        {
            // arms swing behind the body when facing away; otherwise they sit OVER
            // apparel - a clothed torso must not swallow the limbs
            if (parms.facing == Rot4.North) return 4f;
            return 35f;
        }
    }
}
