using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Module: named contingency plans (PROTOTYPE tier - manual, visible,
    // doctrine-shaped; refined by play). One NAME = one PLAN = an ordered
    // route of legs ("north 600, then northwest 300"). Authored from a
    // pawn's palette by clicking waypoints; rendered as a labeled route;
    // executed by name - the executor's squad moves leg by leg and rallies
    // on the final point. Color naming is unit-SOP style per the grounding
    // (GOLD/GREEN/BLACK presets); terms provisional pending the operator's
    // books.
    public class CAContingencyPlan : IExposable
    {
        public int id;
        public string name;
        public string description = "";
        public int ownerId;
        public List<IntVec3> legs = new List<IntVec3>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref description, "description", "");
            Scribe_Values.Look(ref ownerId, "ownerId");
            Scribe_Collections.Look(ref legs, "legs", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (legs == null) legs = new List<IntVec3>();
                if (description == null) description = "";
            }
        }
    }

    // The player names and describes their OWN plans - no preset vocabulary.
    // A small window: name field, description field, then leg-clicking.
    public class Dialog_CAPlanAuthor : Window
    {
        private readonly Pawn owner;
        private string planName = "";
        private string planDescription = "";

        public Dialog_CAPlanAuthor(Pawn owner)
        {
            this.owner = owner;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
        }

        public override Vector2 InitialSize => new Vector2(480f, 260f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f),
                owner.LabelShort + ": author a plan");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 40f, 120f, 28f), "Name");
            planName = Widgets.TextField(
                new Rect(120f, 40f, inRect.width - 120f, 28f), planName);
            Widgets.Label(new Rect(0f, 76f, 120f, 28f), "Description");
            planDescription = Widgets.TextArea(
                new Rect(120f, 76f, inRect.width - 120f, 80f),
                planDescription);
            bool nameOk = !planName.NullOrEmpty()
                && planName.Trim().Length > 0;
            if (Widgets.ButtonText(
                new Rect(inRect.width - 220f, inRect.height - 40f, 100f,
                    32f), "Cancel"))
                Close();
            if (Widgets.ButtonText(
                new Rect(inRect.width - 110f, inRect.height - 40f, 110f,
                    32f), nameOk ? "Click legs..." : "(name it)"))
            {
                if (nameOk)
                {
                    Close();
                    Find.DesignatorManager.Select(
                        new Designator_CAPlanLegs(owner,
                            planName.Trim(), planDescription ?? ""));
                }
            }
        }
    }

    public class CAPlanMapComponent : MapComponent
    {
        private List<CAContingencyPlan> plans =
            new List<CAContingencyPlan>();
        private int nextId = 1;

        private static readonly Color PlanColor =
            new Color(0.95f, 0.8f, 0.25f);

        public CAPlanMapComponent(Map map) : base(map) { }

        public static CAPlanMapComponent For(Map map)
        {
            return map?.GetComponent<CAPlanMapComponent>();
        }

        public List<CAContingencyPlan> All => plans;

        public CAContingencyPlan ByName(string name)
        {
            for (int i = 0; i < plans.Count; i++)
                if (plans[i].name == name) return plans[i];
            return null;
        }

        public CAContingencyPlan Create(Pawn owner, string name,
            string description, List<IntVec3> legs)
        {
            if (owner == null || legs == null || legs.Count == 0) return null;
            CAContingencyPlan existing = ByName(name);
            if (existing != null) plans.Remove(existing);
            var plan = new CAContingencyPlan
            {
                id = nextId++,
                name = name,
                description = description ?? "",
                ownerId = owner.thingIDNumber,
                legs = new List<IntVec3>(legs)
            };
            plans.Add(plan);
            CATrace.Pawn(owner, "contingency plan " + name + " AUTHORED - "
                + legs.Count + " leg(s), final rally " + legs[legs.Count - 1],
                anchor: owner.Position);
            Messages.Message(name + " plan authored: " + legs.Count
                + " leg(s).", new LookTargets(legs[0], map),
                MessageTypeDefOf.TaskCompletion, false);
            return plan;
        }

        // Execution: the executor and every same-squad member receive the
        // legs as queued movement, rallying on the final point (automatic
        // defense anchor, watching back along the approach). Receipted per
        // pawn per doctrine's "known before needed."
        public void Execute(Pawn executor, CAContingencyPlan plan)
        {
            if (executor == null || plan == null || plan.legs.Count == 0)
                return;
            int squad = SquadComponent.SquadOf(executor);
            var affected = new List<Pawn>();
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn member = colonists[i];
                if (member != executor
                    && (squad == 0
                        || SquadComponent.SquadOf(member) != squad)) continue;
                if (member.Downed || member.InMentalState) continue;
                affected.Add(member);
            }
            IntVec3 rally = plan.legs[plan.legs.Count - 1];
            IntVec3 approach = plan.legs.Count > 1
                ? plan.legs[plan.legs.Count - 2] : executor.Position;
            for (int i = 0; i < affected.Count; i++)
            {
                Pawn member = affected[i];
                if (member.CurJob != null && member.CurJob.playerForced)
                    continue;
                member.jobs.ClearQueuedJobs();
                for (int leg = 0; leg < plan.legs.Count; leg++)
                {
                    IntVec3 target = plan.legs[leg].Standable(map)
                        ? plan.legs[leg]
                        : CellFinder.StandableCellNear(plan.legs[leg], map,
                            3f, null);
                    if (!target.IsValid) continue;
                    Job move = JobMaker.MakeJob(JobDefOf.Goto, target);
                    move.locomotionUrgency = LocomotionUrgency.Jog;
                    if (leg == 0)
                        member.jobs.StartJob(move,
                            JobCondition.InterruptForced);
                    else
                        member.jobs.jobQueue.EnqueueLast(move, JobTag.Misc);
                }
                CATrace.Pawn(member, "EXECUTING " + plan.name + " plan - "
                    + plan.legs.Count + " leg(s) to rally " + rally,
                    destination: rally, anchor: member.Position);
            }
            CATactical.AssignAutomaticDefense(executor, rally, approach);
            Messages.Message("Executing " + plan.name + " plan ("
                + affected.Count + " moving).", new LookTargets(rally, map),
                MessageTypeDefOf.ThreatSmall, false);
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map || plans.Count == 0) return;
            for (int i = 0; i < plans.Count; i++)
            {
                var legs = plans[i].legs;
                for (int l = 0; l < legs.Count - 1; l++)
                    GenDraw.DrawLineBetween(
                        legs[l].ToVector3ShiftedWithAltitude(
                            AltitudeLayer.MetaOverlays),
                        legs[l + 1].ToVector3ShiftedWithAltitude(
                            AltitudeLayer.MetaOverlays));
            }
        }

        public override void MapComponentOnGUI()
        {
            if (Find.CurrentMap != map) return;
            for (int i = 0; i < plans.Count; i++)
            {
                if (plans[i].legs.Count == 0) continue;
                GenMapUI.DrawThingLabel(
                    GenMapUI.LabelDrawPosFor(plans[i].legs[0]),
                    plans[i].name + " plan", PlanColor);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref plans, "caPlans", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "caPlanNextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && plans == null)
                plans = new List<CAContingencyPlan>();
        }
    }

    // Leg capture: click waypoints in order; click the last waypoint again
    // to set the plan (the mod's own Line-tool grammar: "click points,
    // click the last again to set").
    public class Designator_CAPlanLegs : Designator
    {
        private readonly Pawn owner;
        private readonly string planName;
        private readonly string planDescription;
        private readonly List<IntVec3> legs = new List<IntVec3>();

        public Designator_CAPlanLegs(Pawn owner, string planName,
            string planDescription)
        {
            this.owner = owner;
            this.planName = planName;
            this.planDescription = planDescription;
            defaultLabel = owner.LabelShort + ": " + planName
                + " plan legs";
            defaultDesc = "Click waypoints in order; click the last "
                + "waypoint again to set the plan.";
            icon = TexCommand.RemoveRoutePlannerWaypoint;
            useMouseIcon = true;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            return c.InBounds(Map);
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (legs.Count > 0 && legs[legs.Count - 1] == c)
            {
                CAPlanMapComponent.For(Map)?.Create(owner, planName,
                    planDescription, legs);
                Find.DesignatorManager.Deselect();
                return;
            }
            legs.Add(c);
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
            for (int l = 0; l < legs.Count - 1; l++)
                GenDraw.DrawLineBetween(
                    legs[l].ToVector3ShiftedWithAltitude(
                        AltitudeLayer.MetaOverlays),
                    legs[l + 1].ToVector3ShiftedWithAltitude(
                        AltitudeLayer.MetaOverlays));
            if (legs.Count > 0)
            {
                IntVec3 mouse = UI.MouseCell();
                if (mouse.InBounds(Map))
                    GenDraw.DrawLineBetween(
                        legs[legs.Count - 1].ToVector3ShiftedWithAltitude(
                            AltitudeLayer.MetaOverlays),
                        mouse.ToVector3ShiftedWithAltitude(
                            AltitudeLayer.MetaOverlays));
            }
        }
    }

    // Status colors (PROTOTYPE, LACE family): GREEN/AMBER/RED/BLACK.
    // Honest mapping to what the game models: L=food, A=NOT MODELED
    // (vanilla has no ammunition system - reported as "no ammo model"),
    // C=casualty state (health/bleed/downed), E=equipment (weapon in
    // hand). Terms provisional pending the operator's books.
    public static class CAStatusReport
    {
        public static string ColorOf(float fraction)
        {
            if (fraction >= 0.8f) return "GREEN";
            if (fraction >= 0.5f) return "AMBER";
            if (fraction > 0.15f) return "RED";
            return "BLACK";
        }

        public static string For(Pawn pawn)
        {
            float liquids = pawn.needs?.food?.CurLevelPercentage ?? 1f;
            float casualty = pawn.Downed ? 0f
                : pawn.health.summaryHealth.SummaryHealthPercent;
            if (pawn.health.hediffSet.BleedRateTotal > 0.1f)
                casualty = Mathf.Min(casualty, 0.4f);
            bool armed = pawn.equipment?.Primary != null;
            return pawn.LabelShort
                + ": L " + ColorOf(liquids)
                + " / A no-ammo-model"
                + " / C " + ColorOf(casualty)
                + " / E " + (armed ? "GREEN" : "BLACK (unarmed)");
        }

        public static void SquadReport(Pawn requester, Map map)
        {
            int squad = SquadComponent.SquadOf(requester);
            var lines = new List<string>();
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
                if (squad == 0 || SquadComponent.SquadOf(colonists[i]) == squad)
                    lines.Add(For(colonists[i]));
            string report = string.Join("\n", lines);
            Find.LetterStack.ReceiveLetter(
                "LACE - " + requester.LabelShort + "'s element",
                report + "\n\n(A = not modeled: vanilla has no ammunition "
                + "system. Terms provisional.)",
                LetterDefOf.NeutralEvent);
            CATrace.Pawn(requester, "LACE report rendered for the element",
                anchor: requester.Position);
        }
    }
}
