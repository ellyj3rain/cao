using System;
using System.Collections.Generic;

namespace ColonistAwareness
{
    // Converts authored population facts into unresolved domestic needs.
    // Runtime arrangements remain owned by provisioning; domestic membership
    // changes never delete or rewrite them from this adapter.
    internal static class CADomesticProvisionAdapter
    {
        internal static void EnsureDemands(
            CARegionalSettlementPlan settlement)
        {
            if (settlement == null) return;
            var expected = new List<CADomesticProvisionDemand>();
            foreach (CASettlementPopulationGroup group in
                settlement.populationGroups
                    ?? new List<CASettlementPopulationGroup>())
            {
                if (group == null || group.share <= 0) continue;
                expected.Add(new CADomesticProvisionDemand
                {
                    demandKey = "population:" + group.key
                        + ":domestic-provision",
                    populationGroupKey = group.key,
                    representedResidents = Math.Max(1, (int)Math.Round(
                        Math.Max(0, settlement.residentPopulation)
                        * group.share / 100d)),
                    needSource = "saved population group " + group.key
                        + " and resident population",
                    state = "unresolved"
                });
            }
            settlement.domesticProvisionDemands = expected;
        }
    }
}
