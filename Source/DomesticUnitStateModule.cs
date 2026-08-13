using System.Collections.Generic;

namespace ColonistAwareness
{
    // Owns normalization of the persistent domestic state carried by a
    // settlement record. Formation and provision adapters mutate through this
    // boundary; they do not create parallel stores.
    internal static class CADomesticUnitState
    {
        internal static void Normalize(CARegionalSettlementRecord record)
        {
            if (record == null) return;
            if (record.domesticUnits == null)
                record.domesticUnits = new List<CADomesticUnit>();
            if (record.domesticMembershipTransitions == null)
                record.domesticMembershipTransitions =
                    new List<CADomesticMembershipTransition>();
            if (record.domesticProvisionDemands == null)
                record.domesticProvisionDemands =
                    new List<CADomesticProvisionDemand>();
        }
    }
}
