using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Module 11: buddy carries. A pawn carrying a downed casualty moves faster when an able
    // same-side ally is adjacent - +25% base assist, +50% with a strong helper on decent
    // ground. Applies to raider extractions and colonist rescues alike.
    public class BuddyCarryMapComponent : MapComponent
    {
        private const int IntervalTicks = 250;
        private HediffDef assistDef;
        private bool resolved;

        public BuddyCarryMapComponent(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % IntervalTicks != 0) return;
            var s = AwarenessMod.Settings;
            if (s == null || !s.buddyCarry) return;
            try
            {
                if (!resolved)
                {
                    assistDef = DefDatabase<HediffDef>.GetNamedSilentFail("CA_AssistedCarry");
                    resolved = true;
                }
                if (assistDef == null) return;

                var pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    var carrier = pawns[i];
                    if (carrier == null || carrier.Dead || !carrier.RaceProps.Humanlike) continue;

                    var existing = carrier.health.hediffSet.GetFirstHediffOfDef(assistDef);
                    var carried = carrier.carryTracker != null ? carrier.carryTracker.CarriedThing as Pawn : null;
                    bool carryingCasualty = carried != null && carried.Downed && !carried.Dead;

                    Pawn helper = carryingCasualty ? FindHelper(carrier) : null;
                    if (!carryingCasualty || helper == null)
                    {
                        if (existing != null) carrier.health.RemoveHediff(existing);
                        continue;
                    }

                    float severity = 0.5f;
                    if (helper.BodySize >= 0.95f && !SquadComponent.CombatLiability(helper)) severity = 1.0f;
                    if (carrier.Position.InBounds(map))
                    {
                        var terrain = carrier.Position.GetTerrain(map);
                        if (terrain != null && terrain.pathCost > 10) severity = 0.5f;
                    }

                    if (existing == null)
                    {
                        existing = HediffMaker.MakeHediff(assistDef, carrier);
                        carrier.health.AddHediff(existing);
                    }
                    if (existing.Severity != severity) existing.Severity = severity;
                }
            }
            catch { }
        }

        private Pawn FindHelper(Pawn carrier)
        {
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                var h = pawns[i];
                if (h == null || h == carrier || h.Dead || h.Downed) continue;
                if (!h.RaceProps.Humanlike) continue;
                if (h.Faction != carrier.Faction) continue;
                if (h.DevelopmentalStage != DevelopmentalStage.Adult) continue;
                if (h.carryTracker != null && h.carryTracker.CarriedThing != null) continue;
                if (!h.Position.InHorDistOf(carrier.Position, 2.9f)) continue;
                return h;
            }
            return null;
        }
    }
}
