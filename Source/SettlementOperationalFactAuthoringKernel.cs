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
            return EstablishWithSource(programKey, populationGroupKey,
                sequence, "authored:established", null, fundingSource,
                stockSource, materialSource, accessSource);
        }

        // Choosing a populated settlement and its ground is an authoring act.
        // The editor may compose the minimum programs that make that exact
        // choice habitable, but it records that provenance separately from a
        // program the operator added one by one. Both paths produce the same
        // operational-fact ontology and downstream program contract.
        public static CAEstablishedProgramFactSpec EstablishForPlacement(
            string programKey, int populationGroupKey, int sequence,
            string operatorIdentity = null, string fundingSource = null,
            string stockSource = null, string materialSource = null,
            string accessSource = null)
        {
            return EstablishWithSource(programKey, populationGroupKey,
                sequence, "authored:regional-placement", operatorIdentity,
                fundingSource, stockSource, materialSource, accessSource);
        }

        private static CAEstablishedProgramFactSpec EstablishWithSource(
            string programKey, int populationGroupKey, int sequence,
            string sourceRoot, string exactOperatorIdentity,
            string fundingSource, string stockSource, string materialSource,
            string accessSource)
        {
            if (string.IsNullOrWhiteSpace(programKey))
                throw new ArgumentException("A program key is required.",
                    nameof(programKey));
            if (populationGroupKey < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(populationGroupKey));
            if (sequence < 1)
                throw new ArgumentOutOfRangeException(nameof(sequence));
            if (string.IsNullOrWhiteSpace(sourceRoot)
                || !sourceRoot.StartsWith("authored:",
                    StringComparison.Ordinal))
                throw new ArgumentException("An authored source is required.",
                    nameof(sourceRoot));

            string population = "population-group:" + populationGroupKey;
            string operatorIdentity = string.IsNullOrWhiteSpace(
                exactOperatorIdentity) ? population : exactOperatorIdentity;
            string authored = sourceRoot + ":" + programKey;
            return new CAEstablishedProgramFactSpec
            {
                FactKey = "established:" + programKey + ":"
                    + operatorIdentity
                    + ":" + sequence,
                ProgramKey = programKey,
                NeedSource = authored + ":need",
                OperatorIdentity = operatorIdentity,
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
