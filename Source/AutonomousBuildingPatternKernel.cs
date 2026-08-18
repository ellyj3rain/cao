using System;

namespace ColonistAwareness
{
    // Composable evidence for autonomous construction. This is deliberately
    // not a style scale: a compact, high-throughput base can also have clear
    // circulation, regular material rhythm, good views, and expansion room.
    // Consumers compare the axes in the order owned by the building task.
    public struct CAAutonomousBuildingPatternEvidence
    {
        public bool ValidGround;
        public int UnmetRequirements;
        public int TerrainFit;
        public int FunctionalAdjacency;
        public int Throughput;
        public int Circulation;
        public int Expansion;
        public int EnvironmentalBuffer;
        public int DefensiveSeparation;
        public int VisualOrder;
        public int MaterialCost;
        public int StableOrder;

        public string Receipt()
        {
            return "valid ground " + ValidGround
                + "; unmet requirements " + UnmetRequirements
                + "; terrain fit " + TerrainFit
                + "; functional adjacency " + FunctionalAdjacency
                + "; throughput " + Throughput
                + "; circulation " + Circulation
                + "; expansion " + Expansion
                + "; environmental buffer " + EnvironmentalBuffer
                + "; defensive separation " + DefensiveSeparation
                + "; visual order " + VisualOrder
                + "; material cost " + MaterialCost
                + "; stable order " + StableOrder;
        }
    }

    public static class CAAutonomousBuildingPatternKernel
    {
        // Frontier siting owns this ordering. It closes survival and ground
        // validity first, then ranks the spatial consequences without
        // collapsing them into one decorative or optimization score.
        public static bool PreferFrontier(
            CAAutonomousBuildingPatternEvidence candidate,
            CAAutonomousBuildingPatternEvidence incumbent)
        {
            int comparison = candidate.ValidGround.CompareTo(
                incumbent.ValidGround);
            if (comparison != 0) return comparison > 0;
            comparison = incumbent.UnmetRequirements.CompareTo(
                candidate.UnmetRequirements);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.TerrainFit.CompareTo(
                incumbent.TerrainFit);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.FunctionalAdjacency.CompareTo(
                incumbent.FunctionalAdjacency);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.Throughput.CompareTo(
                incumbent.Throughput);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.Circulation.CompareTo(
                incumbent.Circulation);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.Expansion.CompareTo(
                incumbent.Expansion);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.EnvironmentalBuffer.CompareTo(
                incumbent.EnvironmentalBuffer);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.DefensiveSeparation.CompareTo(
                incumbent.DefensiveSeparation);
            if (comparison != 0) return comparison > 0;
            comparison = candidate.VisualOrder.CompareTo(
                incumbent.VisualOrder);
            if (comparison != 0) return comparison > 0;
            comparison = incumbent.MaterialCost.CompareTo(
                candidate.MaterialCost);
            if (comparison != 0) return comparison > 0;
            return candidate.StableOrder < incumbent.StableOrder;
        }

        public static int Balance(int northEast, int northWest,
            int southEast, int southWest)
        {
            int maximum = Math.Max(Math.Max(northEast, northWest),
                Math.Max(southEast, southWest));
            int minimum = Math.Min(Math.Min(northEast, northWest),
                Math.Min(southEast, southWest));
            return Math.Max(0, 100 - (maximum - minimum) * 4);
        }
    }
}
