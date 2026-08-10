using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // THIRST, HYGIENE AND BLADDER [Bad Hygiene port, step 1 of the
    // water order]. Ported from Dubs Bad Hygiene by Dubwise56, used by
    // permission - see CREDITS.md. Only the needs and the body states
    // they drive live here.
    //
    // These are BODY STATE, nothing else. A need falls, and what it
    // reaches is written into the pawn's health as a hediff, which is
    // where the engine already keeps "what this body can currently do"
    // and where CA already keeps body weight. Nothing here decides what
    // anybody should do about it: no jobs are issued, no targets are
    // chosen, no pawn is told to go and drink. What a person or an
    // organization does about a dry throat belongs to the machinery that
    // already handles wanting things, and to the water that does not
    // physically exist yet.
    //
    // The rates are per NeedInterval, which the engine runs every 150
    // ticks - 400 of them to the day. They are stated as "days to
    // empty" so they can be argued with.
    internal static class CANeedTuning
    {
        internal const int IntervalsPerDay = 400;

        // A body without water fails in about a day of ordinary
        // activity. Heat and work shorten that - the reason water
        // planning is done by climate and workload rather than by
        // headcount alone.
        internal const float ThirstDaysToEmpty = 1f;
        internal const float HygieneDaysToEmpty = 2f;
        internal const float BladderDaysToEmpty = 0.5f;

        internal static float PerInterval(float daysToEmpty)
        {
            return 1f / (daysToEmpty * IntervalsPerDay);
        }

        // Work and heat both raise water loss. Exertion is read off
        // what the body is doing, not off a job whitelist, so drafted
        // marching, hauling and mining all count without being listed.
        internal static float ThirstMultiplier(Pawn p)
        {
            float m = 1f;
            try
            {
                if (p.pather != null && p.pather.MovingNow) m += 0.35f;
                float carried = CarriedFraction(p);
                m += carried * 0.5f;
                float ambient = p.AmbientTemperature;
                float comfortMax = p.ComfortableTemperatureRange().max;
                if (ambient > comfortMax)
                    m += Mathf.Min(1.5f, (ambient - comfortMax) * 0.06f);
                if (p.health?.hediffSet != null
                    && p.health.hediffSet.BleedRateTotal > 0.01f)
                    m += p.health.hediffSet.BleedRateTotal * 0.5f;
            }
            catch { }
            return m;
        }

        // How loaded the body is, as a share of what it can carry.
        // Carried mass is already the engine's own accounting, so this
        // reads the same number the rucks feed.
        internal static float CarriedFraction(Pawn p)
        {
            try
            {
                float cap = MassUtility.Capacity(p);
                if (cap <= 0f) return 0f;
                return Mathf.Clamp01(MassUtility.GearAndInventoryMass(p)
                    / cap);
            }
            catch { return 0f; }
        }
    }

    // A need whose emptiness is written into the body.
    //
    // THE NEED IS THE ONLY COPY OF THIS STATE. How long a body has gone
    // without is counted here, in `deprivation`, and the hediff is set
    // to match it every interval - never incremented, never read back.
    // A hediff that accumulated on its own would be a second, competing
    // account of the same body, and the two would drift apart the first
    // time anything touched one and not the other. Anything that
    // changes the severity from outside is overwritten on the next
    // interval, which is what "the need is authoritative" has to mean
    // if it is to mean anything.
    public abstract class CANeed_Body : Need
    {
        // 0 = the body owes nothing, 1 = as far as this state goes
        private float deprivation;

        protected CANeed_Body(Pawn pawn) : base(pawn) { }

        public override int GUIChangeArrow => -1;

        protected abstract float FallPerInterval { get; }

        // The hediff that MIRRORS this need, and how fast the debt is
        // taken on and paid off. Null means the need shows nowhere on
        // the body.
        protected virtual HediffDef BodyState => null;
        protected virtual float DeprivationPerInterval => 0f;
        protected virtual float RecoveryPerInterval => 0f;

        // Below this the body starts paying for it.
        protected virtual float Threshold => 0f;

        public float Deprivation => deprivation;

        // A body only pays for what it is somewhere it could have done
        // something about. On the world map there is no ground to drink
        // from, no stream to wash in and nowhere to go - caravan supply
        // is its own problem and it is not modelled yet, so these hold
        // rather than quietly killing everyone who leaves the map. This
        // is a STATED BOUNDARY, not a rule about bodies: it comes out
        // the moment caravans carry water.
        protected override bool IsFrozen
        {
            get
            {
                if (base.IsFrozen) return true;
                try { return pawn != null && !pawn.Spawned; }
                catch { return false; }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref deprivation, "CA_deprivation", 0f);
        }

        public override void SetInitialLevel()
        {
            CurLevel = def.baseLevel;
            deprivation = 0f;
        }

        public override void NeedInterval()
        {
            if (!IsFrozen) CurLevel -= FallPerInterval;
            deprivation = Mathf.Clamp01(deprivation
                + (CurLevel <= Threshold
                    ? DeprivationPerInterval
                    : -RecoveryPerInterval));
            MirrorToBody();
        }

        private void MirrorToBody()
        {
            HediffDef state = BodyState;
            if (state == null || pawn?.health?.hediffSet == null) return;
            try
            {
                Hediff present = pawn.health.hediffSet
                    .GetFirstHediffOfDef(state);
                if (deprivation <= 0f)
                {
                    if (present != null) pawn.health.RemoveHediff(present);
                    return;
                }
                if (present == null)
                {
                    present = HediffMaker.MakeHediff(state, pawn);
                    pawn.health.AddHediff(present);
                }
                present.Severity = deprivation;
            }
            catch { }
        }
    }

    // Water. Falls faster under load, under work, in heat, and while
    // bleeding; at zero the body begins to fail, which is the vanilla
    // dehydration hediff doing the work.
    public class CANeed_Thirst : CANeed_Body
    {
        public CANeed_Thirst(Pawn pawn) : base(pawn) { }

        protected override float FallPerInterval =>
            CANeedTuning.PerInterval(CANeedTuning.ThirstDaysToEmpty)
                * CANeedTuning.ThirstMultiplier(pawn);

        protected override HediffDef BodyState =>
            DefDatabase<HediffDef>.GetNamedSilentFail("CA_Dehydration");

        // Lethal severity is 1. Emptied and left, a body reaches that
        // in about three days; water eases it back several times
        // faster than thirst brought it on, which is what rehydration
        // actually looks like.
        protected override float DeprivationPerInterval =>
            1f / (3f * CANeedTuning.IntervalsPerDay);

        protected override float RecoveryPerInterval =>
            1f / (0.5f * CANeedTuning.IntervalsPerDay);
    }

    // Cleanliness. Its cost is disease risk and how others take you,
    // which the ported hediff already states.
    public class CANeed_Hygiene : CANeed_Body
    {
        public CANeed_Hygiene(Pawn pawn) : base(pawn) { }

        protected override float FallPerInterval =>
            CANeedTuning.PerInterval(CANeedTuning.HygieneDaysToEmpty);

        protected override HediffDef BodyState =>
            DefDatabase<HediffDef>.GetNamedSilentFail("CA_BadHygiene");

        // Filthiness is a matter of degree well before it is total, so
        // this one starts being paid for at a quarter full.
        protected override float Threshold => 0.25f;

        protected override float DeprivationPerInterval =>
            1f / (2f * CANeedTuning.IntervalsPerDay);

        protected override float RecoveryPerInterval =>
            1f / (0.25f * CANeedTuning.IntervalsPerDay);
    }

    // Waste. Where it ends up is what decides whether it comes back as
    // disease - but that is the fixtures' business. Here it only fills,
    // and a body that is given nowhere to go eventually goes anyway.
    public class CANeed_Bladder : CANeed_Body
    {
        private const int SoilCooldownTicks = 30000;
        private int lastSoiledTick = -99999;

        public CANeed_Bladder(Pawn pawn) : base(pawn) { }

        protected override float FallPerInterval =>
            CANeedTuning.PerInterval(CANeedTuning.BladderDaysToEmpty);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastSoiledTick, "CA_lastSoiled",
                -99999);
        }

        public override void NeedInterval()
        {
            base.NeedInterval();
            if (CurLevel > 0f) return;
            int now = Find.TickManager.TicksGame;
            if (now - lastSoiledTick < SoilCooldownTicks) return;
            lastSoiledTick = now;
            CurLevel = 1f;
            try
            {
                ThoughtDef soiled = DefDatabase<ThoughtDef>
                    .GetNamedSilentFail("CA_SoiledSelf");
                if (soiled != null)
                    pawn.needs?.mood?.thoughts?.memories
                        ?.TryGainMemory(soiled);
                ThingDef urine = DefDatabase<ThingDef>
                    .GetNamedSilentFail("CA_FilthUrine");
                if (pawn.Spawned && urine != null)
                    FilthMaker.TryMakeFilth(pawn.Position, pawn.Map,
                        urine, pawn.LabelShort);
                // and it costs them their cleanliness
                Need hygiene = pawn.needs?.TryGetNeed(
                    DefDatabase<NeedDef>.GetNamedSilentFail(
                        "CA_Hygiene"));
                if (hygiene != null) hygiene.CurLevel -= 0.35f;
            }
            catch { }
        }
    }
}
