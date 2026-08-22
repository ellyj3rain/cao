using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // WHAT A PLACE CAN ACTUALLY BUILD FROM. Material was never a fact the
    // world authored: the frontier shell was hard-coded to wood whatever
    // its holding said, the settlement fit-out kept its own local-rock
    // palette, and morphology fell through to the engine default. Three
    // uncoordinated answers, none of which asked who these people are or
    // what they have around them, and a persisted material level that
    // changed nothing that got built.
    //
    // A construction choice is the product of several facts at once:
    //
    //   development pressure     how built-up this place is becoming
    //   technological knowledge  can these people cut stone, work metal
    //   local resources          what rock is under this ground, do trees grow
    //   trade and access         can anything arrive from elsewhere
    //   population and capacity  can the labour and economy carry it
    //   available materials      what the thing can be made from at all
    //
    // giving the set of VALID construction choices, from which the
    // physical settlement is built.
    //
    //   culture                  what these people expect a place to be
    //
    // Development pressure ORDERS that set; it never selects a material by
    // itself. A poor settlement and a wealthy one on the same rock build
    // differently, and so do two equally developed places on different
    // ground.
    //
    // Culture enters through three of the registry's own questions,
    // read from the settlement's culture rather than invented here:
    // settlement permanence and expected comfort move what the place
    // builds TOWARD, and resource stewardship moves how hard it leans on
    // its own ground. None of them move what the place can carry, so
    // culture changes the character of a settlement without ever letting
    // it build past its means. A culture with no position on a question
    // reads as no position, and the place builds to its means alone.
    internal readonly struct CAConstructionContext
    {
        internal readonly Map Map;
        internal readonly CATechnologicalKnowledge Knowledge;
        // A holding's material level or a settlement's realized standing:
        // how built-up this place is becoming. One input, not the answer.
        internal readonly int DevelopmentPressure;
        internal readonly int Population;
        internal readonly int EconomicCapacity;
        internal readonly int TradeConnectivity;
        internal readonly string IdentitySeed;
        // Who these people are. Null where a site has no culture on
        // record, which reads as no cultural position rather than as a
        // default one.
        internal readonly CACulture Culture;
        // True when the build draws on what the settlement actually has
        // in hand rather than being created wholesale. Generation
        // creates a place and its fabric together; a settlement adding
        // to itself later can only use materials it really holds.
        internal readonly bool FromStores;

        internal CAConstructionContext(Map map,
            CATechnologicalKnowledge knowledge, int developmentPressure,
            int population, int economicCapacity, int tradeConnectivity,
            string identitySeed, bool fromStores = false,
            CACulture culture = null)
        {
            Culture = culture;
            FromStores = fromStores;
            Map = map;
            Knowledge = knowledge;
            DevelopmentPressure = developmentPressure;
            Population = population;
            EconomicCapacity = economicCapacity;
            TradeConnectivity = tradeConnectivity;
            IdentitySeed = identitySeed ?? "ca-site";
        }
    }

    internal static class CAConstructionMaterials
    {
        private sealed class Candidate
        {
            internal ThingDef Stuff;
            internal int Standing;   // how worked the material is
            internal bool Local;     // obtainable from this ground
            internal string Basis;   // why it is available here
        }

        private static ThingDef Def(string name)
        {
            return DefDatabase<ThingDef>.GetNamedSilentFail(name);
        }

        // Does this ground actually grow usable timber, or would wood have
        // to arrive from somewhere else?
        private static bool TimberGrowsHere(Map map)
        {
            try
            {
                BiomeDef biome = map?.Biome;
                if (biome == null) return false;
                foreach (ThingDef plant in biome.AllWildPlants)
                    if (plant?.plant?.IsTree == true) return true;
                return false;
            }
            catch (Exception) { return false; }
        }

        // The stone under this specific ground, in the world's own order.
        private static List<ThingDef> LocalBlocks(Map map)
        {
            var blocks = new List<ThingDef>();
            try
            {
                foreach (ThingDef rock in
                    Find.World.NaturalRockTypesIn(map.Tile))
                {
                    ThingDef made = rock == null ? null
                        : Def("Blocks" + rock.defName);
                    if (made != null && !blocks.Contains(made))
                        blocks.Add(made);
                }
            }
            catch (Exception) { }
            return blocks;
        }

        // Every material this place could legitimately build with, each
        // carrying the reason it is available to it.
        private static List<Candidate> Available(
            CAConstructionContext context)
        {
            var found = new List<Candidate>();
            Map map = context.Map;
            int construction = CATechnologicalKnowledgeModel.Rank(
                context.Knowledge, CATechnologyDomains.Construction,
                CATechnologyCompetencies.Construct);
            int metallurgy = CATechnologicalKnowledgeModel.Rank(
                context.Knowledge, CATechnologyDomains.Metallurgy,
                CATechnologyCompetencies.Construct);
            bool timber = TimberGrowsHere(map);
            bool trades = CAConstructionMaterialsKernel.Trades(
                context.TradeConnectivity);

            ThingDef wood = Def("WoodLog");
            if (wood != null && CAConstructionMaterialsKernel
                .TimberAvailable(timber, trades))
                found.Add(new Candidate
                {
                    Stuff = wood,
                    Standing = 1,
                    Local = timber,
                    Basis = timber ? "timber grows here"
                        : "timber arrives by trade"
                });

            // Worked stone needs both the rock and someone who can cut it.
            // The rock on its own is a quarry, not a wall.
            if (CAConstructionMaterialsKernel.WorkedStoneAvailable(
                    construction))
                foreach (ThingDef blocks in LocalBlocks(map))
                    found.Add(new Candidate
                    {
                        Stuff = blocks,
                        Standing = 2,
                        Local = true,
                        Basis = "local rock, masonry known"
                    });

            // Metal is worked where metallurgy is known, or bought where
            // the place is genuinely connected and can pay.
            ThingDef steel = Def("Steel");
            if (steel != null && CAConstructionMaterialsKernel
                .MetalAvailable(metallurgy, trades,
                    context.EconomicCapacity))
                found.Add(new Candidate
                {
                    Stuff = steel,
                    Standing = 3,
                    Local = metallurgy >= 2,
                    Basis = metallurgy >= 2 ? "metal worked here"
                        : "metal bought in"
                });

            return found;
        }

        // What the settlement actually holds. Only consulted when it is
        // building from its own stores; a minimum worth using, so a
        // handful of stray blocks does not decide a material.
        private static bool OnHand(Map map, ThingDef stuff)
        {
            try
            {
                return map?.resourceCounter != null
                    && map.resourceCounter.GetCount(stuff) >= 40;
            }
            catch (Exception) { return true; }
        }

        // The labour and economy a place can actually carry. A hamlet does
        // not raise a masonry town however good its rock is.
        private static int AffordableStanding(
            CAConstructionContext context)
        {
            return CAConstructionMaterialsKernel.AffordableStanding(
                context.Population, context.EconomicCapacity);
        }

        // WHERE A CULTURE STANDS ON A QUESTION THAT BEARS ON BUILDING.
        // The settlement's own distribution, on the registry's own scale
        // of -1 to +1. A culture that has never been asked reads as
        // zero, which the kernel treats as no position at all.
        private static float Stance(CAConstructionContext context,
            string questionKey)
        {
            try
            {
                if (context.Culture == null) return 0f;
                CACultureQuestionDistribution distribution = CACultureModel
                    .DistributionFor(context.Culture, questionKey, "*");
                return distribution == null ? 0f
                    : UnityEngine.Mathf.Clamp(distribution.mean, -1f, 1f);
            }
            catch (Exception) { return 0f; }
        }

        // How far past its means-driven standing this people build
        // toward, from what they expect a settlement to be and how they
        // expect to live in it.
        private static int CulturalAmbition(CAConstructionContext context)
        {
            return CAConstructionMaterialsKernel.CulturalAmbition(
                Stance(context, CACultureQuestionRegistry
                    .SettlementPermanence),
                Stance(context, CACultureQuestionRegistry
                    .ComfortExpectation));
        }

        // Whether this people take the near material because it is near.
        private static int LocalWeight(CAConstructionContext context)
        {
            return CAConstructionMaterialsKernel.LocalWeight(
                Stance(context, CACultureQuestionRegistry
                    .ResourceStewardship));
        }

        // The valid construction choices for one buildable, best first.
        internal static List<ThingDef> ValidChoicesFor(BuildableDef def,
            CAConstructionContext context, out string provenance)
        {
            provenance = "no stuff required";
            var chosen = new List<ThingDef>();
            ThingDef thing = def as ThingDef;
            if (thing == null || !thing.MadeFromStuff) return chosen;

            List<Candidate> available = Available(context)
                .Where(item => !context.FromStores
                    || OnHand(context.Map, item.Stuff))
                .Where(item => item.Stuff.stuffProps?.categories != null
                    && thing.stuffCategories != null
                    && thing.stuffCategories.Any(category =>
                        item.Stuff.stuffProps.categories
                            .Contains(category)))
                .ToList();
            if (available.Count == 0)
            {
                ThingDef fallback = GenStuff.DefaultStuffFor(thing);
                provenance = "nothing this place holds can make "
                    + thing.defName + "; the engine default stands in";
                if (fallback != null) chosen.Add(fallback);
                return chosen;
            }

            // Ambition is the development pressure and what these
            // people expect a built place to be; capacity is what the
            // place can carry. Reach is the lower of the two, so
            // culture shapes what a settlement builds toward and never
            // what it can afford.
            int ambition = CulturalAmbition(context);
            int reach = CAConstructionMaterialsKernel.Reach(
                context.DevelopmentPressure, context.Population,
                context.EconomicCapacity, ambition);
            int localWeight = LocalWeight(context);

            // As worked as this place can both reach and afford, its own
            // ground preferred over imports, then a stable identity-seeded
            // tie-break so one site keeps one material rather than
            // speckling.
            int seed = GenText.StableStringHash(context.IdentitySeed
                + ":" + thing.defName);
            List<Candidate> ordered = available
                .OrderByDescending(item =>
                    CAConstructionMaterialsKernel.Suitability(
                        item.Standing, reach, item.Local, localWeight))
                .ThenBy(item => CAWorldTendencyCausalKernel.Unit(seed,
                    GenText.StableStringHash(item.Stuff.defName), 4517))
                .ToList();

            Candidate best = ordered[0];
            // Every consumer of this string is a live receipt: the
            // settlement and frontier build logs are the only place the
            // first real generation can be read from, so it names WHOSE
            // culture answered, not merely that one did. A culture of
            // "none" there is the diagnostic that the site resolver
            // found nothing rather than that the people are neutral.
            var stance = new List<string>();
            if (ambition > 0) stance.Add("builds to last");
            else if (ambition < 0) stance.Add("builds light");
            if (localWeight == 0) stance.Add("spares its own ground");
            provenance = best.Stuff.defName + " (" + best.Basis
                + "; reach " + reach + " from development "
                + context.DevelopmentPressure + ", capacity "
                + AffordableStanding(context) + "; culture "
                + (context.Culture?.name.NullOrEmpty() != false
                    ? "none" : context.Culture.name)
                + (stance.Count == 0 ? ""
                    : " " + string.Join(" and ", stance.ToArray()))
                + ")";
            foreach (Candidate candidate in ordered)
                chosen.Add(candidate.Stuff);
            return chosen;
        }

        // HOW WHAT THIS PLACE BUILDS LOOKS. Style travelled with the
        // culture already; only the settlement's starting assets ever
        // asked for it, so one settlement's granary carried its
        // people's style while the walls around it, the furniture
        // inside it and the cabins on its frontier were styled like
        // nobody. Every spawn in the build path now asks the same
        // authority the same way it asks for material, from the same
        // context, so a settlement looks like one place.
        internal static ThingStyleDef StyleFor(ThingDef def,
            CACulture culture)
        {
            try
            {
                return CAVisualTraditionStyle.StyleFor(culture, def);
            }
            catch (Exception) { return null; }
        }

        internal static ThingStyleDef StyleFor(ThingDef def,
            CAConstructionContext context)
        {
            return StyleFor(def, context.Culture);
        }

        // Dress a thing this place has built in its people's style.
        // Silent where the culture has no style for it, which is most
        // things - a style category covers a handful of defs, not the
        // whole catalogue.
        internal static void ApplyStyle(Thing thing, CACulture culture)
        {
            if (thing == null || culture == null) return;
            ThingStyleDef style = StyleFor(thing.def, culture);
            if (style != null) thing.SetStyleDef(style);
        }

        // The single material this place builds this thing from.
        internal static ThingDef ChooseFor(BuildableDef def,
            CAConstructionContext context, out string provenance)
        {
            List<ThingDef> choices = ValidChoicesFor(def, context,
                out provenance);
            return choices.Count == 0 ? null : choices[0];
        }
    }
}
