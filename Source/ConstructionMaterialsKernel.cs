using System;

namespace ColonistAwareness
{
    // The arithmetic behind a construction choice, with no engine in it,
    // so the thing that decides what a place builds from can be exercised
    // directly. The module reads the world - rock, timber, knowledge,
    // stores - and this decides what that adds up to.
    //
    // Kept pure and kept as the only copy: the module calls these rather
    // than repeating the thresholds, because a threshold written twice is
    // a threshold that drifts, and a threshold calibrated for one kind of
    // site silently makes the control inert for another.
    public static class CAConstructionMaterialsKernel
    {
        // WHETHER A PLACE CAN OBTAIN A MATERIAL AT ALL, before any
        // question of whether it prefers one. Kept here with the other
        // thresholds because these decide what a site is physically
        // able to build from, and a threshold written twice is a
        // threshold that drifts.

        // Timber is had by growing it or by having it brought in. The
        // second clause is what a supplied frontier holding lives on:
        // treeless ground with a supporter still gets wood.
        public static bool TimberAvailable(bool timberGrowsHere,
            bool trades)
        {
            return timberGrowsHere || trades;
        }

        // The rock on its own is a quarry, not a wall. Worked stone
        // needs someone who can cut it.
        public static bool WorkedStoneAvailable(int constructionRank)
        {
            return constructionRank >= 2;
        }

        // Metal is worked where metallurgy is known, or bought where the
        // place is genuinely connected AND can pay. Being supplied is
        // not the same as having an economy, which is why both are
        // required.
        public static bool MetalAvailable(int metallurgyRank, bool trades,
            int economicCapacity)
        {
            return metallurgyRank >= 2
                || (trades && economicCapacity >= 2);
        }

        // A place is connected when material can actually reach it.
        public static bool Trades(int tradeConnectivity)
        {
            return tradeConnectivity >= 2;
        }

        // How worked a material a place can carry, from its labour and its
        // economy. Calibrated for BOTH site kinds: a frontier holding
        // counts residents in single figures, a settlement counts people
        // in hundreds. Worked stone is within reach of a household that
        // can quarry and cut across seasons; metal needs an economy behind
        // it rather than hands.
        public static int AffordableStanding(int population,
            int economicCapacity)
        {
            int capacity = 1;
            if (population >= 4 || economicCapacity >= 1) capacity = 2;
            if (population >= 150 && economicCapacity >= 2) capacity = 3;
            return capacity;
        }

        // Ambition meets means. Development pressure is how built-up the
        // place is becoming; capacity is what it can actually carry. The
        // reach is the lower of the two, so a wealthy hamlet and a poor
        // city are both held to what they really have.
        public static int Reach(int developmentPressure, int population,
            int economicCapacity)
        {
            return Reach(developmentPressure, population, economicCapacity,
                0);
        }

        // The same, with what these people expect a built place to be.
        // Cultural ambition moves what they REACH FOR; it never moves
        // what they can carry, so a mobile people who could afford stone
        // still do not raise it, and a people who expect permanent roots
        // and cannot afford stone still build in wood.
        public static int Reach(int developmentPressure, int population,
            int economicCapacity, int culturalAmbition)
        {
            int ambition = 1 + Math.Max(0, developmentPressure)
                + culturalAmbition;
            if (ambition < 1) ambition = 1;
            return Math.Min(ambition,
                AffordableStanding(population, economicCapacity));
        }

        // WHAT A PEOPLE EXPECT A BUILT PLACE TO BE, as one step up or
        // down in what they build toward.
        //
        // Permanence is the expectation of enduring settlement against
        // mobile life; a people who expect to move build from what is
        // quick to raise and cheap to leave, and a people who expect
        // roots build from what outlasts them. Comfort is the expected
        // standard of daily material life; austerity builds the plainest
        // thing that serves, and a comfort-centered people build past
        // sufficiency. Permanence carries the greater share because it
        // is about the building itself; comfort is about living in it.
        //
        // Both arrive on the culture's own scale, mobile/austere at -1
        // to permanent/comfort-centered at +1. Only a clear position
        // moves anything: a people who are merely context-dependent
        // build to their means, not to a doctrine.
        public static int CulturalAmbition(float permanence, float comfort)
        {
            float pull = permanence * 0.6f + comfort * 0.4f;
            if (pull >= 0.5f) return 1;
            if (pull <= -0.5f) return -1;
            return 0;
        }

        // HOW HARD A PEOPLE LEAN ON THEIR OWN GROUND. Stewardship is the
        // expected restraint in cutting and quarrying what is around
        // them. Building from local rock and local timber IS that
        // extraction, so a people who expect the land to be spared do
        // not take the near thing simply because it is near.
        //
        // This dampens the local preference to neutral; it never inverts
        // it into a preference for imports, because protecting one's own
        // hill by stripping someone else's is not what the question
        // asks and would send self-sufficient places shopping.
        public static int LocalWeight(float stewardship)
        {
            return stewardship >= 0.34f ? 0 : 1;
        }

        // How strongly a candidate material suits this place. Higher wins.
        // A material beyond the reach is not forbidden outright - it is
        // simply worth no more than the reach allows - so a place with one
        // impossible option still builds rather than failing.
        public static int Suitability(int candidateStanding, int reach,
            bool local)
        {
            return Suitability(candidateStanding, reach, local, 1);
        }

        // The same, with how much this place's own ground counts for it.
        // The local term is the tie-break BELOW reach and affordability,
        // never above them, so stewardship changes which of two equally
        // reachable materials a place settles on and never talks it into
        // building past its means.
        public static int Suitability(int candidateStanding, int reach,
            bool local, int localWeight)
        {
            int withinReach = Math.Min(candidateStanding, reach);
            int affordable = candidateStanding <= reach ? 1 : 0;
            return withinReach * 4 + affordable * 2
                + (local ? localWeight : 0);
        }
    }
}
