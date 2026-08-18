using RimWorld;

namespace ColonistAwareness
{
    // Political Order is normative composition. Neither represented institutions,
    // Culture, FactionDef flags nor world relations establish what a
    // population believes ought to be true. This adapter therefore exposes an
    // empty context until authored testimony or an observed normative record
    // is represented directly.
    internal static class CAPoliticalContext
    {
        internal static CAPoliticalDerivationContext ForFaction(
            CARegionalPlan plan, CARegionalFactionPlan faction)
        {
            return new CAPoliticalDerivationContext();
        }

        internal static CAPoliticalDerivationContext ForFaction(Faction faction,
            CACulture culture, System.Collections.Generic.List<CAAxisEntry> structure)
        {
            return new CAPoliticalDerivationContext();
        }
    }
}
