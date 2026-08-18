using System.Collections.Generic;

namespace ColonistAwareness
{
    // Transient domain evidence. Persistent readback state is the assessment
    // stored on the settlement record; this shape never creates its sources.
    public sealed class CASettlementCapabilityEvidence
    {
        public string Domain;
        public int LevelHint;
        public List<string> Actors = new List<string>();
        public List<string> Organizations = new List<string>();
        public List<string> Operations = new List<string>();
        public List<string> Knowledge = new List<string>();
        public List<string> Material = new List<string>();
        public List<string> History = new List<string>();
        public string Blocker;
    }
}
