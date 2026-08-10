using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Module: arrangements. The acceptance sentence, verbatim: "Be Luc and
    // say SET UP THIS PERIMETER. Then click Cardenas and right-click on that
    // perimeter HE SET and add him to it, manually." An arrangement is a
    // first-class object - named, owned, visible, scribed - composed by the
    // operator's hand pawn by pawn. Fully manual at every autonomy tier;
    // permissions and autonomy layer on LATER. Nothing here is a black box:
    // the object renders its outline, its name, and its member count on the
    // map at all times.
    // Kinds are the EXISTING feature vocabulary only - audited against the
    // code surface. "Formation" and "Position" are the mod's own words
    // (Position - hold this point / Draw formation / CA direct formation
    // orders); Line/Hide/Ambush/Stack likewise. No invented nouns.
    public enum CAArrangementKind
    {
        Formation = 0,
        Line = 1,
        Hide = 2,
        Ambush = 3,
        Stack = 4
    }

    public class CAArrangement : IExposable
    {
        public int id;
        public string name;
        public int ownerId;
        // Organizational ownership: set when an ORGANIZATION (not a pawn)
        // owns this arrangement - a settlement's standing line exists
        // because its organization established it, not because a pawn drew
        // it. Null for pawn-owned arrangements.
        public string ownerOrgKey;
        public int kind;
        public int createdTick;
        // Ambush arrangements share one episode so every joiner enters the
        // SAME synchronized group - the group machinery and the object are
        // one thing.
        public int episodeId;
        public List<IntVec3> cells = new List<IntVec3>();
        public List<int> memberIds = new List<int>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref ownerId, "ownerId");
            Scribe_Values.Look(ref ownerOrgKey, "ownerOrgKey");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref createdTick, "createdTick");
            Scribe_Values.Look(ref episodeId, "episodeId");
            Scribe_Collections.Look(ref cells, "cells", LookMode.Value);
            Scribe_Collections.Look(ref memberIds, "memberIds",
                LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (cells == null) cells = new List<IntVec3>();
                if (memberIds == null) memberIds = new List<int>();
            }
        }

        public IntVec3 Centroid()
        {
            if (cells.Count == 0) return IntVec3.Invalid;
            long x = 0, z = 0;
            for (int i = 0; i < cells.Count; i++)
            { x += cells[i].x; z += cells[i].z; }
            return new IntVec3((int)(x / cells.Count), 0,
                (int)(z / cells.Count));
        }
    }

    public class CAArrangementMapComponent : MapComponent
    {
        private List<CAArrangement> arrangements = new List<CAArrangement>();
        private int nextId = 1;
        private int cooldown;

        // Cached instanced edge rendering - DrawFieldEdges clears a full-map
        // grid per call and must never run per-frame on large maps.
        private const int InstanceCap = 1023;
        private readonly Dictionary<int, List<List<Matrix4x4>>> edgeChunks =
            new Dictionary<int, List<List<Matrix4x4>>>();
        private Material edgeMaterial;
        private bool renderDirty = true;

        private static readonly Color ArrangementColor =
            new Color(0.55f, 0.85f, 1f);

        public CAArrangementMapComponent(Map map) : base(map) { }

        public static CAArrangementMapComponent For(Map map)
        {
            return map?.GetComponent<CAArrangementMapComponent>();
        }

        public List<CAArrangement> All => arrangements;

        public CAArrangement Create(Pawn owner, CAArrangementKind kind,
            List<IntVec3> geometry)
        {
            if (owner == null || geometry == null || geometry.Count == 0)
                return null;
            string baseName = owner.LabelShort + "'s "
                + KindNoun(kind);
            string finalName = baseName;
            int suffix = 2;
            while (ByName(finalName) != null)
                finalName = baseName + " (" + suffix++ + ")";
            var arrangement = new CAArrangement
            {
                id = nextId++,
                name = finalName,
                ownerId = owner.thingIDNumber,
                kind = (int)kind,
                createdTick = Find.TickManager.TicksGame,
                cells = new List<IntVec3>(geometry)
            };
            arrangements.Add(arrangement);
            renderDirty = true;
            CATrace.Pawn(owner, "arrangement CREATED - " + finalName + " ("
                + geometry.Count + " cells); members join by right-clicking "
                + "its outline", anchor: owner.Position);
            Messages.Message(finalName + " established ("
                + geometry.Count + " cells).",
                new LookTargets(arrangement.Centroid(), map),
                MessageTypeDefOf.TaskCompletion, false);
            return arrangement;
        }

        // Organizational creation path: no pawn owner, no message spam (runs
        // during settlement materialization), the organization's name on the
        // object. Same dedup and id discipline as the pawn path.
        public CAArrangement CreateForOrganization(string orgKey,
            string ownerDisplay, CAArrangementKind kind,
            List<IntVec3> geometry)
        {
            if (orgKey == null || geometry == null || geometry.Count == 0)
                return null;
            string baseName = ownerDisplay + "'s " + KindNoun(kind);
            string finalName = baseName;
            int suffix = 2;
            while (ByName(finalName) != null)
                finalName = baseName + " (" + suffix++ + ")";
            var arrangement = new CAArrangement
            {
                id = nextId++,
                name = finalName,
                ownerId = 0,
                ownerOrgKey = orgKey,
                kind = (int)kind,
                createdTick = Find.TickManager.TicksGame,
                cells = new List<IntVec3>(geometry)
            };
            arrangements.Add(arrangement);
            renderDirty = true;
            return arrangement;
        }

        public static string KindNoun(CAArrangementKind kind)
        {
            switch (kind)
            {
                case CAArrangementKind.Line: return "line";
                case CAArrangementKind.Hide: return "hide";
                case CAArrangementKind.Ambush: return "ambush";
                case CAArrangementKind.Stack: return "stack";
                default: return "formation";
            }
        }

        public CAArrangement ByName(string name)
        {
            for (int i = 0; i < arrangements.Count; i++)
                if (arrangements[i].name == name) return arrangements[i];
            return null;
        }

        public CAArrangement ById(int id)
        {
            for (int i = 0; i < arrangements.Count; i++)
                if (arrangements[i].id == id) return arrangements[i];
            return null;
        }

        public CAArrangement At(IntVec3 cell)
        {
            for (int i = 0; i < arrangements.Count; i++)
            {
                var cells = arrangements[i].cells;
                for (int c = 0; c < cells.Count; c++)
                    if (cells[c] == cell
                        || cells[c].InHorDistOf(cell, 1.4f))
                        return arrangements[i];
            }
            return null;
        }

        public CAArrangement MembershipOf(Pawn pawn)
        {
            if (pawn == null) return null;
            for (int i = 0; i < arrangements.Count; i++)
                if (arrangements[i].memberIds.Contains(pawn.thingIDNumber))
                    return arrangements[i];
            return null;
        }

        // Manual join: the pawn takes a clash-free standable slot on the
        // geometry, held through the ordinary hold machinery. Works at every
        // autonomy tier - this is the operator's hand.
        public bool Join(Pawn pawn, CAArrangement arrangement,
            IntVec3 preferNear)
        {
            if (pawn == null || arrangement == null) return false;
            var hold = map.GetComponent<HoldMapComponent>();
            if (hold == null) return false;
            var occupied = new List<IntVec3>();
            var job = CATactical.JobOf(map);
            if (job != null)
            {
                var colonists = map.mapPawns.FreeColonistsSpawned;
                for (int i = 0; i < colonists.Count; i++)
                {
                    int k; IntVec3 oc, ow;
                    if (job.TryGetOrder(colonists[i], out k, out oc, out ow)
                        && k == LordJob_CATactical.KindHold)
                        occupied.Add(oc);
                }
            }
            // Apex doctrine: the heaviest guns sit at the geometry's
            // extremes - the cells farthest from the centroid are the
            // apexes, and a joiner carrying real reach (range >= 30) is
            // steered toward them; everyone else fills by proximity.
            IntVec3 centroidForApex = arrangement.Centroid();
            var joinerVerb = pawn.equipment?.PrimaryEq?.PrimaryVerb;
            bool heavyGun = joinerVerb != null
                && !joinerVerb.verbProps.IsMeleeAttack
                && joinerVerb.verbProps.range >= 30f;
            IntVec3 slot = IntVec3.Invalid;
            float best = float.MaxValue;
            for (int c = 0; c < arrangement.cells.Count; c++)
            {
                IntVec3 cell = arrangement.cells[c];
                IntVec3 stand = cell.Standable(map) ? cell
                    : CellFinder.StandableCellNear(cell, map, 2f, null);
                if (!stand.IsValid) continue;
                bool clash = false;
                for (int o = 0; o < occupied.Count; o++)
                    if (stand.InHorDistOf(occupied[o], 1.9f))
                    { clash = true; break; }
                if (clash) continue;
                if (!pawn.CanReach(stand, Verse.AI.PathEndMode.OnCell,
                    Danger.Deadly)) continue;
                float d = preferNear.IsValid
                    ? stand.DistanceTo(preferNear)
                    : stand.DistanceTo(pawn.Position);
                if (heavyGun && centroidForApex.IsValid)
                    d -= stand.DistanceTo(centroidForApex) * 0.75f;
                if (d < best) { best = d; slot = stand; }
            }
            if (!slot.IsValid)
            {
                Messages.Message("No open position on " + arrangement.name
                    + " for " + pawn.LabelShort + ".",
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
            bool accepted;
            var arrangementKind = (CAArrangementKind)arrangement.kind;
            if (arrangementKind == CAArrangementKind.Hide)
            {
                // Joining a hide means going to ground on its geometry.
                IntVec3 concealment;
                if (!HiddenRegistry.TryFindConcealmentNearExcluding(map, pawn,
                    slot, 7.9f, occupied, out concealment))
                    concealment = slot;
                accepted = HiddenRegistry.OrderHide(pawn, concealment,
                    CACombatIntent.Operator(pawn, pawn,
                        CAIntentController.Hide));
            }
            else if (arrangementKind == CAArrangementKind.Ambush)
            {
                // Every joiner enters the SAME synchronized group: the
                // arrangement carries the episode, so the object and the
                // ambush group are one thing - lifecycle receipts attach to
                // what the operator can see and click.
                if (arrangement.episodeId <= 0)
                    arrangement.episodeId = CACombatIntent.NewEpisode();
                IntVec3 concealment;
                if (!HiddenRegistry.TryFindConcealmentNearExcluding(map, pawn,
                    slot, 7.9f, occupied, out concealment))
                    concealment = slot;
                accepted = HiddenRegistry.OrderAmbush(pawn, concealment,
                    CACombatIntent.Operator(pawn, pawn,
                        CAIntentController.Ambush, arrangement.episodeId));
            }
            else
            {
                // Doctrine (Formation/Line): a position exists to serve
                // FIRES. Each joiner receives an assigned SECTOR - a watch
                // probe outward through their slot toward the threat side -
                // so adjacent sectors interlock by construction.
                IntVec3 sectorWatch = ComputeSectorWatch(arrangement, slot,
                    pawn);
                accepted = sectorWatch.IsValid
                    ? hold.OrderHoldWatching(pawn, slot, sectorWatch,
                        CACombatIntent.Operator(pawn, pawn,
                            CAIntentController.Hold))
                    : hold.OrderHold(pawn, slot,
                        CACombatIntent.Operator(pawn, pawn,
                            CAIntentController.Hold));
            }
            if (!accepted) return false;
            // Doctrine: priorities of work. The first member on the ground
            // IS the security element; mutual support is checked on every
            // join and its absence is said out loud, never assumed.
            if (arrangement.memberIds.Count == 0)
                CATrace.Pawn(pawn, arrangement.name
                    + ": SECURITY POSTED - first position occupied; "
                    + "priorities of work begin",
                    destination: slot, anchor: pawn.Position);
            else
            {
                float nearestMate = float.MaxValue;
                var job2 = CATactical.JobOf(map);
                var colonists2 = map.mapPawns.FreeColonistsSpawned;
                if (job2 != null)
                    for (int i = 0; i < colonists2.Count; i++)
                    {
                        if (!arrangement.memberIds.Contains(
                            colonists2[i].thingIDNumber)) continue;
                        int k3; IntVec3 oc3, ow3;
                        if (job2.TryGetOrder(colonists2[i], out k3, out oc3,
                                out ow3))
                        {
                            float d3 = slot.DistanceTo(oc3);
                            if (d3 < nearestMate) nearestMate = d3;
                        }
                    }
                if (nearestMate <= 12f)
                    CATrace.Pawn(pawn, arrangement.name
                        + ": sector INTERLOCKED - mutual support at "
                        + nearestMate.ToString("0.#") + " cells",
                        destination: slot, anchor: pawn.Position);
                else
                    CATrace.Pawn(pawn, arrangement.name
                        + ": position LACKS MUTUAL SUPPORT - nearest "
                        + "teammate " + (nearestMate == float.MaxValue
                            ? "none placed" : nearestMate.ToString("0.#")
                            + " cells") + "; doctrine wants supporting "
                        + "distance", destination: slot,
                        anchor: pawn.Position);
            }
            if (!arrangement.memberIds.Contains(pawn.thingIDNumber))
                arrangement.memberIds.Add(pawn.thingIDNumber);
            CATrace.Pawn(pawn, "JOINED " + arrangement.name + " at " + slot,
                destination: slot, anchor: pawn.Position);
            Messages.Message(pawn.LabelShort + " joins " + arrangement.name
                + ".", new LookTargets(slot, map),
                MessageTypeDefOf.SilentInput, false);
            return true;
        }

        // Sector assignment: probe toward the freshest known contact side
        // when a threat picture exists, else outward from the geometry's
        // centroid through the slot - enclosing shapes get all-around
        // security, lines get the far side.
        private IntVec3 ComputeSectorWatch(CAArrangement arrangement,
            IntVec3 slot, Pawn pawn)
        {
            var know = KnowledgeMapComponent.For(map);
            if (know != null)
            {
                var contacts = know.FreshContacts(pawn);
                IntVec3 nearest = IntVec3.Invalid;
                float best = float.MaxValue;
                for (int i = 0; i < contacts.Count; i++)
                {
                    if (!contacts[i].Cell.IsValid) continue;
                    float d = contacts[i].Cell.DistanceTo(slot);
                    if (d < best) { best = d; nearest = contacts[i].Cell; }
                }
                if (nearest.IsValid) return nearest;
            }
            IntVec3 centroid = arrangement.Centroid();
            if (!centroid.IsValid || centroid == slot)
                return IntVec3.Invalid;
            Vector3 outward = (slot - centroid).ToVector3();
            if (outward.sqrMagnitude < 1f) return IntVec3.Invalid;
            outward.Normalize();
            IntVec3 probe = slot + IntVec3.FromVector3(outward * 10f);
            return probe.InBounds(map) ? probe : IntVec3.Invalid;
        }

        public void Leave(Pawn pawn, CAArrangement arrangement,
            bool releaseHold = true)
        {
            if (pawn == null || arrangement == null) return;
            arrangement.memberIds.Remove(pawn.thingIDNumber);
            if (releaseHold)
            {
                var hold = map.GetComponent<HoldMapComponent>();
                if (hold != null && hold.IsHolding(pawn))
                    hold.Release(pawn, reason: "left " + arrangement.name);
            }
            CATrace.Pawn(pawn, "LEFT " + arrangement.name,
                anchor: pawn.Position);
        }

        public void Disband(CAArrangement arrangement)
        {
            if (arrangement == null) return;
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
                if (arrangement.memberIds.Contains(
                        colonists[i].thingIDNumber))
                    Leave(colonists[i], arrangement);
            arrangements.Remove(arrangement);
            renderDirty = true;
            Messages.Message(arrangement.name + " disbanded.",
                MessageTypeDefOf.SilentInput, false);
        }

        public override void MapComponentTick()
        {
            if (--cooldown > 0) return;
            cooldown = 250;
            // Prune the dead and the vanished from membership.
            for (int i = 0; i < arrangements.Count; i++)
            {
                var members = arrangements[i].memberIds;
                for (int m = members.Count - 1; m >= 0; m--)
                {
                    bool alive = false;
                    var colonists = map.mapPawns.FreeColonistsSpawned;
                    for (int c = 0; c < colonists.Count; c++)
                        if (colonists[c].thingIDNumber == members[m])
                        { alive = true; break; }
                    if (!alive) members.RemoveAt(m);
                }
            }
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map || arrangements.Count == 0) return;
            if (renderDirty) RebuildRenderCache();
            foreach (var chunks in edgeChunks.Values)
                for (int c = 0; c < chunks.Count; c++)
                    Graphics.DrawMeshInstanced(MeshPool.plane10, 0,
                        edgeMaterial, chunks[c]);
        }

        public override void MapComponentOnGUI()
        {
            if (Find.CurrentMap != map) return;
            for (int i = 0; i < arrangements.Count; i++)
            {
                var arrangement = arrangements[i];
                IntVec3 centroid = arrangement.Centroid();
                if (!centroid.IsValid) continue;
                GenMapUI.DrawThingLabel(
                    GenMapUI.LabelDrawPosFor(centroid),
                    arrangement.name + " ("
                    + arrangement.memberIds.Count + ")",
                    ArrangementColor);
            }
        }

        private void RebuildRenderCache()
        {
            renderDirty = false;
            if (edgeMaterial == null)
            {
                Material source = MatLoader.LoadMat("Misc/FieldEdge");
                edgeMaterial = MaterialPool.MatFrom(
                    (Texture2D)source.mainTexture,
                    ShaderDatabase.Transparent, ArrangementColor, 2900);
                edgeMaterial.mainTexture.wrapMode = TextureWrapMode.Clamp;
                edgeMaterial.enableInstancing = true;
            }
            edgeChunks.Clear();
            float y = Rand.ValueSeeded(
                ArrangementColor.ToOpaque().GetHashCode()) * 0.03658537f / 10f;
            int maxX = map.Size.x, maxZ = map.Size.z;
            for (int a = 0; a < arrangements.Count; a++)
            {
                var cells = arrangements[a].cells;
                var set = new HashSet<IntVec3>(cells);
                var chunks = new List<List<Matrix4x4>>();
                List<Matrix4x4> current = null;
                for (int i = 0; i < cells.Count; i++)
                {
                    IntVec3 cell = cells[i];
                    if (!cell.InBounds(map)) continue;
                    for (int rot = 0; rot < 4; rot++)
                    {
                        bool open;
                        switch (rot)
                        {
                            case 0: open = cell.z < maxZ - 1
                                && !set.Contains(cell + IntVec3.North); break;
                            case 1: open = cell.x < maxX - 1
                                && !set.Contains(cell + IntVec3.East); break;
                            case 2: open = cell.z > 0
                                && !set.Contains(cell + IntVec3.South); break;
                            default: open = cell.x > 0
                                && !set.Contains(cell + IntVec3.West); break;
                        }
                        if (!open) continue;
                        if (current == null || current.Count >= InstanceCap)
                        {
                            current = new List<Matrix4x4>();
                            chunks.Add(current);
                        }
                        current.Add(Matrix4x4.TRS(
                            cell.ToVector3ShiftedWithAltitude(
                                AltitudeLayer.MetaOverlays)
                                + new Vector3(0f, y, 0f),
                            new Rot4(rot).AsQuat, Vector3.one));
                    }
                }
                edgeChunks[arrangements[a].id] = chunks;
            }
        }

        public void MarkRenderDirty() { renderDirty = true; }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref arrangements, "caArrangements",
                LookMode.Deep);
            Scribe_Values.Look(ref nextId, "caArrangementNextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (arrangements == null)
                    arrangements = new List<CAArrangement>();
                renderDirty = true;
            }
        }
    }

    // Creation tool: selected from the owner's palette, drags real geometry
    // with the native style picker, births the object on completion. The
    // owner then mans what they set - first slot is theirs.
    public class Designator_CAArrangementCreate : Designator_Cells
    {
        private readonly Pawn owner;
        private readonly CAArrangementKind kind;
        private readonly List<IntVec3> collected = new List<IntVec3>();
        private static DrawStyleCategoryDef cachedStyle;

        public Designator_CAArrangementCreate(Pawn owner,
            CAArrangementKind kind)
        {
            this.owner = owner;
            this.kind = kind;
            string noun = CAArrangementMapComponent.KindNoun(kind);
            defaultLabel = owner.LabelShort + ": set up a " + noun;
            defaultDesc = "Drag the " + noun + "'s shape. "
                + owner.LabelShort
                + " establishes it and takes the first position; add anyone "
                + "else by selecting them and right-clicking the outline.";
            icon = TexCommand.HoldOpen;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_PlanAdd;
            useMouseIcon = true;
        }

        public override DrawStyleCategoryDef DrawStyleCategory
        {
            get
            {
                if (cachedStyle == null)
                    cachedStyle = DefDatabase<DrawStyleCategoryDef>
                        .GetNamedSilentFail("Default2D")
                        ?? DrawStyleCategoryDefOf.Plans;
                return cachedStyle;
            }
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            return c.InBounds(Map);
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (!collected.Contains(c)) collected.Add(c);
        }

        public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
        {
            base.DesignateMultiCell(cells);
            if (collected.Count == 0) return;
            var component = CAArrangementMapComponent.For(Map);
            if (component == null || owner == null || owner.Dead) return;
            CAArrangement arrangement = component.Create(owner, kind,
                collected);
            collected.Clear();
            if (arrangement != null)
                component.Join(owner, arrangement, owner.Position);
            Find.DesignatorManager.Deselect();
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
        }
    }

    // Post-creation editing: what you create, you can alter. Expand drags
    // new cells into the geometry; carve drags cells out. Freeform - a tile
    // added beside another makes its right angle, exactly as the grid
    // wants; nothing stays binary squares unless you draw squares.
    public class Designator_CAArrangementEdit : Designator_Cells
    {
        private readonly CAArrangement arrangement;
        private readonly bool carve;
        private static DrawStyleCategoryDef cachedStyle;

        public Designator_CAArrangementEdit(CAArrangement arrangement,
            bool carve)
        {
            this.arrangement = arrangement;
            this.carve = carve;
            defaultLabel = (carve ? "Carve " : "Expand ")
                + arrangement.name;
            defaultDesc = carve
                ? "Drag cells to remove them from " + arrangement.name + "."
                : "Drag cells to add them to " + arrangement.name + ".";
            icon = carve ? TexCommand.ClearPrioritizedWork
                : TexCommand.HoldOpen;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            useMouseIcon = true;
        }

        public override DrawStyleCategoryDef DrawStyleCategory
        {
            get
            {
                if (cachedStyle == null)
                    cachedStyle = DefDatabase<DrawStyleCategoryDef>
                        .GetNamedSilentFail("Default2D")
                        ?? DrawStyleCategoryDefOf.Plans;
                return cachedStyle;
            }
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map)) return false;
            bool has = arrangement.cells.Contains(c);
            return carve ? has : !has;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (carve) arrangement.cells.Remove(c);
            else if (!arrangement.cells.Contains(c))
                arrangement.cells.Add(c);
            CAArrangementMapComponent.For(Map)?.MarkRenderDirty();
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
        }
    }

    // The manual composition surface: right-click an arrangement's geometry
    // with any pawn selected to add them; members and owners get gizmos.
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_ArrangementGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos,
            Pawn __instance)
        {
            foreach (var g in gizmos) yield return g;
            Pawn pawn = __instance;
            if (pawn == null || !pawn.IsColonistPlayerControlled
                || pawn.Map == null) yield break;
            var component = CAArrangementMapComponent.For(pawn.Map);
            if (component == null) yield break;
            CAArrangement membership = component.MembershipOf(pawn);
            if (membership != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Leave " + membership.name,
                    defaultDesc = "Step off the arrangement and return to "
                        + "normal duties.",
                    icon = TexCommand.ClearPrioritizedWork,
                    action = delegate
                    { component.Leave(pawn, membership); }
                };
                if (membership.ownerId == pawn.thingIDNumber)
                    yield return new Command_Action
                    {
                        defaultLabel = "Disband " + membership.name,
                        defaultDesc = "Dissolve the arrangement entirely; "
                            + "every member is released.",
                        icon = TexCommand.ForbidOn,
                        action = delegate
                        { component.Disband(membership); }
                    };
            }
        }
    }
}
