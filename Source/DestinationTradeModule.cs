using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // DESTINATION TRADE: arrival has an economy. The settlement's treasury
    // is the same conserved purse that pays tribute and support; its
    // stores are the same real items its kits seeded. Selling converts
    // their abstract wealth to silver in your hand and drains their purse;
    // buying unforbids their real shelf goods and fills it. No trade
    // screen, no phantom stock - a market held where you stand.
    public static class CADestinationTrade
    {
        public static void OpenMenu(Pawn trader, CAOrganization org,
            CARegionalSettlementRecord record, Map map)
        {
            var opts = new List<FloatMenuOption>();

            int carriedValue = CarriedValue(trader, map, out int items);
            int theyCanPay = (int)Mathf.Min(carriedValue * 0.85f,
                org.treasury);
            if (items > 0 && theyCanPay > 0)
                opts.Add(new FloatMenuOption("sell carried goods - they"
                    + " pay up to " + theyCanPay + " silver (treasury "
                    + (int)org.treasury + ")", delegate
                {
                    Sell(trader, org, map);
                }));
            else
                opts.Add(new FloatMenuOption(items == 0
                    ? "nothing carried to sell (porters carry goods in"
                    + " inventory)"
                    : "their treasury is empty - they cannot buy", null));

            var wares = StoreWares(record, map);
            for (int i = 0; i < wares.Count && i < 6; i++)
            {
                Thing w = wares[i];
                int price = Mathf.CeilToInt(w.MarketValue * w.stackCount
                    * 1.15f);
                opts.Add(new FloatMenuOption("buy " + w.LabelCap + " - "
                    + price + " silver", delegate
                {
                    Buy(trader, org, w, price, map);
                }));
            }
            if (wares.Count == 0)
                opts.Add(new FloatMenuOption(
                    "their stores hold nothing for sale", null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private static int CarriedValue(Pawn trader, Map map,
            out int items)
        {
            items = 0;
            float total = 0f;
            foreach (Pawn p in PartyOf(trader, map))
                if (p.inventory != null)
                    for (int i = 0;
                        i < p.inventory.innerContainer.Count; i++)
                    {
                        Thing t = p.inventory.innerContainer[i];
                        if (t.def == ThingDefOf.Silver) continue;
                        total += t.MarketValue * t.stackCount;
                        items++;
                    }
            return (int)total;
        }

        private static List<Pawn> PartyOf(Pawn trader, Map map)
        {
            var party = new List<Pawn> { trader };
            var cols = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < cols.Count; i++)
                if (cols[i] != trader
                    && cols[i].Position.InHorDistOf(trader.Position, 10f))
                    party.Add(cols[i]);
            return party;
        }

        private static void Sell(Pawn trader, CAOrganization org, Map map)
        {
            int paid = 0;
            int sold = 0;
            foreach (Pawn p in PartyOf(trader, map))
            {
                if (p.inventory == null) continue;
                for (int i = p.inventory.innerContainer.Count - 1;
                    i >= 0; i--)
                {
                    Thing t = p.inventory.innerContainer[i];
                    if (t.def == ThingDefOf.Silver) continue;
                    int price = Mathf.FloorToInt(t.MarketValue
                        * t.stackCount * 0.85f);
                    if (price <= 0 || org.treasury < price) continue;
                    org.treasury -= price;
                    paid += price;
                    sold += t.stackCount;
                    t.Destroy();
                }
            }
            if (paid <= 0)
            {
                Messages.Message("No sale - nothing they could afford.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = paid;
            GenPlace.TryPlaceThing(silver, trader.Position, map,
                ThingPlaceMode.Near);
            org.Record("relations", "bought " + sold + " goods from "
                + "the colony's traders for " + paid + " silver");
            CAOrganizationWorldComponent.Current?.EnsureColony().Record(
                "relations", "sold " + sold + " goods at " + org.name
                + " for " + paid + " silver");
            Messages.Message("Sold " + sold + " goods at " + org.name
                + " for " + paid + " silver (their treasury now "
                + (int)org.treasury + ").",
                MessageTypeDefOf.PositiveEvent, false);
        }

        private static List<Thing> StoreWares(
            CARegionalSettlementRecord record, Map map)
        {
            var wares = new List<Thing>();
            if (record.localRect == CellRect.Empty) return wares;
            foreach (IntVec3 c in record.localRect)
            {
                List<Thing> things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing t = things[i];
                    if (t.def == null
                        || t.def.category != ThingCategory.Item) continue;
                    if (!t.IsForbidden(Faction.OfPlayer)) continue;
                    wares.Add(t);
                    if (wares.Count >= 12) return wares;
                }
            }
            return wares;
        }

        private static void Buy(Pawn trader, CAOrganization org,
            Thing ware, int price, Map map)
        {
            if (ware == null || ware.Destroyed) return;
            if (!TryPaySilver(trader, map, price))
            {
                Messages.Message("Not enough silver in hand - " + price
                    + " needed, carried or on the ground beside you.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            org.treasury += price;
            ware.SetForbidden(false, false);
            org.Record("relations", "sold " + ware.LabelCap
                + " to the colony's traders for " + price + " silver");
            CAOrganizationWorldComponent.Current?.EnsureColony().Record(
                "relations", "bought " + ware.LabelCap + " at " + org.name
                + " for " + price + " silver");
            Messages.Message("Bought " + ware.LabelCap + " for " + price
                + " silver - it is yours to haul.",
                new LookTargets(ware.Position, map),
                MessageTypeDefOf.PositiveEvent, false);
        }

        private static bool TryPaySilver(Pawn trader, Map map, int amount)
        {
            var sources = new List<Thing>();
            foreach (Pawn p in PartyOf(trader, map))
                if (p.inventory != null)
                    for (int i = 0;
                        i < p.inventory.innerContainer.Count; i++)
                        if (p.inventory.innerContainer[i].def
                            == ThingDefOf.Silver)
                            sources.Add(p.inventory.innerContainer[i]);
            foreach (IntVec3 c in GenRadial.RadialCellsAround(
                trader.Position, 8f, true))
            {
                if (!c.InBounds(map)) continue;
                List<Thing> things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                    if (things[i].def == ThingDefOf.Silver
                        && !things[i].IsForbidden(Faction.OfPlayer))
                        sources.Add(things[i]);
            }
            int have = 0;
            for (int i = 0; i < sources.Count; i++)
                have += sources[i].stackCount;
            if (have < amount) return false;
            int owed = amount;
            for (int i = 0; i < sources.Count && owed > 0; i++)
            {
                int take = Mathf.Min(owed, sources[i].stackCount);
                sources[i].SplitOff(take).Destroy();
                owed -= take;
            }
            return true;
        }
    }
}
