using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Module: painted lines. HOI4-style direct intent at every autonomy level: right-click
    // with a team selected, paint a defensive line (two clicks) or mark an objective
    // point - the team distributes into evenly spaced holds along the line or ringed
    // around the objective, fights from them, and the hold deviation triggers supply
    // the adaptation when the enemy breaks the plan's feasibility. Repainting is the
    // player shifting intent; the team re-adapts.
    public class FloatMenuOptionProvider_LineOrders : FloatMenuOptionProvider
    {
        protected override bool Drafted { get { return true; } }
        protected override bool Undrafted { get { return true; } }
        protected override bool Multiselect { get { return true; } }

        public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
        {
            var s = AwarenessMod.Settings;
            // Per-block gates below - holdOrders gates the line/objective/draw block,
            // battleDrills gates the drills block; neither toggle hides the other's orders.
            if (s == null || (!s.holdOrders && !s.battleDrills)) yield break;
            var map = context.map;
            IntVec3 cell = context.ClickedCell;
            if (map == null || !cell.InBounds(map) || !cell.Standable(map)) yield break;

            // Box-selecting IS explicit inclusion - same rule as the with-who fork:
            // selected pawns comply at any mode. Only the unable are excluded.
            var team = new List<Pawn>();
            foreach (var p in context.ValidSelectedPawns)
            {
                if (p == null || !p.IsColonistPlayerControlled || p.Downed || p.InMentalState) continue;
                if (p.WorkTagIsDisabled(WorkTags.Violent)) continue;
                if (p.DevelopmentalStage != DevelopmentalStage.Adult) continue;
                team.Add(p);
            }
            if (team.Count < 2) yield break; // single selection gets these via the fork

            var teamArr = team;
            var a = cell;
            var direct = new List<FloatMenuOption>();
            if (s.holdOrders)
            {
                direct.Add(new FloatMenuOption("Defensive line to... (" + team.Count + ")",
                    delegate { LineOrders.BeginLineTargeting(null, teamArr, a, map); }));
                direct.Add(new FloatMenuOption("Advance on objective here (" + team.Count + ")",
                    delegate { LineOrders.OrderObjective(null, teamArr, a, map); }));

                // Drawing is a direct player command surface, not an autonomous plan.
                // The selected pawns' ability to comply is evaluated after the shape is
                // authored; no colony-wide skill gate prevents the player authoring it.
                direct.Add(new FloatMenuOption("Draw formation... (" + team.Count + ")",
                    delegate { PaintManager.OpenShapeMenu(null, teamArr, map); }));
            }

            // Battle drill: stack and clear, on a clicked door.
            if (s.battleDrills)
            {
                var door = cell.GetDoor(map);
                if (door != null)
                {
                    var d2 = door;
                    var drills = map.GetComponent<DrillsMapComponent>();
                    direct.Add(new FloatMenuOption("Stack on door (" + team.Count + ")",
                        delegate { Drills.StackOnDoor(teamArr, d2, map); }));
                    if (drills != null && drills.IsStacked(door, team))
                        direct.Add(new FloatMenuOption("Breach and clear (" + team.Count + ")",
                            delegate { Drills.BreachAndClear(teamArr, d2, map); }));
                }
            }
            if (direct.Count > 0)
            {
                var options = direct;
                yield return new FloatMenuOption("CA direct formation orders...", delegate
                {
                    Find.WindowStack.Add(new FloatMenu(options));
                });
            }
        }
    }

    public static class LineOrders
    {
        // Second click completes the line; the team spreads along it.
        public static void BeginLineTargeting(Pawn issuer, List<Pawn> team,
            IntVec3 a, Map map)
        {
            var tp = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false
            };
            Find.Targeter.BeginTargeting(tp,
                delegate (LocalTargetInfo targ)
                {
                    IntVec3 b = targ.Cell;
                    if (!b.InBounds(map) || b == a)
                    {
                        Messages.Message("Line needs two points.", MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    OrderLine(issuer, team, a, b, map);
                },
                delegate (LocalTargetInfo targ)
                {
                    Widgets.MouseAttachedLabel("line end (" + team.Count + " positions)", 0f, 0f, null);
                    if (targ.Cell.InBounds(map))
                        GenDraw.DrawLineBetween(a.ToVector3Shifted(), targ.Cell.ToVector3Shifted());
                });
        }

        public static void OrderLine(Pawn issuer, List<Pawn> team, IntVec3 a,
            IntVec3 b, Map map)
        {
            var cells = new List<IntVec3> { a };
            foreach (var c in GenSight.PointsOnLineOfSight(a, b)) cells.Add(c);
            if (cells[cells.Count - 1] != b) cells.Add(b);

            var slots = PickSlots(cells, team.Count, map);
            int assigned = AssignHolds(issuer, team, slots, map);
            if (assigned > 0)
            {
                LineDrawMapComponent.Note(map, a, b);
                Messages.Message(assigned + " of " + team.Count
                    + " taking line positions.",
                    new LookTargets(cells[cells.Count / 2], map),
                    MessageTypeDefOf.SilentInput, false);
            }
            else
                Messages.Message("No selected fighter can reach that line.",
                    MessageTypeDefOf.RejectInput, false);
        }

        // The objective: a spaced ring just short of the point - a formation that takes
        // ground and holds it, not a pile on one tile.
        public static void OrderObjective(Pawn issuer, List<Pawn> team,
            IntVec3 objective, Map map)
        {
            float radius = Mathf.Max(2.5f, Mathf.Sqrt(team.Count) * 1.6f);
            var slots = new List<IntVec3>();
            for (int i = 0; i < team.Count; i++)
            {
                float ang = (360f / team.Count) * i * Mathf.Deg2Rad;
                var rough = objective + IntVec3.FromVector3(
                    new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius);
                var cell = CellFinder.StandableCellNear(rough, map, 4f, null);
                if (cell.IsValid) slots.Add(cell);
            }
            int assigned = AssignHolds(issuer, team, slots, map);
            if (assigned > 0)
                Messages.Message(assigned + " of " + team.Count
                    + " advancing on the objective.",
                    new LookTargets(objective, map),
                    MessageTypeDefOf.SilentInput, false);
            else
                Messages.Message("No selected fighter can reach that objective formation.",
                    MessageTypeDefOf.RejectInput, false);
        }

        // Evenly spaced standable slots along the painted cells.
        public static List<IntVec3> PickSlots(List<IntVec3> cells, int count, Map map)
        {
            var slots = new List<IntVec3>();
            if (cells.Count == 0 || count == 0) return slots;
            float step = Mathf.Max(1f, (cells.Count - 1) / (float)Mathf.Max(1, count - 1));
            for (int i = 0; i < count; i++)
            {
                int idx = Mathf.Min(cells.Count - 1, Mathf.RoundToInt(i * step));
                var cell = cells[idx];
                // Nobody stands in a river to hold a shape: bad cells walk FORWARD along
                // the drawn cells instead of drifting off-shape to the far side of a
                // fence or into the water.
                if (!cell.Standable(map) || cell.GetTerrain(map).IsWater)
                {
                    bool found = false;
                    for (int probe = idx + 1; probe < cells.Count && probe <= idx + 5; probe++)
                    {
                        var c2 = cells[probe];
                        if (c2.Standable(map) && !c2.GetTerrain(map).IsWater)
                        { cell = c2; found = true; break; }
                    }
                    if (!found) continue;
                }
                if (!slots.Contains(cell)) slots.Add(cell);
            }
            return slots;
        }

        // Greedy nearest-slot assignment, then holds do the rest: walk there, fight from
        // there, deviate on judgment, report up.
        public static int AssignHolds(Pawn issuer, List<Pawn> team,
            List<IntVec3> slots, Map map)
        {
            var hold = map.GetComponent<HoldMapComponent>();
            if (hold == null) return 0;
            if (slots.Count == 0)
            {
                for (int i = 0; i < team.Count; i++)
                    if (team[i] != null && hold.IsHolding(team[i]))
                        hold.Release(team[i]);
                return 0;
            }
            var freeSlots = new List<IntVec3>(slots);
            var remaining = new List<Pawn>(team);
            var assignedPawns = new HashSet<int>();
            int assigned = 0;
            int episode = CACombatIntent.NewEpisode();
            while (remaining.Count > 0 && freeSlots.Count > 0)
            {
                Pawn bestPawn = null;
                int bestSlot = -1;
                float bestDist = float.MaxValue;
                for (int i = 0; i < remaining.Count; i++)
                    for (int j = 0; j < freeSlots.Count; j++)
                    {
                        if (!remaining[i].CanReach(freeSlots[j], PathEndMode.OnCell,
                            Danger.Deadly)) continue;
                        float d = remaining[i].Position.DistanceTo(freeSlots[j]);
                        if (d < bestDist) { bestDist = d; bestPawn = remaining[i]; bestSlot = j; }
                    }
                if (bestPawn == null) break;
                // watch OUTWARD: away from the team's own side of the drawn shape
                var slot = freeSlots[bestSlot];
                var centroid = Vector3.zero;
                for (int i = 0; i < team.Count; i++) centroid += team[i].Position.ToVector3();
                centroid /= team.Count;
                var outDir = (slot.ToVector3() - centroid).normalized;
                var watchPt = slot + IntVec3.FromVector3(outDir * 12f);
                CAIntentContext context = issuer == null
                    ? new CAIntentContext(episode, CAIntentOrigin.OperatorDirect,
                        CAIntentController.Formation, bestPawn.thingIDNumber)
                    : CACombatIntent.Operator(issuer, bestPawn,
                        CAIntentController.Formation, episode);
                bool accepted = hold.OrderHoldWatching(bestPawn, slot, watchPt,
                    context);
                if (accepted)
                {
                    assigned++;
                    assignedPawns.Add(bestPawn.thingIDNumber);
                    freeSlots.RemoveAt(bestSlot);
                }
                remaining.Remove(bestPawn);
            }

            // A newly painted formation replaces the previous team intent. Pawns that
            // could not take any new slot must not silently retain stale old Holds.
            for (int i = 0; i < team.Count; i++)
                if (team[i] != null
                    && !assignedPawns.Contains(team[i].thingIDNumber)
                    && hold.IsHolding(team[i])) hold.Release(team[i]);
            return assigned;
        }
    }

    // The geometry layer: shapes drawn OVER the grid, rasterized onto it post hoc.
    // The grid stays the substrate; the overlay decides what the cells mean - a
    // formation of colonists or a run of wall blueprints, along any shape.
    public static class ShapeKit
    {
        public static List<IntVec3> Polyline(List<IntVec3> verts)
        {
            var cells = new List<IntVec3>();
            if (verts.Count == 0) return cells;
            cells.Add(verts[0]);
            for (int i = 1; i < verts.Count; i++)
            {
                foreach (var c in GenSight.PointsOnLineOfSight(verts[i - 1], verts[i]))
                    if (!cells.Contains(c)) cells.Add(c);
                if (!cells.Contains(verts[i])) cells.Add(verts[i]);
            }
            return cells;
        }

        // Ring cells ordered by angle, so slot spacing walks the circumference.
        public static List<IntVec3> Circle(IntVec3 center, float radius)
        {
            var cells = new List<IntVec3>();
            int limit = GenRadial.NumCellsInRadius(radius + 0.9f);
            for (int i = 0; i < limit; i++)
            {
                var c = center + GenRadial.RadialPattern[i];
                float d = c.DistanceTo(center);
                if (d >= radius - 0.6f && d <= radius + 0.6f) cells.Add(c);
            }
            cells.Sort(delegate (IntVec3 a, IntVec3 b)
            {
                float aa = Mathf.Atan2(a.z - center.z, a.x - center.x);
                float bb = Mathf.Atan2(b.z - center.z, b.x - center.x);
                return aa.CompareTo(bb);
            });
            return cells;
        }

        public static List<IntVec3> Triangle(IntVec3 a, IntVec3 b, IntVec3 c)
        {
            return Polyline(new List<IntVec3> { a, b, c, a });
        }
    }

    // Shape capture by clicked vertices - reliable at any speed, no drag dependency.
    // Freeform: each click adds a point, the line snakes through all of them; click
    // the last point again (or Escape) to commit. Circle: center, then radius. Triangle:
    // three corners. Live preview the whole way.
    public static class PaintManager
    {
        public const int ShapeFreeform = 0;
        public const int ShapeCircle = 1;
        public const int ShapeTriangle = 2;

        private static bool collecting;
        private static Map map;
        private static List<Pawn> team;
        private static Pawn issuer;
        private static int shape;
        private static readonly List<IntVec3> verts = new List<IntVec3>();

        public static void OpenShapeMenu(Pawn orderIssuer, List<Pawn> forTeam,
            Map m)
        {
            var opts = new List<FloatMenuOption>
            {
                new FloatMenuOption("Line (click points, click the last again to set)",
                    delegate { Start(orderIssuer, forTeam, m, ShapeFreeform); }),
                new FloatMenuOption("Triangle (click three corners)",
                    delegate { Start(orderIssuer, forTeam, m, ShapeTriangle); }),
                new FloatMenuOption("Circle (click center, then radius)",
                    delegate { Start(orderIssuer, forTeam, m, ShapeCircle); })
            };
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private static void Start(Pawn orderIssuer, List<Pawn> forTeam, Map m,
            int shapeMode)
        {
            map = m;
            team = forTeam;
            issuer = orderIssuer;
            shape = shapeMode;
            verts.Clear();
            collecting = true;
            NextClick();
        }

        private static void NextClick()
        {
            var tp = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false
            };
            Find.Targeter.BeginTargeting(tp, OnVertex, OnGui);
        }

        private static void OnVertex(LocalTargetInfo targ)
        {
            if (!collecting || !targ.Cell.InBounds(map)) { Reset(); return; }
            var c = targ.Cell;

            if (shape == ShapeFreeform && verts.Count > 0
                && c == verts[verts.Count - 1])
            {
                Commit();
                return;
            }
            verts.Add(c);
            if (shape == ShapeCircle && verts.Count == 2) { Commit(); return; }
            if (shape == ShapeTriangle && verts.Count == 3) { Commit(); return; }
            if (verts.Count >= 30) { Commit(); return; }
            // Chain the next click from INSIDE the callback: the targeter clears its
            // needsStopTargetingCall flag when BeginTargeting is called during the
            // action, so re-arming here is the sanctioned pattern - and it means a
            // fast Escape/right-click always cancels cleanly instead of being undone
            // by a deferred re-arm.
            NextClick();
        }

        private static void OnGui(LocalTargetInfo targ)
        {
            string label;
            if (shape == ShapeCircle)
                label = verts.Count == 0 ? "click the center" : "click the radius";
            else if (shape == ShapeTriangle)
                label = "corner " + (verts.Count + 1) + " of 3";
            else
                label = verts.Count == 0 ? "click the first point"
                    : "click to add - click the last point again to set";
            Widgets.MouseAttachedLabel(label, 0f, 0f, null);
        }

        // Preview + the escape-commit watcher, driven per frame from the map component.
        public static void Update(Map m)
        {
            if (!collecting) return;
            // Map switch mid-draw: the session is void - stop targeting and clear.
            if (m != map || Find.CurrentMap != map)
            {
                if (Find.Targeter.IsTargeting) Find.Targeter.StopTargeting();
                Reset();
                return;
            }

            // draw what exists so far plus the segment to the mouse
            var mouse = UI.MouseCell();
            if (shape == ShapeCircle && verts.Count == 1)
            {
                GenDraw.DrawRadiusRing(verts[0], Mathf.Max(1f, verts[0].DistanceTo(mouse)));
            }
            else if (verts.Count > 0)
            {
                for (int i = 1; i < verts.Count; i++)
                    GenDraw.DrawLineBetween(verts[i - 1].ToVector3Shifted(), verts[i].ToVector3Shifted());
                if (mouse.InBounds(m) && Find.Targeter.IsTargeting)
                    GenDraw.DrawLineBetween(verts[verts.Count - 1].ToVector3Shifted(), mouse.ToVector3Shifted());
                if (shape == ShapeTriangle && verts.Count == 2 && mouse.InBounds(m) && Find.Targeter.IsTargeting)
                    GenDraw.DrawLineBetween(mouse.ToVector3Shifted(), verts[0].ToVector3Shifted());
            }

            if (!Find.Targeter.IsTargeting)
            {
                // Escape/right-click ended targeting: a line with enough points
                // commits; else cancel. (Chaining now happens inside OnVertex, so a
                // dead targeter here always means the player ended the session.)
                if (shape == ShapeFreeform && verts.Count >= 2) Commit();
                else Reset();
            }
        }

        private static void Commit()
        {
            collecting = false;
            List<IntVec3> cells;
            if (shape == ShapeCircle && verts.Count >= 2)
                cells = ShapeKit.Circle(verts[0], Mathf.Max(1f, verts[0].DistanceTo(verts[1])));
            else if (shape == ShapeTriangle && verts.Count >= 3)
                cells = ShapeKit.Triangle(verts[0], verts[1], verts[2]);
            else if (verts.Count >= 2)
                cells = ShapeKit.Polyline(new List<IntVec3>(verts));
            else { Reset(); return; }

            if (team != null && team.Count > 0)
            {
                var slots = LineOrders.PickSlots(cells, team.Count, map);
                int assigned = LineOrders.AssignHolds(issuer, team, slots, map);
                if (assigned > 0)
                {
                    LineDrawMapComponent.NotePath(map, cells);
                    Messages.Message("Drawn - " + assigned + " taking positions.",
                        new LookTargets(cells[cells.Count / 2], map),
                        MessageTypeDefOf.SilentInput, false);
                }
                else
                    Messages.Message("No selected fighter can reach that formation.",
                        MessageTypeDefOf.RejectInput, false);
            }
            Reset();
        }

        private static void Reset()
        {
            collecting = false;
            verts.Clear();
            team = null;
            issuer = null;
            map = null; // never retain a discarded map
        }

        public static void DrawPath(List<IntVec3> cells)
        {
            for (int i = 1; i < cells.Count; i++)
                GenDraw.DrawLineBetween(cells[i - 1].ToVector3Shifted(), cells[i].ToVector3Shifted());
        }
    }

    // The painted intent stays visible for a stretch so the plan reads on screen.
    public class LineDrawMapComponent : MapComponent
    {
        private readonly List<IntVec3> persist = new List<IntVec3>();
        private int drawUntil;

        public LineDrawMapComponent(Map map) : base(map) { }

        public static void Note(Map map, IntVec3 a, IntVec3 b)
        {
            NotePath(map, new List<IntVec3> { a, b });
        }

        public static void NotePath(Map map, List<IntVec3> cells)
        {
            var c = map.GetComponent<LineDrawMapComponent>();
            if (c == null) return;
            c.persist.Clear();
            c.persist.AddRange(cells);
            c.drawUntil = Find.TickManager.TicksGame + 1800;
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            PaintManager.Update(map);
            if (persist.Count < 2) return;
            if (Find.TickManager.TicksGame > drawUntil) { persist.Clear(); return; }
            PaintManager.DrawPath(persist);
        }
    }
}
