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
        public bool IdeoligionProtected;
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
        public string CultureName;
        public string CultureMaturity;
        public int CultureRevision;
        public int CultureTransitionCount;
        public List<CACulturalConstituentState> CultureConstituents =
            new List<CACulturalConstituentState>();
        public List<CACulturalPracticeState> CulturePractices =
            new List<CACulturalPracticeState>();
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
        public string SettlementPrograms;
        public int Role = -1;
        public int Scale = -1;
        public int Form = -1;
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

    // A practice is recorded only when lived evidence establishes it. These
    // records are not a menu of universal cultural axes: absent evidence does
    // not create a value, and contradictory practices may coexist while
    // historical inertia decays them gradually.
    public sealed class CACulturalPracticeEvidence
    {
        public string Key;
        public string Summary;
        public int Strength;
        public string SourceOwner;
        public string SourceDomain;
        public string SourceSignature;
        public int EvidenceStartTick = -1;
        public int ObservedTick = -1;
    }

    public sealed class CACulturalHistoryEvidence
    {
        public int Tick = -1;
        public string RecordedSignature;
        public string Population;
        public string Spatial;
        public string Social;
        public string Institutional;
        public string Political;
        public string Material;
        public List<CACulturalPracticeEvidence> Practices =
            new List<CACulturalPracticeEvidence>();

        public string StableSignature()
        {
            if (!string.IsNullOrWhiteSpace(RecordedSignature))
                return RecordedSignature;
            return CACulturalExpressionCausalKernel.Signature(new[]
            {
                "population=" + (Population ?? "unrecorded"),
                "spatial=" + (Spatial ?? "unrecorded"),
                "social=" + (Social ?? "unrecorded"),
                "institutional=" + (Institutional ?? "unrecorded"),
                "political=" + (Political ?? "unrecorded"),
                "material=" + (Material ?? "unrecorded"),
                "practices=" + string.Join(",", (Practices
                        ?? new List<CACulturalPracticeEvidence>())
                    .Where(item => item != null)
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => (item.Key ?? "unrecorded") + ":"
                        + item.Strength + ":"
                        + (item.SourceSignature ?? "unrecorded")))
            });
        }
    }

    public sealed class CACulturalPracticeState
    {
        public string Key;
        public string Summary;
        public int Strength;
        public string SourceSignature;
    }

    public sealed class CACulturalConstituentState
    {
        public string CultureId;
        public string Label;
        public int Share;
        public bool Inherited;
        public bool IdeoligionProtected;
    }

    public sealed class CACulturalTransitionState
    {
        public int Sequence;
        public int Tick = -1;
        public string Cause;
        public string Summary;
        public string SuccessorSignature;
        public string PredecessorCultureSignature;
        public string EvidenceSignature;
        public string ChangedDomains;
    }

    public sealed class CACulturalPersistedStateInput
    {
        public string Identity;
        public string ParentIdentity;
        public string Locality;
        public string Maturity;
        public int Revision;
        public string CompositionSignature;
        public string VisualTradition;
        public List<CACulturalPracticeState> Practices =
            new List<CACulturalPracticeState>();
        public List<CACulturalTransitionState> Transitions =
            new List<CACulturalTransitionState>();

        public string HistoricalSignature()
        {
            return CACulturalExpressionCausalKernel.Signature(new[]
            {
                "id=" + (Identity ?? "unrecorded"),
                "parent=" + (ParentIdentity ?? "none"),
                "locality=" + (Locality ?? "none"),
                "maturity=" + (Maturity ?? "unrecorded"),
                "revision=" + Revision,
                "composition=" + (CompositionSignature ?? "unrecorded"),
                "practices=" + CACulturalExpressionCausalKernel
                    .PracticeFingerprint(Practices),
                "transitions=" + CACulturalExpressionCausalKernel
                    .TransitionFingerprint(Transitions)
            });
        }

        public string VisualExpressionSignature()
        {
            return CACulturalExpressionCausalKernel.Signature(new[]
            {
                HistoricalSignature(),
                "visual=" + (VisualTradition ?? "none")
            });
        }
    }

    public sealed class CACulturalTransitionEvaluation
    {
        public bool Changed;
        public string PriorEvidenceSignature;
        public string EvidenceSignature;
        public string TransitionSignature;
        public IReadOnlyList<string> ChangedDomains;
        public IReadOnlyList<CACulturalPracticeState> Practices;
    }

    public static class CACultureLongitudinalKernel
    {
        private const int HistoricalPeriod = 60000;

        public static CACulturalTransitionEvaluation Evaluate(
            CACulturalHistoryEvidence previous,
            CACulturalHistoryEvidence current,
            IEnumerable<CACulturalPracticeState> priorPractices,
            string priorCultureSignature)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));
            string before = previous?.StableSignature();
            string after = current.StableSignature();
            List<CACulturalPracticeState> existing = (priorPractices
                    ?? Enumerable.Empty<CACulturalPracticeState>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(
                    item.Key))
                .Select(Copy).OrderBy(item => item.Key,
                    StringComparer.Ordinal).ToList();
            if (previous == null)
                return new CACulturalTransitionEvaluation
                {
                    Changed = false,
                    PriorEvidenceSignature = before,
                    EvidenceSignature = after,
                    TransitionSignature = null,
                    ChangedDomains = Array.Empty<string>(),
                    Practices = existing
                };

            List<string> observedDomainChanges = ChangedDomains(previous,
                current);
            int elapsed = previous.Tick >= 0 && current.Tick >= previous.Tick
                ? current.Tick - previous.Tick : 0;
            int periods = elapsed / HistoricalPeriod;
            var updated = existing.ToDictionary(item => item.Key,
                item => item, StringComparer.Ordinal);
            var observed = (current.Practices
                    ?? new List<CACulturalPracticeEvidence>())
                .Where(item => item != null
                    && !string.IsNullOrWhiteSpace(item.Key))
                .GroupBy(item => item.Key, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(item =>
                    item.Strength).First())
                .OrderBy(item => item.Key, StringComparer.Ordinal).ToList();
            var observedKeys = new HashSet<string>(observed.Select(item =>
                item.Key), StringComparer.Ordinal);
            if (periods > 0)
            {
                foreach (CACulturalPracticeEvidence evidence in observed)
                {
                    CACulturalPracticeState prior;
                    int strength = Math.Max(0, Math.Min(100,
                        evidence.Strength));
                    if (updated.TryGetValue(evidence.Key, out prior))
                        strength = (int)Math.Round(
                            (prior.Strength * 3d + strength) / 4d,
                            MidpointRounding.AwayFromZero);
                    updated[evidence.Key] = new CACulturalPracticeState
                    {
                        Key = evidence.Key,
                        Summary = evidence.Summary,
                        Strength = strength,
                        SourceSignature = evidence.SourceSignature ?? after
                    };
                }
                foreach (CACulturalPracticeState prior in existing)
                {
                    if (observedKeys.Contains(prior.Key)) continue;
                    int decayed = Math.Max(0, prior.Strength - 10 * periods);
                    if (decayed == 0) updated.Remove(prior.Key);
                    else updated[prior.Key] = new CACulturalPracticeState
                    {
                        Key = prior.Key,
                        Summary = prior.Summary,
                        Strength = decayed,
                        SourceSignature = prior.SourceSignature
                    };
                }
            }
            List<CACulturalPracticeState> practices = updated.Values
                .OrderBy(item => item.Key, StringComparer.Ordinal).ToList();
            bool practicesChanged = !SamePractices(existing, practices);
            // Only state with cultural significance becomes a Culture
            // transition. Spatial, social, and material snapshots remain
            // evidence until they produce a qualified lived practice. A
            // population, institution, or political-order change is itself a
            // historically meaningful social boundary after an elapsed cycle.
            List<string> changed = observedDomainChanges.Where(domain =>
                    periods > 0 && (domain == "population"
                        || domain == "institutional"
                        || domain == "political"))
                .ToList();
            if (practicesChanged) changed.Add("lived practice");
            string practiceFingerprint = string.Join(",", practices.Select(
                item => item.Key + ":" + item.Strength + ":"
                    + (item.SourceSignature ?? "unrecorded")));
            return new CACulturalTransitionEvaluation
            {
                Changed = changed.Count > 0,
                PriorEvidenceSignature = before,
                EvidenceSignature = after,
                ChangedDomains = changed,
                Practices = practices,
                TransitionSignature = CACulturalExpressionCausalKernel
                    .Signature(new[]
                    {
                        "prior-culture=" + (priorCultureSignature
                            ?? "unrecorded"),
                        "prior-evidence=" + (before ?? "unrecorded"),
                        "evidence=" + after,
                        "period=" + previous.Tick + "->" + current.Tick,
                        "domains=" + string.Join(",", changed),
                        "practices=" + practiceFingerprint
                    })
            };
        }

        private static bool SamePractices(
            IReadOnlyList<CACulturalPracticeState> before,
            IReadOnlyList<CACulturalPracticeState> after)
        {
            if (before.Count != after.Count) return false;
            for (int i = 0; i < before.Count; i++)
            {
                if (!string.Equals(before[i].Key, after[i].Key,
                        StringComparison.Ordinal)
                    || before[i].Strength != after[i].Strength
                    || !string.Equals(before[i].Summary, after[i].Summary,
                        StringComparison.Ordinal)
                    || !string.Equals(before[i].SourceSignature,
                        after[i].SourceSignature,
                        StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static List<string> ChangedDomains(
            CACulturalHistoryEvidence previous,
            CACulturalHistoryEvidence current)
        {
            var result = new List<string>();
            AddIfChanged(result, "population", previous.Population,
                current.Population);
            AddIfChanged(result, "spatial", previous.Spatial,
                current.Spatial);
            AddIfChanged(result, "social", previous.Social,
                current.Social);
            AddIfChanged(result, "institutional", previous.Institutional,
                current.Institutional);
            AddIfChanged(result, "political", previous.Political,
                current.Political);
            AddIfChanged(result, "material", previous.Material,
                current.Material);
            return result;
        }

        private static void AddIfChanged(List<string> result, string domain,
            string before, string after)
        {
            if (!string.Equals(before, after, StringComparison.Ordinal))
                result.Add(domain);
        }

        private static CACulturalPracticeState Copy(
            CACulturalPracticeState value)
        {
            return new CACulturalPracticeState
            {
                Key = value.Key,
                Summary = value.Summary,
                Strength = value.Strength,
                SourceSignature = value.SourceSignature
            };
        }
    }

    // Pure causal operations shared by production adapters and executable
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
            int protectedIdeoligions = populations.Count(item =>
                item.IdeoligionProtected);
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
            List<CACulturalConstituentState> constituents =
                NormalizeConstituents(input.CultureConstituents);
            List<CACulturalPracticeState> practices = (input.CulturePractices
                    ?? new List<CACulturalPracticeState>())
                .Where(item => item != null
                    && !string.IsNullOrWhiteSpace(item.Key)
                    && item.Strength > 0)
                .OrderByDescending(item => item.Strength)
                .ThenBy(item => item.Key, StringComparer.Ordinal).ToList();
            bool plural = constituents.Count > 1 || (populations.Count > 1
                && (ideologySources > 1 || beliefSources > 1
                    || protectedIdeoligions > 0));
            int status = Status(input.Tension, plural, input.Founding,
                input.CultureTransitionCount, practices.Count);

            string plurality = populations.Count == 0
                ? "residents whose composition is not yet recorded"
                : populations.Count == 1 ? "one resident community"
                    : "several resident groups";
            string summary = input.Founding
                ? ((input.CultureName ?? "The founders' culture")
                    + " is inherited at landing. Its local history begins "
                    + "with the founders' conduct and adopted arrangement.")
                : ((input.CultureName ?? "Local culture")
                    + " is the recorded continuity and lived practice of "
                    + plurality + ".");

            var facts = new List<string>
            {
                "scope=" + (input.Scope ?? "unrecorded"),
                "culture=" + (input.CultureName ?? "unrecorded"),
                "culture maturity=" + (input.CultureMaturity
                    ?? "unrecorded"),
                "culture revision/transitions=" + input.CultureRevision
                    + "/" + input.CultureTransitionCount,
                "culture constituents=" + ConstituentFingerprint(
                    constituents),
                "culture practices=" + PracticeFingerprint(practices),
                "Ideoligion=" + (input.Ideoligion ?? "unrecorded"),
                "Ideoligion commitments=" + JoinValues(
                    input.IdeoligionCommitments),
                "Political Order=" + AxisFingerprint(
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
                "settlement programs=" + (input.SettlementPrograms
                    ?? "none"),
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
            int transitionCount, int practiceCount)
        {
            if (tension) return 4;
            if (plural) return 3;
            if (!founding && transitionCount > 0 && practiceCount > 0)
                return 1;
            return 0;
        }

        public static List<CACulturalConstituentState> NormalizeConstituents(
            IEnumerable<CACulturalConstituentState> values)
        {
            return (values ?? Enumerable.Empty<CACulturalConstituentState>())
                .Where(item => item != null && item.Share > 0)
                .GroupBy(item => item.CultureId ?? "unrecorded",
                    StringComparer.Ordinal)
                .Select(group => new CACulturalConstituentState
                {
                    CultureId = group.Key,
                    Label = group.Select(item => item.Label)
                        .FirstOrDefault(value =>
                            !string.IsNullOrWhiteSpace(value))
                        ?? "Culture not recorded",
                    Share = group.Sum(item => item.Share),
                    Inherited = group.Any(item => item.Inherited),
                    IdeoligionProtected = group.Any(item =>
                        item.IdeoligionProtected)
                })
                .OrderByDescending(item => item.Share)
                .ThenBy(item => item.CultureId, StringComparer.Ordinal)
                .ToList();
        }

        public static string ConstituentFingerprint(
            IEnumerable<CACulturalConstituentState> values)
        {
            return string.Join(",", NormalizeConstituents(values).Select(item =>
                item.CultureId + ":" + item.Share + ":"
                    + (item.Inherited ? "inherited" : "local") + ":"
                    + (item.IdeoligionProtected
                        ? "Ideoligion-protected" : "unprotected")));
        }

        public static string PracticeFingerprint(
            IEnumerable<CACulturalPracticeState> values)
        {
            return string.Join(",", (values
                    ?? Enumerable.Empty<CACulturalPracticeState>())
                .Where(item => item != null
                    && !string.IsNullOrWhiteSpace(item.Key))
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => item.Key + ":" + item.Strength + ":"
                    + (item.SourceSignature ?? "unrecorded")));
        }

        public static string TransitionFingerprint(
            IEnumerable<CACulturalTransitionState> values)
        {
            return string.Join(",", (values
                    ?? Enumerable.Empty<CACulturalTransitionState>())
                .Where(item => item != null)
                .OrderBy(item => item.Sequence)
                .Select(item => item.Sequence + ":" + item.Tick + ":"
                    + (item.Cause ?? "unrecorded") + ":"
                    + (item.SuccessorSignature ?? "unrecorded") + ":"
                    + (item.PredecessorCultureSignature ?? "unrecorded")
                    + ":" + (item.EvidenceSignature ?? "unrecorded") + ":"
                    + (item.ChangedDomains ?? "unrecorded")));
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
                    + (item.IdeoligionProtected
                        ? "Ideoligion-protected" : "unprotected"))
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

}
