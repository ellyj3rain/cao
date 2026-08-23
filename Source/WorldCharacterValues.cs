namespace ColonistAwareness
{
    // THE AUTHORED VALUES BEHIND EACH NAMED WORLD, AS PURE DATA. The
    // chooser presents these and the world consumes them. This table
    // is the single source: CAWorldPreset builds its entries from it,
    // and the deterministic receipts run the real partition kernel over
    // it to prove the characters separate at the substrate.
    //
    // Values only. Naming, description, ordering, and everything else a
    // player reads stay with the presentation. The urban classification
    // threshold is a fixed game constant, not authored state (DR-114,
    // DR-115); it does not appear here.
    internal readonly struct CAWorldCharacterValues
    {
        internal readonly string Key;
        internal readonly float FrequencyMin;
        internal readonly float FrequencyMax;
        internal readonly int SpanMin;
        internal readonly int SpanMax;
        internal readonly float Concentration;
        internal readonly float FrontierFrequency;
        internal readonly float FrontierSize;
        internal readonly float Variety;
        internal readonly float WorldDevelopment;
        internal readonly float DistantFounding;
        internal readonly float WorldVariability;
        internal readonly float WorldStability;

        internal CAWorldCharacterValues(string key, float frequencyMin,
            float frequencyMax, int spanMin, int spanMax,
            float concentration, float frontierFrequency,
            float frontierSize, float variety, float worldDevelopment,
            float distantFounding, float worldVariability,
            float worldStability)
        {
            Key = key;
            FrequencyMin = frequencyMin;
            FrequencyMax = frequencyMax;
            SpanMin = spanMin;
            SpanMax = spanMax;
            Concentration = concentration;
            FrontierFrequency = frontierFrequency;
            FrontierSize = frontierSize;
            Variety = variety;
            WorldDevelopment = worldDevelopment;
            DistantFounding = distantFounding;
            WorldVariability = worldVariability;
            WorldStability = worldStability;
        }

        internal float FrequencyMid
        {
            get { return (FrequencyMin + FrequencyMax) * 0.5f; }
        }

        internal static readonly CAWorldCharacterValues[] All =
        {
            new CAWorldCharacterValues("balanced",
                0.25f, 0.55f, 3, 5, 0.5f, 0.45f, 0.5f, 0.5f,
                0.5f, 0.5f, 0.35f, 0.5f),
            new CAWorldCharacterValues("heartlands",
                0.57f, 0.87f, 3, 5, 0.82f, 0.15f, 0.5f, 0.82f,
                0.85f, 0.55f, 0.15f, 0.85f),
            new CAWorldCharacterValues("city-states",
                0f, 0.2f, 2, 3, 0.85f, 0.2f, 0.3f, 0.35f,
                0.85f, 0.35f, 0.25f, 0.7f),
            new CAWorldCharacterValues("wide-marches",
                0.6f, 0.9f, 6, 9, 0.2f, 0.6f, 0.6f, 0.5f,
                0.35f, 0.45f, 0.45f, 0.35f),
            new CAWorldCharacterValues("open-frontier",
                0f, 0.27f, 2, 3, 0.2f, 0.78f, 0.82f, 0.5f,
                0.15f, 0.3f, 0.55f, 0.2f),
            new CAWorldCharacterValues("fractured-rim",
                0.25f, 0.55f, 3, 5, 0.2f, 0.78f, 0.18f, 0.82f,
                0.35f, 0.82f, 0.85f, 0.15f),
            new CAWorldCharacterValues("crossroads",
                0.3f, 0.6f, 3, 6, 0.5f, 0.3f, 0.4f, 1f,
                0.55f, 0.85f, 0.65f, 0.4f),
            new CAWorldCharacterValues("backwater",
                0.25f, 0.55f, 3, 5, 0.5f, 0.35f, 0.5f, 0.15f,
                0.3f, 0.1f, 0.15f, 0.8f),
            new CAWorldCharacterValues("imperial-marches",
                0.55f, 0.85f, 4, 7, 0.75f, 0.7f, 0.7f, 0.3f,
                0.8f, 0.6f, 0.35f, 0.6f)
        };

        internal static CAWorldCharacterValues ByKey(string key)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Key == key) return All[i];
            return All[0];
        }
    }
}