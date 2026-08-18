using System;

namespace ColonistAwareness
{
    // Pure formation contract shared by runtime authoring and the governed
    // fixture generator. Calling this kernel represents an explicit authoring
    // act at the initial-history boundary; it never infers an institution from
    // a tendency, asset, population count, hash, or current tick.
    public sealed class CAEstablishedProgramFactSpec
    {
        public string FactKey;
        public string ProgramKey;
        public string NeedSource;
        public string OperatorIdentity;
        public string OperatorSource;
        public string LaborSource;
        public string StandingSource;
        public string ActivitySource;
        public string TargetPopulation;
        public string KnowledgeSource;
        public string FundingSource;
        public string StockSource;
        public string PolicyKey;
        public string MaterialSource;
        public string AccessSource;
        public string MaintenanceSource;
        public string Provenance;
    }

    public static class CASettlementOperationalFactAuthoringKernel
    {
        public static CAEstablishedProgramFactSpec Establish(
            string programKey, int populationGroupKey, int sequence,
            string fundingSource = null, string stockSource = null,
            string materialSource = null, string accessSource = null)
        {
            if (string.IsNullOrWhiteSpace(programKey))
                throw new ArgumentException("A program key is required.",
                    nameof(programKey));
            if (populationGroupKey < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(populationGroupKey));
            if (sequence < 1)
                throw new ArgumentOutOfRangeException(nameof(sequence));

            string population = "population-group:" + populationGroupKey;
            string authored = "authored:established:" + programKey;
            return new CAEstablishedProgramFactSpec
            {
                FactKey = "established:" + programKey + ":" + population
                    + ":" + sequence,
                ProgramKey = programKey,
                NeedSource = authored + ":need",
                OperatorIdentity = population,
                OperatorSource = authored + ":operator",
                LaborSource = "authored:" + population,
                StandingSource = authored + ":standing",
                ActivitySource = authored + ":practice",
                TargetPopulation = population,
                KnowledgeSource = authored + ":knowledge",
                FundingSource = fundingSource ?? authored + ":funding",
                StockSource = stockSource ?? authored + ":stock",
                PolicyKey = authored + ":rule",
                MaterialSource = materialSource ?? authored + ":materials",
                AccessSource = accessSource ?? authored + ":access",
                MaintenanceSource = authored + ":maintenance",
                Provenance = authored + ":initial-history"
            };
        }
    }
}
