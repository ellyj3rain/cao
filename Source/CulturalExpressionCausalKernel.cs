using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    public sealed class CACulturalAxisCause
    {
        public string Axis;
        public string Option;
    }

    public sealed class CACulturalPopulationCause
    {
        public string Key;
        public int Share;
        public string IdeoligionSource;
        public string BeliefSource;
        public bool SeparateQuarter;
    }

    public sealed class CACulturalWeightedBeliefCause
    {
        public string Identity;
        public int Share;
        public List<CACulturalAxisCause> Positions =
            new List<CACulturalAxisCause>();
    }

    public sealed class CACulturalExpressionCausalInput
    {
        public string Scope;
        public string Background;
        public string Ideoligion;
        public List<string> IdeoligionCommitments = new List<string>();
        public List<CACulturalAxisCause> PoliticalBeliefs =
            new List<CACulturalAxisCause>();
        public List<CACulturalAxisCause> InstitutionalPractice =
            new List<CACulturalAxisCause>();
        public List<CACulturalPopulationCause> Populations =
            new List<CACulturalPopulationCause>();
        public List<CACulturalWeightedBeliefCause> WeightedBeliefs =
            new List<CACulturalWeightedBeliefCause>();
        public List<string> PopulationIdeoligionIdentities =
            new List<string>();
        public int Residents;
        public int Land;
        public int Access;
        public int Services;
        public int Civic;
        public int Economy;
        public int Trade;
        public int Specialization;
        public int History;
        public int Facilities;
        public int Role = -1;
        public int Scale = -1;
        public int Form = -1;
        public int Fortification = -1;
        public int Organization = -1;
        public bool Road;
        public bool River;
        public bool Coast;
        public int HostileRelations;
        public int NeutralRelations;
        public string RelationReceipt;
        public bool Founding;
        public bool Tension;
        public string FoundingArrangement;
        public int Buildings;
        public int InfrastructureObjects;
        public string ProvisionFingerprint;
        public string InstitutionSummary;
        public string MaterialSummary;
        public List<string> ReadingFacts = new List<string>();
    }

    public sealed class CACulturalExpressionCausalResult
    {
        public int Status;
        public string Summary;
        public string Signature;
        public IReadOnlyList<string> Facts;
    }

    public sealed class CACultureLegacyMigrationInput
    {
        public int SchemaVersion;
        public string PresetName;
        public string Name;
        public string SourceCultureDefName;
        public bool NameAuthored;
    }

    public sealed class CACultureLegacyMigrationResult
    {
        public int SchemaVersion;
        public string PresetName;
        public string Name;
        public string SourceCultureDefName;
        public bool ClearLegacyPractices;
    }

    // Pure B5 causal operations shared by production adapters and executable
    // receipts. RimWorld-facing code gathers facts; this kernel changes no
    // authored, saved, or materialized state.
    public static class CACulturalExpressionCausalKernel
    {
        public static CACulturalExpressionCausalResult Evaluate(
            CACulturalExpressionCausalInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            List<CACulturalPopulationCause> populations = (input.Populations
                    ?? new List<CACulturalPopulationCause>())
                .Where(item => item != null && item.Share > 0)
                .OrderByDescending(item => item.Share)
                .ThenBy(item => item.Key, StringComparer.Ordinal)
                .ToList();
            List<CACulturalWeightedBeliefCause> weightedBeliefs =
                (input.WeightedBeliefs
                    ?? new List<CACulturalWeightedBeliefCause>())
                .Where(item => item != null && item.Share > 0)
                .OrderByDescending(item => item.Share)
                .ThenBy(item => item.Identity, StringComparer.Ordinal)
                .ToList();
            int quartered = populations.Count(item => item.SeparateQuarter);
            int ideologySources = input.PopulationIdeoligionIdentities != null
                && input.PopulationIdeoligionIdentities.Count > 0
                ? input.PopulationIdeoligionIdentities
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal).Count()
                : populations.Select(item => item.IdeoligionSource ?? "default")
                    .Distinct(StringComparer.Ordinal).Count();
            int beliefSources = weightedBeliefs.Count > 0
                ? weightedBeliefs.Select(item => AxisFingerprint(item.Positions))
                    .Distinct(StringComparer.Ordinal).Count()
                : populations.Select(item => item.BeliefSource ?? "default")
                    .Distinct(StringComparer.Ordinal).Count();
            bool plural = populations.Count > 1
                && (ideologySources > 1 || beliefSources > 1 || quartered > 0);
            int materialAverage = AverageKnown(input.Access, input.Services,
                input.Civic, input.Economy, input.Trade);
            int status = Status(input.Tension, plural, input.Founding,
                materialAverage, input.Land, input.History,
                input.HostileRelations);

            string plurality = populations.Count == 0
                ? "residents whose composition is not yet recorded"
                : populations.Count == 1 ? "one resident community"
                    : "several resident groups";
            string summary = input.Founding
                ? ((input.Background ?? "carried background")
                    + " is carried into a new settlement; local culture is "
                    + "developing from "
                    + (input.Ideoligion ?? "the founders' Ideoligion")
                    + " and the founding arrangement.")
                : ((input.Background ?? "carried background")
                    + " is being lived by " + plurality + ". "
                    + (input.InstitutionSummary ?? "Current institutions are not recorded")
                    + "; "
                    + (input.MaterialSummary ?? "material conditions are not recorded")
                    + ".");

            var facts = new List<string>
            {
                "scope=" + (input.Scope ?? "unrecorded"),
                "background=" + (input.Background ?? "unrecorded"),
                "Ideoligion=" + (input.Ideoligion ?? "unrecorded"),
                "Ideoligion commitments=" + JoinValues(
                    input.IdeoligionCommitments),
                "political beliefs=" + AxisFingerprint(
                    input.PoliticalBeliefs),
                "institutional practice=" + AxisFingerprint(
                    input.InstitutionalPractice),
                "founding arrangement="
                    + (input.FoundingArrangement ?? "not applicable"),
                "populations=" + PopulationFingerprint(populations),
                "weighted beliefs=" + WeightedBeliefFingerprint(
                    weightedBeliefs),
                "infrastructure=" + input.Access + "/" + input.Services + "/"
                    + input.Civic,
                "economy/trade/specialization/history=" + input.Economy + "/"
                    + input.Trade + "/" + input.Specialization + "/"
                    + input.History,
                "residents/land=" + input.Residents + "/" + input.Land,
                "role/scale/form=" + input.Role + "/" + input.Scale + "/"
                    + input.Form,
                "fortification/organization=" + input.Fortification + "/"
                    + input.Organization,
                "facilities=" + input.Facilities,
                "provisions=" + (input.ProvisionFingerprint ?? "none"),
                "geography=" + input.Road + "/" + input.River + "/"
                    + input.Coast,
                "relations=" + input.HostileRelations + "/"
                    + input.NeutralRelations + "/"
                    + (input.RelationReceipt ?? "regional"),
                "buildings/infrastructure objects=" + input.Buildings + "/"
                    + input.InfrastructureObjects
            };
            facts.AddRange((input.ReadingFacts ?? new List<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item)));
            return new CACulturalExpressionCausalResult
            {
                Status = status,
                Summary = summary,
                Facts = facts,
                Signature = Signature(facts)
            };
        }

        public static string Signature(IEnumerable<string> facts)
        {
            uint hash = 2166136261u;
            foreach (char character in string.Join("|",
                (facts ?? Enumerable.Empty<string>()).ToArray()))
            {
                hash ^= character;
                hash *= 16777619u;
            }
            return hash.ToString("X8");
        }

        public static int Status(bool tension, bool plural, bool founding,
            int materialAverage, int land, int history, int hostileRelations)
        {
            if (tension) return 4;
            if (plural) return 3;
            if (!founding && (materialAverage <= 1 || land <= 1)) return 2;
            if (history >= 2 || hostileRelations > 0) return 1;
            return 0;
        }

        public static int ApplyDevelopmentProfile(int derived, int profile)
        {
            int offset = Math.Clamp(profile, -1, 1);
            return Math.Clamp(derived + offset, 0, 3);
        }

        public static int ResolveDevelopment(int authored, int derived,
            int profile)
        {
            return authored >= 0 ? Math.Clamp(authored, 0, 3)
                : ApplyDevelopmentProfile(derived, profile);
        }

        public static int ResolveFacilityMask(int generated, int authoredMask,
            int authoredValues, int allowedMask)
        {
            int authored = authoredMask & allowedMask;
            return ((generated & allowedMask) & ~authored)
                | (authoredValues & authored);
        }

        private static int AverageKnown(params int[] values)
        {
            int[] known = (values ?? Array.Empty<int>())
                .Where(value => value >= 0).ToArray();
            return known.Length == 0 ? -1
                : (int)Math.Round(known.Average(),
                    MidpointRounding.AwayFromZero);
        }

        private static string JoinValues(IEnumerable<string> values)
        {
            string[] normalized = (values ?? Enumerable.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .OrderBy(item => item, StringComparer.Ordinal).ToArray();
            return normalized.Length == 0 ? "none" : string.Join(",", normalized);
        }

        private static string AxisFingerprint(
            IEnumerable<CACulturalAxisCause> positions)
        {
            string[] normalized = (positions
                    ?? Enumerable.Empty<CACulturalAxisCause>())
                .Where(item => item != null)
                .OrderBy(item => item.Axis, StringComparer.Ordinal)
                .ThenBy(item => item.Option, StringComparer.Ordinal)
                .Select(item => (item.Axis ?? "unset") + "="
                    + (item.Option ?? "unset")).ToArray();
            return normalized.Length == 0 ? "none" : string.Join(",", normalized);
        }

        private static string PopulationFingerprint(
            IEnumerable<CACulturalPopulationCause> populations)
        {
            string[] normalized = (populations
                    ?? Enumerable.Empty<CACulturalPopulationCause>())
                .Select(item => item.Share + "%:" + (item.Key ?? "group")
                    + ":" + (item.IdeoligionSource ?? "default") + ":"
                    + (item.BeliefSource ?? "default") + ":"
                    + (item.SeparateQuarter ? "quarter" : "shared"))
                .ToArray();
            return normalized.Length == 0 ? "none" : string.Join(",", normalized);
        }

        private static string WeightedBeliefFingerprint(
            IEnumerable<CACulturalWeightedBeliefCause> beliefs)
        {
            string[] normalized = (beliefs
                    ?? Enumerable.Empty<CACulturalWeightedBeliefCause>())
                .Select(item => item.Share + "%:"
                    + (item.Identity ?? "beliefs") + "["
                    + AxisFingerprint(item.Positions) + "]").ToArray();
            return normalized.Length == 0 ? "none" : string.Join(",", normalized);
        }
    }

    public static class CACultureLegacyMigrationKernel
    {
        private const int CurrentSchemaVersion = 3;

        private static readonly Dictionary<string, string> LegacyVisualSources =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "hearth_common", "Rustican" },
                { "Hearth commons", "Rustican" },
                { "Hearth customs", "Rustican" },
                { "road_exchange", "Corunan" },
                { "Road exchange", "Corunan" },
                { "Traveling customs", "Corunan" },
                { "memorial_households", "Sophian" },
                { "Memorial households", "Sophian" },
                { "Memorial customs", "Sophian" },
                { "festival_market", "Astropolitan" },
                { "Festival market", "Astropolitan" },
                { "Festival customs", "Astropolitan" }
            };

        public static CACultureLegacyMigrationResult Migrate(
            CACultureLegacyMigrationInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            string source = input.SourceCultureDefName;
            string name = input.Name;
            if (input.SchemaVersion < CurrentSchemaVersion
                && !string.IsNullOrWhiteSpace(input.PresetName))
            {
                string visual;
                if (string.IsNullOrWhiteSpace(source)
                    && LegacyVisualSources.TryGetValue(input.PresetName,
                        out visual))
                    source = visual;
                if (string.IsNullOrWhiteSpace(name))
                    name = LegacyBackgroundName(input.PresetName);
            }
            if (input.SchemaVersion < CurrentSchemaVersion
                && !input.NameAuthored
                && !string.IsNullOrWhiteSpace(name)
                && name.EndsWith(" customs",
                    StringComparison.OrdinalIgnoreCase))
                name = !string.IsNullOrWhiteSpace(source)
                    ? source + " background"
                    : LegacyBackgroundName(name);
            return new CACultureLegacyMigrationResult
            {
                SchemaVersion = CurrentSchemaVersion,
                PresetName = null,
                Name = name,
                SourceCultureDefName = source,
                ClearLegacyPractices = true
            };
        }

        private static string LegacyBackgroundName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Carried background";
            if (value.IndexOf("road", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("travel", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Traveling background";
            if (value.IndexOf("memorial", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Memorial background";
            if (value.IndexOf("festival", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Market-town background";
            return "Settled background";
        }
    }
}
