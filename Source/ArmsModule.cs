using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Module: arms. Weapons float beside the body with no hands on the grip - half of why
    // combat reads stiff. Drawn from MapComponentUpdate (the unambiguous per-frame world
    // hook, independent of which internal path renders the weapon): skin-tinted hands on
    // every drawn weapon, and the offhand weapon beside the main for dual wielders.
    public static class ArmsRenderPatch
    {
        // 1.6: the DrawEquipmentAiming postfix proved unreliable for world view
        // (fires in UI contexts). Superseded by ArmsMapComponent below.
        public static void TryInstall(HarmonyLib.Harmony harmony) { }
    }

    [StaticConstructorOnStartup]
    public class ArmsMapComponent : MapComponent
    {
        private static Texture2D handTex;
        private static bool texFailed;
        private static bool loggedLive;

        public ArmsMapComponent(Map map) : base(map) { }

        public override void MapComponentUpdate()
        {
            var s = AwarenessMod.Settings;
            if (s == null || (!s.renderArms && !s.dualWield)) return;
            if (Find.CurrentMap != map || texFailed) return;
            try { DrawAll(s); } catch { }
        }

        private void DrawAll(AwarenessSettings s)
        {
            if (handTex == null)
            {
                handTex = ContentFinder<Texture2D>.Get("CA/Hand", false);
                if (handTex == null)
                {
                    Log.Warning("[Colonist Awareness] CA/Hand texture missing; arms disabled");
                    texFailed = true;
                    return;
                }
            }

            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                var p = pawns[i];
                if (p.Dead || !p.RaceProps.Humanlike || p.equipment == null) continue;
                var primary = p.equipment.Primary;
                if (primary == null) continue;

                // Only when the game itself shows the weapon: aiming, or carried at ready.
                float angle;
                Vector3 drawLoc;
                var busy = p.stances != null ? p.stances.curStance as Stance_Busy : null;
                bool aiming = busy != null && !busy.neverAimWeapon && busy.focusTarg.IsValid;
                if (aiming)
                {
                    Vector3 targ = busy.focusTarg.HasThing
                        ? busy.focusTarg.Thing.DrawPos
                        : busy.focusTarg.Cell.ToVector3Shifted();
                    angle = (targ - p.DrawPos).AngleFlat();
                    drawLoc = p.DrawPos + new Vector3(0f, 0f, 0.4f).RotatedBy(angle);
                }
                else if (p.Drafted || (p.CurJob != null &&
                    (p.CurJob.def == JobDefOf.AttackMelee || p.CurJob.def == JobDefOf.AttackStatic)))
                {
                    // the game's own carry pose per facing
                    var rot = p.Rotation;
                    if (rot == Rot4.South) { drawLoc = p.DrawPos + new Vector3(0f, 0f, -0.22f); angle = 143f; }
                    else if (rot == Rot4.North) { drawLoc = p.DrawPos + new Vector3(0f, -0.0028f, -0.11f); angle = 143f; }
                    else if (rot == Rot4.East) { drawLoc = p.DrawPos + new Vector3(0.2f, 0f, -0.22f); angle = 143f; }
                    else { drawLoc = p.DrawPos + new Vector3(-0.2f, 0f, -0.22f); angle = 217f; }
                }
                else continue;

                drawLoc.y += 0.04f;

                if (!loggedLive)
                {
                    loggedLive = true;
                    Log.Message("[Colonist Awareness] arms world hook live");
                }

                var off = OffhandComponent.GetOffhand(p);
                bool dual = off != null;

                float rad = angle * Mathf.Deg2Rad;
                var along = new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad));
                var across = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

                if (s.renderArms)
                {
                    var mat = HandMat(p);
                    if (mat != null)
                    {
                        bool ranged = primary.def.IsRangedWeapon;
                        float range = 0f;
                        if (ranged && primary.def.Verbs != null && primary.def.Verbs.Count > 0)
                            range = primary.def.Verbs[0].range;
                        DrawHand(drawLoc, along, angle, mat, -0.1f, 0.17f);
                        if (ranged && range >= 20f && !dual)
                            DrawHand(drawLoc, along, angle, mat, 0.2f, 0.15f);
                    }
                }

                if (dual && s.dualWield)
                {
                    var offLoc = drawLoc - across * 0.32f;
                    offLoc.y += 0.001f;
                    PawnRenderUtility.DrawEquipmentAiming(off, offLoc, angle);
                    if (s.renderArms)
                    {
                        var mat = HandMat(p);
                        if (mat != null) DrawHand(offLoc + new Vector3(0f, 0.002f, 0f), along, angle, mat, -0.1f, 0.15f);
                    }
                }
            }
        }

        private static Material HandMat(Pawn p)
        {
            var color = p.story != null ? p.story.SkinColor : new Color(0.9f, 0.75f, 0.65f);
            return MaterialPool.MatFrom(new MaterialRequest(handTex, ShaderDatabase.Cutout, color));
        }

        private static void DrawHand(Vector3 root, Vector3 along, float angle, Material mat, float a, float size)
        {
            var pos = root + along * a;
            pos.y += 0.002f;
            var matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0);
        }
    }
}
