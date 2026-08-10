using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // What is in the water, kept as two separate facts because they
    // are two separate problems with two separate answers. Boiling
    // clears contamination and does nothing to salt; distilling
    // removes salt and costs most of the volume. A model with one
    // "quality" axis cannot express either honestly.
    public enum CAWaterSalinity : byte
    {
        Fresh = 0,
        Brackish = 1,
        Saline = 2
    }

    // WATER CONTAINERS [Bad Hygiene port, step 2]. Bulk water is what a
    // place HOLDS; a container is what a person CARRIES. Conflating
    // them is what makes carried supply meaningless - a cistern does a
    // patrol no good twenty miles out.
    //
    // A vessel holds a QUANTITY plus the state of what is in it. That
    // is the whole simulation: no fluid dynamics, no pressure, no
    // mixing chemistry. Water filled from a foul stream stays foul
    // until someone boils it. Sea water stays sea water. What changes
    // it is a process someone performs, not time or wishing.
    public class CompProperties_WaterVessel : CompProperties
    {
        // litres, which are also kilograms
        public int capacity = 2;

        // A sealed vessel keeps what is put in it. An open one - a
        // bucket, a barrel with a lid off - takes in whatever is
        // around, so water left standing in it goes stagnant.
        public bool sealed_ = true;

        // How long open water stands before it is stagnant, in days.
        public float daysToStagnateOpen = 2f;

        public CompProperties_WaterVessel()
        {
            compClass = typeof(CompWaterVessel);
        }
    }

    public class CompWaterVessel : ThingComp
    {
        private int units;
        private CAWaterSalinity salinity = CAWaterSalinity.Fresh;
        private bool contaminated;
        private int filledTick = -99999;

        public CompProperties_WaterVessel Props =>
            (CompProperties_WaterVessel)props;

        public int Units => units;
        public int Capacity => Props.capacity;
        public int Space => Mathf.Max(0, Capacity - units);
        public bool Empty => units <= 0;
        public CAWaterSalinity Salinity => salinity;
        public bool Contaminated => contaminated;
        public bool Sealed => Props.sealed_;

        // A body can use fresh water whether or not it is foul. It
        // cannot use salt water at all, which is why salinity is a
        // hard gate here and contamination is only a price.
        public bool Drinkable => units > 0
            && salinity == CAWaterSalinity.Fresh;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref units, "CA_units", 0);
            Scribe_Values.Look(ref salinity, "CA_salinity",
                CAWaterSalinity.Fresh);
            Scribe_Values.Look(ref contaminated, "CA_contaminated",
                false);
            Scribe_Values.Look(ref filledTick, "CA_filledTick", -99999);
        }

        // Mixing states is refused rather than averaged. A canteen with
        // a mouthful of sea water in it is a canteen of sea water, and
        // averaging would quietly launder salt into the drinking
        // supply.
        public bool Accepts(CAWaterSalinity kind, bool foul)
        {
            if (Empty) return true;
            return salinity == kind && contaminated == foul;
        }

        public int Fill(CAWaterSalinity kind, bool foul, int wanted)
        {
            if (wanted <= 0 || !Accepts(kind, foul)) return 0;
            int took = Mathf.Min(wanted, Space);
            if (took <= 0) return 0;
            if (Empty)
            {
                salinity = kind;
                contaminated = foul;
                filledTick = Find.TickManager.TicksGame;
            }
            units += took;
            return took;
        }

        public int Draw(int wanted)
        {
            if (Empty || wanted <= 0) return 0;
            int gave = Mathf.Min(wanted, units);
            units -= gave;
            if (units <= 0) Clear();
            return gave;
        }

        public void Clear()
        {
            units = 0;
            salinity = CAWaterSalinity.Fresh;
            contaminated = false;
        }

        // Boiling: kills what is living in it, leaves the salt.
        public void Treat()
        {
            if (units > 0) contaminated = false;
            filledTick = Find.TickManager.TicksGame;
        }

        // Distilling: takes the salt out, and most of the volume with
        // it. The caller supplies what survives.
        public void Distil(int surviving)
        {
            if (units <= 0) return;
            units = Mathf.Clamp(surviving, 0, Capacity);
            salinity = CAWaterSalinity.Fresh;
            contaminated = false;
            filledTick = Find.TickManager.TicksGame;
            if (units <= 0) Clear();
        }

        // STAGNATION, not spoilage. Nothing rots: water sitting open
        // to the air takes in dust, insects and whatever falls in, and
        // what was already in it multiplies in the warm and the still.
        // Calling that "spoiling" would hide the physical fact and the
        // remedy - it is the same contamination boiling removes, so
        // stagnant water is not ruined, it is untreated again.
        public override void CompTick()
        {
            if (Props.sealed_ || contaminated || units <= 0) return;
            if (!parent.IsHashIntervalTick(2000)) return;
            int age = Find.TickManager.TicksGame - filledTick;
            if (age > Props.daysToStagnateOpen * 60000f)
                contaminated = true;
        }

        public override string CompInspectStringExtra()
        {
            if (Empty) return "Empty" + (Props.sealed_ ? "" : " (open)");
            string what = salinity == CAWaterSalinity.Saline
                ? "sea water"
                : salinity == CAWaterSalinity.Brackish
                    ? "brackish water"
                    : contaminated ? "untreated water" : "water";
            string s = what + ": " + units + " / " + Capacity + " L";
            if (Props.sealed_ || salinity != CAWaterSalinity.Fresh)
                return s;
            if (contaminated)
                return s + "\nStanding open - stagnant. Boil before"
                    + " drinking.";
            int left = Mathf.Max(0, Mathf.RoundToInt(
                Props.daysToStagnateOpen
                - (Find.TickManager.TicksGame - filledTick) / 60000f));
            return s + "\nStanding open - will go stagnant in about "
                + left + " days.";
        }

        public override string TransformLabel(string label)
        {
            if (Empty) return label + " (empty)";
            return label + " (" + units + "L)";
        }
    }

    // A full container weighs what it holds. The engine already asks a
    // Thing for its mass through a stat, and this mod already extends a
    // stat this way for rucks, so the water joins the same sum - and
    // the load falls as it is drunk without anything tracking it.
    public class StatPart_WaterVesselMass : StatPart
    {
        public override void TransformValue(StatRequest req,
            ref float val)
        {
            CompWaterVessel v = Vessel(req);
            if (v != null) val += v.Units;   // a litre is a kilogram
        }

        public override string ExplanationPart(StatRequest req)
        {
            CompWaterVessel v = Vessel(req);
            if (v == null || v.Empty) return null;
            return "Contents: " + v.Units + " L (+" + v.Units + " kg)";
        }

        private static CompWaterVessel Vessel(StatRequest req)
        {
            return (req.Thing as ThingWithComps)
                ?.GetComp<CompWaterVessel>();
        }
    }

    internal static class CAVessels
    {
        // Every vessel a pawn has on them, emptiest first, so a
        // part-used canteen is finished before a full one is opened.
        internal static List<CompWaterVessel> Carried(Pawn pawn)
        {
            var found = new List<CompWaterVessel>();
            if (pawn?.inventory?.innerContainer != null)
                foreach (Thing t in pawn.inventory.innerContainer)
                {
                    var v = (t as ThingWithComps)
                        ?.GetComp<CompWaterVessel>();
                    if (v != null) found.Add(v);
                }
            if (pawn?.apparel?.WornApparel != null)
                foreach (Apparel a in pawn.apparel.WornApparel)
                {
                    var v = a.GetComp<CompWaterVessel>();
                    if (v != null) found.Add(v);
                }
            found.Sort((a, b) => a.Units.CompareTo(b.Units));
            return found;
        }

        internal static CompWaterVessel FirstDrinkable(Pawn pawn)
        {
            foreach (CompWaterVessel v in Carried(pawn))
                if (v.Drinkable) return v;
            return null;
        }

        internal static int DrinkableUnits(Pawn pawn)
        {
            int n = 0;
            foreach (CompWaterVessel v in Carried(pawn))
                if (v.Drinkable) n += v.Units;
            return n;
        }
    }
}
