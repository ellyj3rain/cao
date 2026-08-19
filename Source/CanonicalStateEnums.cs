namespace ColonistAwareness
{
    // Shared persisted discriminants live in a dependency-free source file so
    // production and the campaign-preflight harness compile the same values.
    public enum CASiteFactionReferenceKind : byte
    {
        None = 0,
        RegionalFaction = 1,
        WorldFaction = 2
    }

    public enum CAKnowledgeFactKind : byte
    {
        SocialEvent = 0,
        SiteAffiliation = 1,
        SitePopulation = 2,
        Culture = 3,
        Ideoligion = 4,
        PoliticalOrder = 5,
        Institution = 6,
        Organization = 7,
        Officeholder = 8,
        TechnologicalKnowledge = 9,
        Geography = 10,
        Route = 11,
        Conflict = 12,
        Research = 13
    }

    public enum CAKnowledgePersistenceClass : byte
    {
        Transient = 0,
        Working = 1,
        Durable = 2,
        Institutional = 3
    }
}
