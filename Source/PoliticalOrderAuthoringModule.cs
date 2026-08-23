using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    public sealed class CAPoliticalOptionShare : IExposable
    {
        public string optionKey;
        public int share;
        public byte source;

        public void ExposeData()
        {
            Scribe_Values.Look(ref optionKey, "optionKey");
            Scribe_Values.Look(ref share, "share", 0);
            Scribe_Values.Look(ref source, "source", (byte)0);
        }

        internal CAPoliticalOptionShare Copy()
        {
            return (CAPoliticalOptionShare)MemberwiseClone();
        }
    }

    public sealed class CAPoliticalQuestionState : IExposable
    {
        public string questionKey;
        public List<CAPoliticalOptionShare> options =
            new List<CAPoliticalOptionShare>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref questionKey, "questionKey");
            Scribe_Collections.Look(ref options, "options", LookMode.Deep);
        }

        internal CAPoliticalQuestionState Copy()
        {
            return new CAPoliticalQuestionState
            {
                questionKey = questionKey,
                options = (options ?? new List<CAPoliticalOptionShare>())
                    .Where(item => item != null).Select(item => item.Copy())
                    .ToList()
            };
        }
    }

    internal sealed class CAPoliticalOptionDef
    {
        internal readonly string Key;
        internal readonly string Label;
        internal readonly string Clause;

        internal CAPoliticalOptionDef(string key, string label,
            string clause)
        {
            Key = key;
            Label = label;
            Clause = clause;
        }
    }

    internal sealed class CAPoliticalQuestionDef
    {
        internal readonly string Key;
        internal readonly string Group;
        internal readonly string Label;
        internal readonly string Prompt;
        internal readonly bool Blendable;
        internal readonly CAPoliticalOptionDef[] Options;

        internal CAPoliticalQuestionDef(string key, string group,
            string label, string prompt, bool blendable,
            params CAPoliticalOptionDef[] options)
        {
            Key = key;
            Group = group;
            Label = label;
            Prompt = prompt;
            Blendable = blendable;
            Options = options;
        }
    }

    internal sealed class CAPoliticalOrderPreset
    {
        internal readonly string Key;
        internal readonly string Label;
        internal readonly string Summary;

        internal CAPoliticalOrderPreset(string key, string label,
            string summary)
        {
            Key = key;
            Label = label;
            Summary = summary;
        }
    }

    internal static class CAPoliticalQuestionRegistry
    {
        internal const string Authority = "Authority and decisions";
        internal const string Civic = "Civic life and status";
        internal const string Property = "Property and enterprise";
        internal const string Economy = "Exchange, work, and provision";
        internal const string Security = "Security and conflict";

        private static CAPoliticalOptionDef O(string key, string label,
            string clause)
        {
            return new CAPoliticalOptionDef(key, label, clause);
        }

        internal static readonly CAPoliticalQuestionDef[] Questions =
        {
            new CAPoliticalQuestionDef("authority.leadership", Authority,
                "Political authority",
                "Where should final political authority rest?", true,
                O("executive", "executive leadership",
                    "An executive should be able to act for the whole."),
                O("council", "standing councils",
                    "Standing councils should govern common affairs."),
                O("assembly", "public assemblies",
                    "Public assemblies should retain direct authority."),
                O("federal", "delegated local authority",
                    "Local bodies should delegate only bounded powers upward."),
                O("customary", "customary authority",
                    "Established custom and standing should carry authority.")),
            new CAPoliticalQuestionDef("authority.decisions", Authority,
                "Binding decisions",
                "How should binding public decisions be made?", true,
                O("directive", "executive direction",
                    "The executive may issue binding directions within its mandate."),
                O("majority", "majority decision",
                    "Eligible participants should decide by majority."),
                O("consensus", "consensus",
                    "Binding decisions should require broad consent."),
                O("custom", "custom and precedent",
                    "Custom and precedent should settle recurring questions."),
                O("review", "independent review",
                    "Binding acts should remain subject to independent review.")),
            new CAPoliticalQuestionDef("civic.participation", Civic,
                "Political voice",
                "Who should have political voice?", true,
                O("residents", "all adult residents",
                    "All adult residents should have political voice."),
                O("citizens", "recognized members",
                    "Political voice should belong to recognized members."),
                O("workers", "workers and producers",
                    "Workers and producers should govern matters they sustain."),
                O("households", "households",
                    "Households should hold a represented voice."),
                O("standing", "earned standing",
                    "Political voice should increase with earned public standing.")),
            new CAPoliticalQuestionDef("civic.liberty", Civic,
                "Dissent and liberty",
                "How should political dissent be treated?", false,
                O("protected", "protected liberty",
                    "Political dissent and open association should be protected."),
                O("majoritarian", "majority rule",
                    "The majority may govern while minorities retain ordinary standing."),
                O("restricted", "restricted dissent",
                    "Political dissent may be limited to protect the established order."),
                O("orthodox", "enforced doctrine",
                    "Public political doctrine should be enforced."),
                O("customary", "customary toleration",
                    "Tolerance should follow custom, office, and social standing.")),
            new CAPoliticalQuestionDef("civic.status", Civic,
                "Public status",
                "What should determine public rank and privilege?", true,
                O("equal", "equal legal standing",
                    "No inherited class should carry fixed legal privilege."),
                O("merit", "earned distinction",
                    "Public distinction should follow service and achievement."),
                O("hereditary", "hereditary rank",
                    "Rank and privilege may pass through families."),
                O("caste", "fixed estates",
                    "Distinct social estates should carry different rights and duties.")),
            new CAPoliticalQuestionDef("civic.membership", Civic,
                "Membership",
                "How should membership in the political community be gained?", true,
                O("open", "open residence",
                    "Settled residents should be able to join the community."),
                O("vetted", "admission by service or sponsorship",
                    "Membership should require represented service, sponsorship, or oath."),
                O("descent", "membership by descent",
                    "Membership should ordinarily pass through families."),
                O("closed", "restricted admission",
                    "Admission should remain exceptional and closely controlled.")),

            PropertyQuestion("property.land", "Land and natural resources",
                "Who should own land and natural resources?"),
            PropertyQuestion("property.housing", "Homes and housing",
                "Who should own homes and housing?"),
            PropertyQuestion("property.food", "Farms and food production",
                "Who should own farms and essential food production?"),
            PropertyQuestion("property.care", "Medicine and care",
                "Who should own medical and care capacity?"),
            PropertyQuestion("property.industry", "Workshops and industry",
                "Who should own workshops and industry?"),
            PropertyQuestion("property.trade", "Stores and trade stock",
                "Who should own stores and goods held for trade?"),
            PropertyQuestion("property.utilities", "Utilities",
                "Who should own power, water, and other utilities?"),
            PropertyQuestion("property.infrastructure", "Roads and public works",
                "Who should own roads and shared public works?"),
            PropertyQuestion("property.finance", "Finance and credit",
                "Who should own lending and credit institutions?"),
            PropertyQuestion("property.luxury", "Surplus and luxury enterprise",
                "Who should own surplus and luxury enterprise?"),
            PropertyQuestion("property.security", "Security and defense assets",
                "Who should own organized security and defense assets?"),
            PropertyQuestion("property.knowledge", "Knowledge and communications",
                "Who should own common knowledge and communications systems?"),

            new CAPoliticalQuestionDef("economy.exchange", Economy,
                "Exchange and allocation",
                "How should goods and services be allocated?", true,
                O("market", "open exchange",
                    "Voluntary exchange and prices should coordinate ordinary trade."),
                O("regulated", "regulated markets",
                    "Markets should operate under public rules and material guarantees."),
                O("planned", "public allocation",
                    "Public bodies should allocate represented goods and services."),
                O("communal", "common distribution",
                    "Common stores and reciprocal provision should distribute goods.")),
            new CAPoliticalQuestionDef("economy.credit", Economy,
                "Credit and lending",
                "How should credit and lending be provided?", true,
                O("private", "private credit",
                    "Private lenders may extend credit under common rules."),
                O("public", "public credit",
                    "Public institutions should provide accessible credit."),
                O("cooperative", "cooperative credit",
                    "Member-owned institutions should provide credit."),
                O("restricted", "restricted lending",
                    "Debt creation and interest should remain tightly limited.")),
            new CAPoliticalQuestionDef("economy.rent", Economy,
                "Rent and extraction",
                "What limits should apply to rent and passive extraction?", false,
                O("open", "open rents",
                    "Owners may set rents and returns by agreement."),
                O("regulated", "regulated rents",
                    "Rent and passive returns should be regulated against abuse."),
                O("cost", "cost-limited rents",
                    "Rent should cover represented costs rather than unrestricted extraction."),
                O("noExtraction", "no rent extraction",
                    "Ownership should not permit extraction from another person's basic need.")),
            new CAPoliticalQuestionDef("economy.work", Economy,
                "Work and enterprise",
                "How should work and enterprise be organized?", true,
                O("contract", "voluntary employment",
                    "People may contract for paid work and form private enterprises."),
                O("cooperative", "worker-led enterprise",
                    "Workers should be able to own and govern their workplaces."),
                O("public", "public service",
                    "Public institutions should organize work for common needs."),
                O("household", "household production",
                    "Households may organize their own productive work."),
                O("duty", "required public duty",
                    "Defined public duties may require represented service."),
                O("coerced", "coerced labor",
                    "The established order may compel labor without a right of refusal.")),
            new CAPoliticalQuestionDef("economy.provision", Economy,
                "Basic provision",
                "Who should guarantee food, shelter, medicine, and care?", true,
                O("household", "household responsibility",
                    "Households should ordinarily provide for their own needs."),
                O("public", "public guarantee",
                    "Public institutions should guarantee essential provision."),
                O("common", "common provision",
                    "Common stores and mutual provision should meet basic needs."),
                O("voluntary", "voluntary support",
                    "Voluntary and religious organizations should provide support.")),

            new CAPoliticalQuestionDef("security.local", Security,
                "Local security",
                "How should settlements maintain internal security?", true,
                O("adhoc", "ad hoc response",
                    "Residents should assemble for incidents only when needed."),
                O("watch", "community watch",
                    "Residents should share a represented watch duty."),
                O("professional", "professional civil security",
                    "A professional public body should maintain local security."),
                O("executive", "executive guard",
                    "A security body should answer directly to executive authority.")),
            new CAPoliticalQuestionDef("security.defense", Security,
                "Defense service",
                "How should defense be organized?", true,
                O("adhoc", "emergency defenders",
                    "Defenders should assemble only when danger arises."),
                O("levy", "general service",
                    "Members may owe defined defense service."),
                O("militia", "trained militia",
                    "A trained part-time militia should defend the community."),
                O("professional", "professional force",
                    "A professional force should provide continuing defense."),
                O("hereditary", "hereditary warrior estate",
                    "A hereditary estate should hold primary defense duties.")),
            new CAPoliticalQuestionDef("security.conflict", Security,
                "Conduct in conflict",
                "What protections should govern armed conflict?", true,
                O("quarter", "quarter and surrender",
                    "Surrendered and defeated enemies should be spared."),
                O("combatants", "combatant distinction",
                    "Force should be directed against represented combatants."),
                O("unrestricted", "victor's discretion",
                    "Victory may override standing protections for enemies."))
        };

        private static CAPoliticalQuestionDef PropertyQuestion(string key,
            string label, string prompt)
        {
            return new CAPoliticalQuestionDef(key, Property, label, prompt,
                true,
                O("private", "private title",
                    "Individuals and private enterprises may hold title."),
                O("cooperative", "worker or member ownership",
                    "Workers and members may own it cooperatively."),
                O("common", "common ownership",
                    "It may be held in common for represented users."),
                O("public", "public ownership",
                    "A public body may own it for the political community."));
        }

        internal static CAPoliticalQuestionDef Find(string key)
        {
            return Questions.FirstOrDefault(item => item.Key == key);
        }

        internal static IEnumerable<CAPoliticalQuestionDef> InGroup(
            string group)
        {
            return Questions.Where(item => item.Group == group);
        }
    }

    internal static class CAPoliticalOrderModel
    {
        private static readonly string[] PropertyKeys =
            CAPoliticalQuestionRegistry.Questions.Where(item =>
                    item.Group == CAPoliticalQuestionRegistry.Property)
                .Select(item => item.Key).ToArray();

        internal static readonly CAPoliticalOrderPreset[] Presets =
        {
            new CAPoliticalOrderPreset("progressive-civic-mix",
                "Progressive civic mixed economy",
                "Strong executive capacity, protected civic liberty, broad public guarantees, and mixed private, cooperative, and public enterprise."),
            new CAPoliticalOrderPreset("civic-market",
                "Civic free enterprise",
                "Representative government, protected dissent, private enterprise, regulated exchange, and limited public guarantees."),
            new CAPoliticalOrderPreset("market-socialist",
                "Democratic market socialism",
                "Broad political voice, worker-led production, public essentials, cooperative credit, and regulated markets."),
            new CAPoliticalOrderPreset("cooperative-commonwealth",
                "Cooperative commonwealth",
                "Council government, cooperative ownership, common provision, and locally organized defense."),
            new CAPoliticalOrderPreset("developmental-executive",
                "Executive developmental state",
                "Concentrated executive authority, public infrastructure and finance, regulated enterprise, and professional administration."),
            new CAPoliticalOrderPreset("customary-landed",
                "Customary landed order",
                "Hereditary and customary authority, private landed property, household production, and status-based participation."),
            new CAPoliticalOrderPreset("communal-assembly",
                "Communal assembly",
                "Direct public authority, common ownership, mutual provision, open membership, and community defense."),
            new CAPoliticalOrderPreset("central-party",
                "Central party state",
                "Central direction, enforced doctrine, public ownership, planned allocation, and professional security."),
        };

        internal static CAPoliticalOrderPreset FindPreset(string key)
        {
            return Presets.FirstOrDefault(item => item.Key == key);
        }

        internal static bool MatchesPreset(CAPoliticalBeliefs beliefs,
            string key)
        {
            if (!HasVariables(beliefs) || FindPreset(key) == null)
                return false;
            var expected = new CAPoliticalBeliefs();
            ApplyPreset(expected, key, CAAxisSource.Generated);
            return Matches(beliefs, expected);
        }

        internal static bool Matches(CAPoliticalBeliefs left,
            CAPoliticalBeliefs right)
        {
            if (!HasVariables(left) || !HasVariables(right)) return false;
            return CAPoliticalQuestionRegistry.Questions.All(definition =>
            {
                CAPoliticalQuestionState leftState = State(left,
                    definition.Key);
                CAPoliticalQuestionState rightState = State(right,
                    definition.Key);
                if (leftState?.options == null || rightState?.options == null
                    || leftState.options.Count != rightState.options.Count)
                    return false;
                return leftState.options.OrderBy(item => item.optionKey)
                    .Zip(rightState.options.OrderBy(item => item.optionKey),
                        (actual, baseline) => actual.optionKey
                                == baseline.optionKey
                            && actual.share == baseline.share)
                    .All(equal => equal);
            });
        }

        internal static bool HasVariables(CAPoliticalBeliefs beliefs)
        {
            return beliefs?.questions != null && beliefs.questions.Any(item =>
                item != null && item.options != null
                    && item.options.Any(option => option != null
                        && option.share > 0));
        }

        internal static void Normalize(CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null) return;
            if (beliefs.questions == null)
                beliefs.questions = new List<CAPoliticalQuestionState>();
            beliefs.questions.RemoveAll(item => item == null
                || CAPoliticalQuestionRegistry.Find(item.questionKey) == null);
            beliefs.questions = CAPoliticalQuestionRegistry.Questions
                .Select(definition =>
                {
                    List<CAPoliticalQuestionState> represented = beliefs
                        .questions.Where(item => item.questionKey
                            == definition.Key).ToList();
                    return represented.Count == 0 ? null
                        : Merge(definition.Key, represented);
                }).Where(item => item != null).ToList();
            foreach (CAPoliticalQuestionState state in beliefs.questions)
                NormalizeState(state);
            if (HasVariables(beliefs))
            {
                ProjectLegacyBeliefs(beliefs);
                if (!beliefs.nameAuthored || beliefs.name.NullOrEmpty())
                    beliefs.name = GeneratedName(beliefs,
                        beliefs.nameRoll);
            }
        }

        internal static void Ensure(CAPoliticalBeliefs beliefs, string seed)
        {
            if (beliefs == null) return;
            Normalize(beliefs);
            if (!HasVariables(beliefs))
            {
                SeedLegacyEvidence(beliefs);
                string preset = ClosestPreset(beliefs, seed);
                CompleteFromPreset(beliefs, preset,
                    CAAxisSource.Generated);
            }
            else
            {
                string preset = ClosestPreset(beliefs, seed);
                CompleteFromPreset(beliefs, preset,
                    CAAxisSource.Generated);
            }
            Normalize(beliefs);
        }

        internal static void ApplyPreset(CAPoliticalBeliefs beliefs,
            string key, CAAxisSource source)
        {
            if (beliefs == null) return;
            beliefs.questions = new List<CAPoliticalQuestionState>();
            BuildPreset(beliefs, key, source, false);
            beliefs.nameAuthored = false;
            Normalize(beliefs);
        }

        internal static void Randomize(CAPoliticalBeliefs beliefs,
            string seed)
        {
            if (beliefs == null) return;
            beliefs.generationRoll++;
            int index = PositiveHash((seed ?? beliefs.id ?? "politics")
                + ":" + beliefs.generationRoll) % Presets.Length;
            ApplyPreset(beliefs, Presets[index].Key,
                CAAxisSource.Generated);
            // A bounded second influence creates composite results without
            // turning independent questions into random noise.
            int second = (index + 1 + PositiveHash((seed ?? "politics")
                + ":secondary:" + beliefs.generationRoll)
                % (Presets.Length - 1)) % Presets.Length;
            OverlaySelectedDomains(beliefs, Presets[second].Key,
                CAAxisSource.Generated, seed);
            Normalize(beliefs);
        }

        internal static void RerollName(CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null) return;
            beliefs.nameRoll++;
            beliefs.nameAuthored = false;
            beliefs.name = GeneratedName(beliefs, beliefs.nameRoll);
        }

        internal static void Rename(CAPoliticalBeliefs beliefs,
            string value)
        {
            if (beliefs == null || value.NullOrEmpty()) return;
            beliefs.name = value.Trim();
            beliefs.nameAuthored = true;
        }

        internal static CAPoliticalQuestionState State(
            CAPoliticalBeliefs beliefs, string questionKey)
        {
            return beliefs?.questions?.FirstOrDefault(item => item != null
                && item.questionKey == questionKey);
        }

        internal static int Weight(CAPoliticalBeliefs beliefs,
            string questionKey, string optionKey)
        {
            return State(beliefs, questionKey)?.options?
                .FirstOrDefault(item => item != null
                    && item.optionKey == optionKey)?.share ?? 0;
        }

        internal static void SetQuestion(CAPoliticalBeliefs beliefs,
            string questionKey,
            IEnumerable<KeyValuePair<string, int>> values,
            CAAxisSource source)
        {
            if (beliefs == null) return;
            CAPoliticalQuestionDef definition =
                CAPoliticalQuestionRegistry.Find(questionKey);
            if (definition == null) return;
            beliefs.questions.RemoveAll(item => item != null
                && item.questionKey == questionKey);
            var state = new CAPoliticalQuestionState
            {
                questionKey = questionKey,
                options = (values
                        ?? Enumerable.Empty<KeyValuePair<string, int>>())
                    .Where(pair => pair.Value > 0 && definition.Options.Any(
                        option => option.Key == pair.Key))
                    .Select(pair => new CAPoliticalOptionShare
                    {
                        optionKey = pair.Key,
                        share = pair.Value,
                        source = (byte)source
                    }).ToList()
            };
            beliefs.questions.Add(state);
            NormalizeState(state);
            Normalize(beliefs);
        }

        internal static string Identity(CAPoliticalBeliefs beliefs)
        {
            if (!HasVariables(beliefs)) return "Political order not set";
            return beliefs.name.NullOrEmpty()
                ? GeneratedName(beliefs, beliefs.nameRoll) : beliefs.name;
        }

        internal static string Description(CAPoliticalBeliefs beliefs)
        {
            if (!HasVariables(beliefs))
                return "No complete political order has been generated.";
            var paragraphs = new List<string>();
            paragraphs.Add(string.Join(" ", new[]
            {
                QuestionSentence(beliefs, "authority.leadership"),
                QuestionSentence(beliefs, "authority.decisions"),
                QuestionSentence(beliefs, "civic.participation"),
                QuestionSentence(beliefs, "civic.liberty"),
                QuestionSentence(beliefs, "civic.status"),
                QuestionSentence(beliefs, "civic.membership")
            }.Where(value => !value.NullOrEmpty())));
            paragraphs.Add(string.Join(" ", PropertyKeys.Select(key =>
                    QuestionSentence(beliefs, key))
                .Where(value => !value.NullOrEmpty())));
            paragraphs.Add(string.Join(" ", new[]
            {
                QuestionSentence(beliefs, "economy.exchange"),
                QuestionSentence(beliefs, "economy.credit"),
                QuestionSentence(beliefs, "economy.rent"),
                QuestionSentence(beliefs, "economy.work"),
                QuestionSentence(beliefs, "economy.provision"),
                QuestionSentence(beliefs, "security.local"),
                QuestionSentence(beliefs, "security.defense"),
                QuestionSentence(beliefs, "security.conflict")
            }.Where(value => !value.NullOrEmpty())));
            return string.Join("\n\n", paragraphs.Where(value =>
                !value.NullOrEmpty()));
        }

        internal static string ShortSummary(CAPoliticalBeliefs beliefs)
        {
            if (!HasVariables(beliefs))
                return "Political order not set";
            var parts = new List<string>
            {
                DominantLabel(beliefs, "authority.leadership"),
                DominantLabel(beliefs, "civic.liberty"),
                OwnershipSummary(beliefs),
                DominantLabel(beliefs, "economy.exchange"),
                DominantLabel(beliefs, "economy.provision")
            };
            int antiExtraction = Weight(beliefs, "economy.rent",
                    "noExtraction")
                + Weight(beliefs, "economy.rent", "cost");
            parts.Add(antiExtraction >= 50
                ? "rent extraction barred" : DominantLabel(beliefs,
                    "economy.rent"));
            return string.Join(" · ", parts.Where(value =>
                    !value.NullOrEmpty()).Distinct().ToArray());
        }

        internal static string QuestionSentence(CAPoliticalBeliefs beliefs,
            string questionKey)
        {
            CAPoliticalQuestionDef definition =
                CAPoliticalQuestionRegistry.Find(questionKey);
            CAPoliticalQuestionState state = State(beliefs, questionKey);
            if (definition == null || state?.options == null
                || state.options.Count == 0) return null;
            List<CAPoliticalOptionShare> selected = state.options
                .Where(item => item != null && item.share > 0)
                .OrderByDescending(item => item.share)
                .ThenBy(item => item.optionKey).ToList();
            if (selected.Count == 1)
            {
                CAPoliticalOptionDef option = definition.Options
                    .FirstOrDefault(item => item.Key
                        == selected[0].optionKey);
                return option?.Clause;
            }
            string composition = CompositionWords(definition, selected);
            if (definition.Group == CAPoliticalQuestionRegistry.Property)
                return definition.Label + " are held through "
                    + composition + ".";
            return questionKey switch
            {
                "authority.leadership" =>
                    "Political authority rests in " + composition + ".",
                "authority.decisions" =>
                    "Binding decisions are made through " + composition + ".",
                "civic.participation" =>
                    "Political voice is distributed through " + composition + ".",
                "civic.status" =>
                    "Public standing follows " + composition + ".",
                "civic.membership" =>
                    "Political membership follows " + composition + ".",
                "economy.exchange" =>
                    "Exchange and allocation rely on " + composition + ".",
                "economy.credit" =>
                    "Credit is provided through " + composition + ".",
                "economy.work" =>
                    "Work and enterprise are organized through "
                        + composition + ".",
                "economy.provision" =>
                    "Basic provision is secured through " + composition + ".",
                "security.local" =>
                    "Local security is maintained through " + composition + ".",
                "security.defense" =>
                    "Defense is organized through " + composition + ".",
                "security.conflict" =>
                    "Conduct in conflict follows " + composition + ".",
                _ => definition.Label + " relies on " + composition + "."
            };
        }

        private static string CompositionWords(
            CAPoliticalQuestionDef definition,
            IReadOnlyList<CAPoliticalOptionShare> selected)
        {
            var values = selected.Select(item => new
            {
                item.share,
                Label = definition.Options.First(option => option.Key
                    == item.optionKey).Label
            }).ToList();
            if (values.Count == 0) return "no represented position";
            if (values.Count == 1) return values[0].Label;

            var material = values.Where(item => item.share >= 16).ToList();
            var limited = values.Where(item => item.share < 16).ToList();
            if (material.Count == 0)
            {
                material.Add(values[0]);
                limited = values.Skip(1).ToList();
            }

            string phrase;
            if (material[0].share >= 60)
                phrase = "mostly " + material[0].Label
                    + (material.Count > 1 ? ", with " + NaturalList(
                        material.Skip(1).Select(item => item.Label).ToArray())
                        + " in supporting roles" : "");
            else if (material.Count > 1
                && material[0].share - material[1].share >= 15)
                phrase = material[0].Label + " as the leading form, alongside "
                    + NaturalList(material.Skip(1).Select(item => item.Label)
                        .ToArray());
            else
                phrase = "a mixed balance of " + NaturalList(material
                    .Select(item => item.Label).ToArray());

            if (limited.Count > 0)
                phrase += ", with a limited role for " + NaturalList(limited
                    .Select(item => item.Label).ToArray());
            return phrase;
        }

        private static string DominantLabel(CAPoliticalBeliefs beliefs,
            string questionKey)
        {
            CAPoliticalQuestionDef definition =
                CAPoliticalQuestionRegistry.Find(questionKey);
            CAPoliticalOptionShare option = State(beliefs, questionKey)
                ?.options?.Where(item => item != null && item.share > 0)
                .OrderByDescending(item => item.share)
                .ThenBy(item => item.optionKey).FirstOrDefault();
            return option == null ? null : definition?.Options
                .FirstOrDefault(item => item.Key == option.optionKey)?.Label;
        }

        private static string OwnershipSummary(CAPoliticalBeliefs beliefs)
        {
            var totals = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                { "private", PropertyAverage(beliefs, "private") },
                { "cooperative", PropertyAverage(beliefs, "cooperative") },
                { "public", PropertyAverage(beliefs, "public") },
                { "common", PropertyAverage(beliefs, "common") }
            };
            double maximum = totals.Values.Max();
            string[] represented = totals.Where(pair => pair.Value >= 15d
                    && pair.Value >= maximum / 3d)
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key).Select(pair => pair.Key).ToArray();
            if (represented.Length == 0) return null;
            if (represented.Length == 1)
                return represented[0] + " ownership";
            return "mixed " + NaturalList(represented) + " ownership";
        }

        private static string NaturalList(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0) return null;
            if (values.Count == 1) return values[0];
            if (values.Count == 2) return values[0] + " and " + values[1];
            return string.Join(", ", values.Take(values.Count - 1))
                + ", and " + values[values.Count - 1];
        }

        internal static string ValidationFailure(CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null) return "political order is missing";
            if (beliefs.questions == null)
                return "political question collection is missing";
            if (beliefs.questions.GroupBy(item => item?.questionKey,
                    StringComparer.Ordinal).Any(group => group.Key == null
                        || group.Count() > 1))
                return "political question collection contains duplicate or null state";
            foreach (CAPoliticalQuestionDef definition in
                CAPoliticalQuestionRegistry.Questions)
                if (State(beliefs, definition.Key) == null)
                    return definition.Key + " is not represented";
            foreach (CAPoliticalQuestionState state in beliefs.questions)
            {
                if (state == null || state.questionKey.NullOrEmpty())
                    return "political question state is incomplete";
                CAPoliticalQuestionDef definition =
                    CAPoliticalQuestionRegistry.Find(state.questionKey);
                if (definition == null)
                    return "unknown political question '"
                        + state.questionKey + "'";
                if (state.options == null || state.options.Count == 0)
                    return state.questionKey + " has no selected position";
                if (state.options.GroupBy(item => item?.optionKey,
                        StringComparer.Ordinal).Any(group => group.Key == null
                            || group.Count() > 1))
                    return state.questionKey
                        + " contains duplicate or null positions";
                if (state.options.Any(item => item == null || item.share <= 0
                        || item.share > 100 || !definition.Options.Any(option =>
                            option.Key == item.optionKey)
                        || (item.source != (byte)CAAxisSource.Generated
                            && item.source != (byte)CAAxisSource.Authored)))
                    return state.questionKey + " has an invalid position";
                if (state.options.Sum(item => item.share) != 100)
                    return state.questionKey + " does not total 100";
                if (!definition.Blendable && state.options.Count != 1)
                    return state.questionKey + " does not permit a mixture";
            }
            return null;
        }

        private static CAPoliticalQuestionState Merge(string key,
            IEnumerable<CAPoliticalQuestionState> values)
        {
            return new CAPoliticalQuestionState
            {
                questionKey = key,
                options = values.Where(item => item?.options != null)
                    .SelectMany(item => item.options)
                    .Where(item => item != null).Select(item => item.Copy())
                    .ToList()
            };
        }

        private static void NormalizeState(CAPoliticalQuestionState state)
        {
            if (state == null) return;
            CAPoliticalQuestionDef definition =
                CAPoliticalQuestionRegistry.Find(state.questionKey);
            if (definition == null) return;
            if (state.options == null)
                state.options = new List<CAPoliticalOptionShare>();
            state.options = state.options.Where(item => item != null
                    && item.share > 0 && definition.Options.Any(option =>
                        option.Key == item.optionKey))
                .GroupBy(item => item.optionKey, StringComparer.Ordinal)
                .Select(group => new CAPoliticalOptionShare
                {
                    optionKey = group.Key,
                    share = group.Sum(item => Math.Max(0, item.share)),
                    source = group.Any(item => item.source
                            == (byte)CAAxisSource.Authored)
                        ? (byte)CAAxisSource.Authored
                        : (byte)CAAxisSource.Generated
                }).OrderBy(item => Array.FindIndex(definition.Options,
                    option => option.Key == item.optionKey)).ToList();
            if (state.options.Count == 0) return;
            if (!definition.Blendable)
            {
                CAPoliticalOptionShare winner = state.options
                    .OrderByDescending(item => item.share)
                    .ThenBy(item => item.optionKey).First();
                winner.share = 100;
                state.options = new List<CAPoliticalOptionShare> { winner };
                return;
            }
            int total = state.options.Sum(item => item.share);
            if (total <= 0) return;
            var normalized = state.options.Select(item => new
            {
                Item = item,
                Exact = item.share * 100d / total,
                Floor = (int)Math.Floor(item.share * 100d / total)
            }).ToList();
            foreach (var item in normalized) item.Item.share = item.Floor;
            int remaining = 100 - normalized.Sum(item => item.Floor);
            foreach (var item in normalized.OrderByDescending(value =>
                         value.Exact - value.Floor)
                     .ThenBy(value => value.Item.optionKey)
                     .Take(remaining))
                item.Item.share++;
            state.options.RemoveAll(item => item.share <= 0);
        }

        private static void CompleteFromPreset(CAPoliticalBeliefs beliefs,
            string key, CAAxisSource source)
        {
            // Complete the destination from the chosen preset's finished
            // composition. Building directly with missingOnly would let the
            // common starting values occupy every empty question before the
            // preset's own positions were reached.
            var complete = new CAPoliticalBeliefs();
            BuildPreset(complete, key, source, false);
            foreach (CAPoliticalQuestionState state in complete.questions)
            {
                if (State(beliefs, state.questionKey)?.options?.Count > 0)
                    continue;
                beliefs.questions.Add(state.Copy());
            }
        }

        private static void BuildPreset(CAPoliticalBeliefs beliefs,
            string key, CAAxisSource source, bool missingOnly)
        {
            // Shared positions are explicit common values; each preset then
            // replaces the subjects on which it differs. Completion copies
            // from the finished preset rather than treating these values as a
            // universal generated order.
            Set(beliefs, "authority.leadership", source, missingOnly,
                "council", 55, "executive", 25, "assembly", 20);
            Set(beliefs, "authority.decisions", source, missingOnly,
                "majority", 60, "review", 25, "consensus", 15);
            Set(beliefs, "civic.participation", source, missingOnly,
                "residents", 75, "citizens", 25);
            Set(beliefs, "civic.liberty", source, missingOnly,
                "protected", 100);
            Set(beliefs, "civic.status", source, missingOnly,
                "equal", 70, "merit", 30);
            Set(beliefs, "civic.membership", source, missingOnly,
                "open", 65, "vetted", 35);
            foreach (string property in PropertyKeys)
                Set(beliefs, property, source, missingOnly,
                    "private", 40, "cooperative", 25,
                    "public", 25, "common", 10);
            Set(beliefs, "property.land", source, missingOnly,
                "private", 55, "cooperative", 15,
                "common", 20, "public", 10);
            Set(beliefs, "property.utilities", source, missingOnly,
                "public", 55, "cooperative", 25,
                "private", 15, "common", 5);
            Set(beliefs, "property.infrastructure", source, missingOnly,
                "public", 70, "cooperative", 15,
                "private", 10, "common", 5);
            Set(beliefs, "property.finance", source, missingOnly,
                "private", 35, "cooperative", 30, "public", 35);
            Set(beliefs, "economy.exchange", source, missingOnly,
                "market", 35, "regulated", 50,
                "planned", 10, "communal", 5);
            Set(beliefs, "economy.credit", source, missingOnly,
                "private", 35, "public", 35, "cooperative", 30);
            Set(beliefs, "economy.rent", source, missingOnly,
                "regulated", 100);
            Set(beliefs, "economy.work", source, missingOnly,
                "contract", 45, "cooperative", 35,
                "public", 15, "household", 5);
            Set(beliefs, "economy.provision", source, missingOnly,
                "public", 45, "common", 25,
                "household", 20, "voluntary", 10);
            Set(beliefs, "security.local", source, missingOnly,
                "watch", 45, "professional", 40, "adhoc", 15);
            Set(beliefs, "security.defense", source, missingOnly,
                "militia", 45, "professional", 35,
                "levy", 10, "adhoc", 10);
            Set(beliefs, "security.conflict", source, missingOnly,
                "quarter", 50, "combatants", 50);

            if (key == "progressive-civic-mix")
            {
                Set(beliefs, "authority.leadership", source, missingOnly,
                    "executive", 50, "council", 30, "assembly", 20);
                Set(beliefs, "authority.decisions", source, missingOnly,
                    "directive", 25, "majority", 45, "review", 30);
                Set(beliefs, "property.food", source, missingOnly,
                    "public", 45, "cooperative", 30, "private", 25);
                Set(beliefs, "property.care", source, missingOnly,
                    "public", 55, "cooperative", 25, "private", 20);
                Set(beliefs, "property.industry", source, missingOnly,
                    "private", 45, "cooperative", 40, "public", 15);
                Set(beliefs, "property.luxury", source, missingOnly,
                    "private", 55, "cooperative", 35, "public", 10);
                Set(beliefs, "economy.rent", source, missingOnly,
                    "noExtraction", 100);
            }
            else if (key == "civic-market")
            {
                PropertyPattern(beliefs, source, missingOnly,
                    68, 12, 12, 8);
                Set(beliefs, "property.infrastructure", source, missingOnly,
                    "public", 45, "private", 35, "cooperative", 20);
                Set(beliefs, "economy.exchange", source, missingOnly,
                    "market", 70, "regulated", 30);
                Set(beliefs, "economy.provision", source, missingOnly,
                    "household", 50, "public", 25, "voluntary", 25);
                Set(beliefs, "economy.rent", source, missingOnly,
                    "regulated", 65, "open", 35);
            }
            else if (key == "market-socialist")
            {
                Set(beliefs, "authority.leadership", source, missingOnly,
                    "council", 50, "assembly", 35, "executive", 15);
                PropertyPattern(beliefs, source, missingOnly,
                    20, 48, 22, 10);
                Set(beliefs, "property.food", source, missingOnly,
                    "cooperative", 45, "public", 35,
                    "private", 15, "common", 5);
                Set(beliefs, "property.utilities", source, missingOnly,
                    "public", 50, "cooperative", 40, "common", 10);
                Set(beliefs, "economy.exchange", source, missingOnly,
                    "regulated", 55, "market", 35, "communal", 10);
                Set(beliefs, "economy.work", source, missingOnly,
                    "cooperative", 60, "contract", 25, "public", 15);
                Set(beliefs, "economy.rent", source, missingOnly,
                    "noExtraction", 55, "cost", 45);
            }
            else if (key == "cooperative-commonwealth")
            {
                Set(beliefs, "authority.leadership", source, missingOnly,
                    "council", 60, "federal", 25, "assembly", 15);
                PropertyPattern(beliefs, source, missingOnly,
                    10, 55, 25, 10);
                Set(beliefs, "economy.exchange", source, missingOnly,
                    "communal", 45, "regulated", 35, "market", 20);
                Set(beliefs, "economy.credit", source, missingOnly,
                    "cooperative", 70, "public", 30);
                Set(beliefs, "economy.work", source, missingOnly,
                    "cooperative", 70, "household", 20, "public", 10);
                Set(beliefs, "economy.provision", source, missingOnly,
                    "common", 60, "public", 30, "household", 10);
            }
            else if (key == "developmental-executive")
            {
                Set(beliefs, "authority.leadership", source, missingOnly,
                    "executive", 70, "council", 20, "assembly", 10);
                Set(beliefs, "authority.decisions", source, missingOnly,
                    "directive", 55, "review", 25, "majority", 20);
                PropertyPattern(beliefs, source, missingOnly,
                    30, 15, 50, 5);
                Set(beliefs, "property.industry", source, missingOnly,
                    "public", 45, "private", 35, "cooperative", 20);
                Set(beliefs, "economy.exchange", source, missingOnly,
                    "regulated", 45, "planned", 35, "market", 20);
                Set(beliefs, "security.local", source, missingOnly,
                    "professional", 55, "executive", 35, "watch", 10);
            }
            else if (key == "customary-landed")
            {
                Set(beliefs, "authority.leadership", source, missingOnly,
                    "customary", 55, "executive", 35, "council", 10);
                Set(beliefs, "authority.decisions", source, missingOnly,
                    "custom", 65, "directive", 35);
                Set(beliefs, "civic.participation", source, missingOnly,
                    "standing", 45, "households", 35, "citizens", 20);
                Set(beliefs, "civic.liberty", source, missingOnly,
                    "customary", 100);
                Set(beliefs, "civic.status", source, missingOnly,
                    "hereditary", 60, "merit", 25, "caste", 15);
                Set(beliefs, "civic.membership", source, missingOnly,
                    "descent", 70, "vetted", 30);
                PropertyPattern(beliefs, source, missingOnly,
                    72, 8, 10, 10);
                Set(beliefs, "economy.rent", source, missingOnly,
                    "open", 100);
                Set(beliefs, "economy.work", source, missingOnly,
                    "household", 45, "contract", 35, "duty", 20);
            }
            else if (key == "communal-assembly")
            {
                Set(beliefs, "authority.leadership", source, missingOnly,
                    "assembly", 65, "council", 25, "federal", 10);
                Set(beliefs, "authority.decisions", source, missingOnly,
                    "consensus", 55, "majority", 45);
                PropertyPattern(beliefs, source, missingOnly,
                    5, 25, 15, 55);
                Set(beliefs, "economy.exchange", source, missingOnly,
                    "communal", 70, "regulated", 20, "market", 10);
                Set(beliefs, "economy.provision", source, missingOnly,
                    "common", 75, "public", 15, "household", 10);
                Set(beliefs, "security.local", source, missingOnly,
                    "watch", 65, "adhoc", 35);
                Set(beliefs, "security.defense", source, missingOnly,
                    "militia", 70, "levy", 20, "adhoc", 10);
            }
            else if (key == "central-party")
            {
                Set(beliefs, "authority.leadership", source, missingOnly,
                    "executive", 65, "council", 35);
                Set(beliefs, "authority.decisions", source, missingOnly,
                    "directive", 70, "custom", 30);
                Set(beliefs, "civic.participation", source, missingOnly,
                    "citizens", 65, "workers", 35);
                Set(beliefs, "civic.liberty", source, missingOnly,
                    "orthodox", 100);
                PropertyPattern(beliefs, source, missingOnly,
                    5, 10, 80, 5);
                Set(beliefs, "economy.exchange", source, missingOnly,
                    "planned", 75, "regulated", 20, "communal", 5);
                Set(beliefs, "economy.credit", source, missingOnly,
                    "public", 80, "restricted", 20);
                Set(beliefs, "economy.work", source, missingOnly,
                    "public", 60, "duty", 30, "cooperative", 10);
                Set(beliefs, "security.local", source, missingOnly,
                    "executive", 55, "professional", 45);
                Set(beliefs, "security.defense", source, missingOnly,
                    "professional", 70, "levy", 30);
            }
        }

        private static void PropertyPattern(CAPoliticalBeliefs beliefs,
            CAAxisSource source, bool missingOnly, int privateShare,
            int cooperative, int publicShare, int common)
        {
            foreach (string property in PropertyKeys)
                Set(beliefs, property, source, missingOnly,
                    "private", privateShare, "cooperative", cooperative,
                    "public", publicShare, "common", common);
        }

        private static void Set(CAPoliticalBeliefs beliefs, string key,
            CAAxisSource source, bool missingOnly, params object[] pairs)
        {
            if (missingOnly && State(beliefs, key)?.options?.Count > 0)
                return;
            var values = new List<KeyValuePair<string, int>>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                values.Add(new KeyValuePair<string, int>(
                    (string)pairs[i], (int)pairs[i + 1]));
            SetQuestion(beliefs, key, values, source);
        }

        private static void OverlaySelectedDomains(CAPoliticalBeliefs beliefs,
            string presetKey, CAAxisSource source, string seed)
        {
            var donor = new CAPoliticalBeliefs();
            BuildPreset(donor, presetKey, source, false);
            string[] groups = { CAPoliticalQuestionRegistry.Authority,
                CAPoliticalQuestionRegistry.Civic,
                CAPoliticalQuestionRegistry.Property,
                CAPoliticalQuestionRegistry.Economy,
                CAPoliticalQuestionRegistry.Security };
            int chosen = PositiveHash((seed ?? "politics") + ":group:"
                + beliefs.generationRoll) % groups.Length;
            foreach (CAPoliticalQuestionState state in donor.questions.Where(
                         item => CAPoliticalQuestionRegistry.Find(
                             item.questionKey)?.Group == groups[chosen]))
            {
                beliefs.questions.RemoveAll(item => item != null
                    && item.questionKey == state.questionKey);
                beliefs.questions.Add(state.Copy());
            }
        }

        private static string ClosestPreset(CAPoliticalBeliefs beliefs,
            string seed)
        {
            if (!HasVariables(beliefs))
                return Presets[PositiveHash((seed ?? beliefs?.id
                    ?? "politics") + ":" + (beliefs?.generationRoll ?? 0))
                    % Presets.Length].Key;
            int bestScore = int.MinValue;
            var best = new List<string>();
            foreach (CAPoliticalOrderPreset preset in Presets)
            {
                var candidate = new CAPoliticalBeliefs();
                BuildPreset(candidate, preset.Key, CAAxisSource.Generated,
                    false);
                int score = beliefs.questions.Where(item => item != null)
                    .Sum(item => Similarity(item,
                        State(candidate, item.questionKey)));
                if (score > bestScore)
                {
                    bestScore = score;
                    best.Clear();
                    best.Add(preset.Key);
                }
                else if (score == bestScore) best.Add(preset.Key);
            }
            return best[PositiveHash(seed ?? beliefs.id ?? "politics")
                % best.Count];
        }

        private static int Similarity(CAPoliticalQuestionState left,
            CAPoliticalQuestionState right)
        {
            if (left?.options == null || right?.options == null) return 0;
            return left.options.Sum(item => Math.Min(item.share,
                right.options.FirstOrDefault(other => other != null
                    && other.optionKey == item.optionKey)?.share ?? 0));
        }

        private static void SeedLegacyEvidence(CAPoliticalBeliefs beliefs)
        {
            if (beliefs?.positions == null) return;
            Legacy(beliefs, CAFactionAxes.Leadership,
                "authority.leadership", new Dictionary<string, string>
                {
                    { "single", "executive" }, { "council", "council" },
                    { "whole", "assembly" }, { "federated", "federal" },
                    { "none", "assembly" }
                });
            Legacy(beliefs, CAFactionAxes.Decisions,
                "authority.decisions", new Dictionary<string, string>
                {
                    { "decree", "directive" }, { "majority", "majority" },
                    { "consensus", "consensus" }, { "custom", "custom" }
                });
            Legacy(beliefs, CAFactionAxes.Participation,
                "civic.participation", new Dictionary<string, string>
                {
                    { "universal", "residents" }, { "members", "citizens" },
                    { "standing", "standing" }, { "heads", "households" }
                });
            Legacy(beliefs, CAFactionAxes.Dissent,
                "civic.liberty", new Dictionary<string, string>
                {
                    { "plural", "protected" },
                    { "majoritarian", "majoritarian" },
                    { "orthodoxy", "orthodox" },
                    { "customary", "customary" }
                });
            Legacy(beliefs, CAFactionAxes.Status,
                "civic.status", new Dictionary<string, string>
                {
                    { "equal", "equal" }, { "earned", "merit" },
                    { "hereditary", "hereditary" }, { "castes", "caste" }
                });
            Legacy(beliefs, CAFactionAxes.Membership,
                "civic.membership", new Dictionary<string, string>
                {
                    { "open", "open" }, { "vetted", "vetted" },
                    { "hereditary", "descent" }, { "closed", "closed" }
                });
            Legacy(beliefs, CAFactionAxes.Ownership,
                "property.industry", new Dictionary<string, string>
                {
                    { "private", "private" },
                    { "cooperative", "cooperative" },
                    { "common", "common" }, { "state", "public" }
                });
            Legacy(beliefs, CAFactionAxes.Economy,
                "economy.exchange", new Dictionary<string, string>
                {
                    { "market", "market" }, { "planned", "planned" },
                    { "communal", "communal" }
                });
            Legacy(beliefs, CAFactionAxes.Work,
                "economy.work", new Dictionary<string, string>
                {
                    { "contract", "contract" },
                    { "organized", "cooperative" },
                    { "duty", "duty" }, { "household", "household" }
                });
            Legacy(beliefs, CAFactionAxes.Support,
                "economy.provision", new Dictionary<string, string>
                {
                    { "private", "household" }, { "public", "public" },
                    { "communal", "common" },
                    { "charitable", "voluntary" }
                });
            Legacy(beliefs, CAFactionAxes.LocalOrder,
                "security.local", new Dictionary<string, string>
                {
                    { "none", "adhoc" }, { "watch", "watch" },
                    { "constabulary", "professional" },
                    { "rulers", "executive" }
                });
            Legacy(beliefs, CAFactionAxes.Defense,
                "security.defense", new Dictionary<string, string>
                {
                    { "none", "adhoc" }, { "levy", "levy" },
                    { "militia", "militia" },
                    { "professional", "professional" },
                    { "caste", "hereditary" }
                });
            Legacy(beliefs, CAFactionAxes.WarConduct,
                "security.conflict", new Dictionary<string, string>
                {
                    { "quarter", "quarter" },
                    { "combatants", "combatants" },
                    { "strength", "unrestricted" }
                });
        }

        private static void Legacy(CAPoliticalBeliefs beliefs,
            string axis, string question,
            Dictionary<string, string> crosswalk)
        {
            if (State(beliefs, question) != null) return;
            List<CAAxisEntry> entries = CAFactionAxes.EntriesOf(
                beliefs.positions, axis);
            var values = entries.Where(item => crosswalk.ContainsKey(
                    item.optionKey)).Select(item =>
                    new KeyValuePair<string, int>(crosswalk[item.optionKey],
                        1)).ToList();
            if (values.Count == 0) return;
            CAAxisSource source = entries.Any(item => item.source
                    == (byte)CAAxisSource.Authored)
                ? CAAxisSource.Authored : CAAxisSource.Generated;
            SetQuestion(beliefs, question, values, source);
        }

        private static void ProjectLegacyBeliefs(CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null || !HasVariables(beliefs)) return;
            beliefs.positions = new List<CAAxisEntry>();
            Project(beliefs, "authority.leadership",
                CAFactionAxes.Leadership, new Dictionary<string, string>
                {
                    { "executive", "single" }, { "council", "council" },
                    { "assembly", "whole" }, { "federal", "federated" },
                    { "customary", "council" }
                });
            Project(beliefs, "authority.decisions", CAFactionAxes.Decisions,
                new Dictionary<string, string>
                {
                    { "directive", "decree" }, { "majority", "majority" },
                    { "consensus", "consensus" }, { "custom", "custom" },
                    { "review", "majority" }
                });
            Project(beliefs, "civic.participation",
                CAFactionAxes.Participation, new Dictionary<string, string>
                {
                    { "residents", "universal" }, { "citizens", "members" },
                    { "workers", "members" }, { "households", "heads" },
                    { "standing", "standing" }
                });
            Project(beliefs, "civic.liberty", CAFactionAxes.Dissent,
                new Dictionary<string, string>
                {
                    { "protected", "plural" },
                    { "majoritarian", "majoritarian" },
                    { "restricted", "orthodoxy" },
                    { "orthodox", "orthodoxy" },
                    { "customary", "customary" }
                });
            Project(beliefs, "civic.status", CAFactionAxes.Status,
                new Dictionary<string, string>
                {
                    { "equal", "equal" }, { "merit", "earned" },
                    { "hereditary", "hereditary" }, { "caste", "castes" }
                });
            Project(beliefs, "civic.membership", CAFactionAxes.Membership,
                new Dictionary<string, string>
                {
                    { "open", "open" }, { "vetted", "vetted" },
                    { "descent", "hereditary" }, { "closed", "closed" }
                });
            ProjectProperty(beliefs);
            Project(beliefs, "economy.exchange", CAFactionAxes.Economy,
                new Dictionary<string, string>
                {
                    { "market", "market" }, { "regulated", "market" },
                    { "planned", "planned" }, { "communal", "communal" }
                });
            Project(beliefs, "economy.work", CAFactionAxes.Work,
                new Dictionary<string, string>
                {
                    { "contract", "contract" },
                    { "cooperative", "organized" },
                    { "public", "duty" }, { "household", "household" },
                    { "duty", "duty" }, { "coerced", "duty" }
                });
            Project(beliefs, "economy.provision", CAFactionAxes.Support,
                new Dictionary<string, string>
                {
                    { "household", "private" }, { "public", "public" },
                    { "common", "communal" },
                    { "voluntary", "charitable" }
                });
            Project(beliefs, "security.local", CAFactionAxes.LocalOrder,
                new Dictionary<string, string>
                {
                    { "adhoc", "none" }, { "watch", "watch" },
                    { "professional", "constabulary" },
                    { "executive", "rulers" }
                });
            Project(beliefs, "security.defense", CAFactionAxes.Defense,
                new Dictionary<string, string>
                {
                    { "adhoc", "none" }, { "levy", "levy" },
                    { "militia", "militia" },
                    { "professional", "professional" },
                    { "hereditary", "caste" }
                });
            Project(beliefs, "security.conflict", CAFactionAxes.WarConduct,
                new Dictionary<string, string>
                {
                    { "quarter", "quarter" },
                    { "combatants", "combatants" },
                    { "unrestricted", "strength" }
                });
            beliefs.derivationReceipts.Clear();
        }

        private static void ProjectProperty(CAPoliticalBeliefs beliefs)
        {
            var totals = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "private", 0 }, { "cooperative", 0 },
                { "common", 0 }, { "public", 0 }
            };
            foreach (string key in PropertyKeys)
                foreach (string option in totals.Keys.ToList())
                    totals[option] += Weight(beliefs, key, option);
            int maximum = totals.Values.Max();
            foreach (KeyValuePair<string, int> pair in totals.Where(pair =>
                         pair.Value >= Math.Max(15 * PropertyKeys.Length,
                             maximum / 3)))
            {
                string legacy = pair.Key == "public" ? "state" : pair.Key;
                CAFactionAxes.Add(beliefs.positions,
                    CAFactionAxes.Ownership, legacy,
                    QuestionSource(beliefs, PropertyKeys));
            }
        }

        private static void Project(CAPoliticalBeliefs beliefs,
            string question, string axis,
            Dictionary<string, string> crosswalk)
        {
            CAPoliticalQuestionState state = State(beliefs, question);
            if (state?.options == null) return;
            int maximum = state.options.Count == 0 ? 0
                : state.options.Max(item => item.share);
            foreach (CAPoliticalOptionShare option in state.options.Where(
                         item => item.share >= Math.Max(15, maximum / 3)))
            {
                if (!crosswalk.TryGetValue(option.optionKey,
                        out string mapped)) continue;
                CAFactionAxes.Add(beliefs.positions, axis, mapped,
                    (CAAxisSource)option.source);
            }
        }

        private static CAAxisSource QuestionSource(CAPoliticalBeliefs beliefs,
            IEnumerable<string> keys)
        {
            return keys.Select(key => State(beliefs, key))
                .Where(state => state?.options != null)
                .SelectMany(state => state.options)
                .Any(item => item.source == (byte)CAAxisSource.Authored)
                    ? CAAxisSource.Authored : CAAxisSource.Generated;
        }

        private static string GeneratedName(CAPoliticalBeliefs beliefs,
            int roll)
        {
            int executive = Weight(beliefs, "authority.leadership",
                "executive");
            int democratic = Weight(beliefs, "authority.leadership",
                    "council")
                + Weight(beliefs, "authority.leadership", "assembly");
            int federal = Weight(beliefs, "authority.leadership", "federal");
            double privateShare = PropertyAverage(beliefs, "private");
            double cooperative = PropertyAverage(beliefs, "cooperative");
            double publicShare = PropertyAverage(beliefs, "public");
            double common = PropertyAverage(beliefs, "common");
            int orthodoxy = Weight(beliefs, "civic.liberty", "orthodox");
            int inherited = Weight(beliefs, "civic.status", "hereditary")
                + Weight(beliefs, "civic.status", "caste");

            if (orthodoxy >= 60)
                return Pick(roll, "Central party state",
                    "Directive party state", "Orthodox state");
            if (inherited >= 45)
                return Pick(roll, "Customary landed order",
                    "Hereditary civic order", "Landed commonwealth");
            if (publicShare >= 55 && executive >= 45)
                return Pick(roll, "Developmental state",
                    "Public development order", "Executive commonwealth");
            if (cooperative + common >= 62 && democratic >= 55)
                return Pick(roll, "Cooperative commonwealth",
                    "Communal assembly", "Mutual commonwealth");
            if (cooperative >= 38 && democratic >= 55)
                return Pick(roll, "Cooperative republic",
                    "Civic cooperative order", "Worker commonwealth");
            if (privateShare >= 55 && democratic >= 55)
                return Pick(roll, "Civic market republic",
                    "Commercial republic", "Representative market order");
            if (federal >= 35)
                return Pick(roll, "Federal commonwealth",
                    "Delegated civic order", "Confederal order");
            if (executive >= 45)
                return Pick(roll, "Executive civic order",
                    "Presidential order", "Directive commonwealth");
            return Pick(roll, "Civic mixed economy",
                "Plural civic order", "Mixed civic commonwealth");
        }

        private static double PropertyAverage(CAPoliticalBeliefs beliefs,
            string option)
        {
            return PropertyKeys.Average(key => Weight(beliefs, key, option));
        }

        private static string Pick(int roll, params string[] values)
        {
            return values[Math.Abs(roll) % values.Length];
        }

        private static int PositiveHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value ?? "")
                {
                    hash ^= character;
                    hash *= 16777619;
                }
                return (int)(hash & 0x7fffffff);
            }
        }
    }

    internal sealed class Dialog_CAPoliticalOrderEditor : Window
    {
        private readonly CAPoliticalBeliefs beliefs;
        private readonly List<CAAxisEntry> representedInstitutions;
        private readonly CAFoundingArrangement foundingArrangement;
        private readonly string seed;
        private readonly Action changed;
        private string group = CAPoliticalQuestionRegistry.Authority;
        private Vector2 scroll;
        private float viewHeight;

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(1120f, UI.screenWidth - 48f),
            Mathf.Min(820f, UI.screenHeight - 48f));

        private Dialog_CAPoliticalOrderEditor(CAPoliticalBeliefs beliefs,
            List<CAAxisEntry> representedInstitutions, string seed,
            CAFoundingArrangement foundingArrangement, Action changed)
        {
            this.beliefs = beliefs ?? new CAPoliticalBeliefs();
            this.representedInstitutions = representedInstitutions;
            this.seed = seed ?? "political-order";
            this.foundingArrangement = foundingArrangement;
            this.changed = changed;
            CAPoliticalBeliefsModel.Ensure(this.beliefs, this.seed);
            // One close affordance, not two. The X and the bottom Close
            // button did the same thing, and doCloseButton also reserves a
            // band of empty chrome at the foot of the window.
            doCloseX = true;
            doCloseButton = false;
            doWindowBackground = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        protected override float Margin => 0f;

        internal static Dialog_CAPoliticalOrderEditor ForFounding(
            CAPoliticalBeliefs beliefs, string seed,
            CAFoundingArrangement foundingArrangement, Action changed)
        {
            return new Dialog_CAPoliticalOrderEditor(beliefs, null, seed,
                foundingArrangement, changed);
        }

        internal static Dialog_CAPoliticalOrderEditor ForEstablished(
            CAPoliticalBeliefs beliefs,
            List<CAAxisEntry> representedInstitutions, string seed,
            Action changed)
        {
            return new Dialog_CAPoliticalOrderEditor(beliefs,
                representedInstitutions, seed, null, changed);
        }

        public override void DoWindowContents(Rect inRect)
        {
            inRect = CAOpeningTheme.BeginWindowSurface(inRect);
            try
            {
                DoEditorContents(inRect);
            }
            finally
            {
                CAOpeningTheme.EndWindowSurface();
            }
        }

        private void DoEditorContents(Rect inRect)
        {
            long performanceStarted = CAConvergenceExercise
                    .PoliticalProbeEnabled
                ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
            GameFont previous = Text.Font;
            Text.Font = GameFont.Medium;
            GUI.color = CAOpeningTheme.TextHi;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f),
                "Political Order");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            string description = representedInstitutions == null
                ? "Choose what the founders believe should govern their "
                    + "society. Their first rules at landing are set separately."
                : "Choose what this population believes should govern society. "
                    + "The institutions already in use are shown separately.";
            float descriptionHeight = Text.CalcHeight(description,
                inRect.width);
            Widgets.Label(new Rect(0f, 38f, inRect.width,
                descriptionHeight), description);
            float y = 44f + descriptionHeight;
            string[] tabs = { "Overview",
                CAPoliticalQuestionRegistry.Authority,
                CAPoliticalQuestionRegistry.Civic,
                CAPoliticalQuestionRegistry.Property,
                CAPoliticalQuestionRegistry.Economy,
                CAPoliticalQuestionRegistry.Security };
            string[] tabLabels = { "Overview", "Authority", "Civic life",
                "Property", "Economy and work", "Security" };
            float tabGap = 5f;
            float tabWidth = (inRect.width - tabGap * (tabs.Length - 1))
                / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                Rect tab = new Rect(i * (tabWidth + tabGap), y,
                    tabWidth, 32f);
                // The cells are equal width, but only the selected tab used
                // to draw its frame, so the rest were bare centered text in
                // invisible cells -- and because the labels differ in length
                // ("Property" against "Economy and work") the row read as
                // arbitrarily spaced rather than as a tab strip. Every tab
                // draws its cell; selection is shown by highlighting it.
                bool selected = group == tabs[i];
                if (CAOpeningTheme.Chip(tab, tabLabels[i], selected)
                    && !selected)
                {
                    group = tabs[i];
                    scroll = Vector2.zero;
                }
            }
            y += 40f;
            Rect outer = new Rect(0f, y, inRect.width,
                inRect.height - y - 48f);
            Rect view = new Rect(0f, 0f, outer.width - 18f,
                Mathf.Max(outer.height, viewHeight));
            Widgets.BeginScrollView(outer, ref scroll, view);
            float rowY = 0f;
            if (group == "Overview")
            {
                DrawIdentity(ref rowY, view.width);
                DrawOverview(ref rowY, view.width);
            }
            else
                DrawQuestions(ref rowY, view.width, group);
            viewHeight = rowY + 12f;
            Widgets.EndScrollView();
            Text.Font = previous;
            // Visible close affordance: a "Done" button at the bottom
            // of the editor, clearer than relying on the X or ESC alone.
            float btnW = 120f;
            float btnH = 32f;
            Rect doneBtn = new Rect(
                inRect.x + (inRect.width - btnW) * 0.5f,
                inRect.yMax - btnH - 8f, btnW, btnH);
            if (Widgets.ButtonText(doneBtn, "Done"))
                Close();
            if (performanceStarted != 0L)
                CAConvergenceExercise.RecordPoliticalFrame(
                    System.Diagnostics.Stopwatch.GetTimestamp()
                        - performanceStarted);
        }

        private void DrawIdentity(ref float y, float width)
        {
            Rect card = new Rect(0f, y, width, 126f);
            Widgets.DrawBoxSolid(card, CAOpeningTheme.Surface);
            CAOpeningTheme.Border(card, CAOpeningTheme.Hairline);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(card.x + 12f, card.y + 9f,
                    card.width - 24f, 30f),
                CAPoliticalOrderModel.Identity(beliefs));
            Text.Font = GameFont.Small;
            string source = beliefs.nameAuthored
                ? "Custom name. The description still follows the choices below."
                : "Name and description generated from the choices below.";
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(card.x + 12f, card.y + 43f,
                card.width - 24f, 32f), source);
            GUI.color = Color.white;
            float actionWidth = Mathf.Min(180f, (card.width - 30f) / 2f);
            Rect reroll = new Rect(card.x + 12f, card.y + 84f,
                actionWidth, 30f);
            Rect rename = new Rect(reroll.xMax + 6f, reroll.y,
                actionWidth, 30f);
            if (CAOpeningTheme.GhostButton(reroll,
                    "Reroll generated name"))
                RerollName();
            if (CAOpeningTheme.GhostButton(rename, "Custom name..."))
                Rename();
            y += card.height + 10f;
        }

        private void DrawOverview(ref float y, float width)
        {
            float gap = 6f;
            int columns = width >= 720f ? 4 : 2;
            float buttonWidth = (width - gap * (columns - 1)) / columns;
            var labels = new List<string> { "Political presets...",
                "Generate another", "Save order..." };
            var actions = new List<Action> { OpenPresets, GenerateAnother,
                Save };
            if (CAAuthoringProfileLibrary.PoliticalOrders.Count > 0)
            {
                labels.Insert(1, "Saved orders...");
                actions.Insert(1, OpenSaved);
            }
            int rows = (labels.Count + columns - 1) / columns;
            for (int i = 0; i < labels.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                Rect button = new Rect(column * (buttonWidth + gap),
                    y + row * 36f, buttonWidth, 30f);
                if (CAOpeningTheme.GhostButton(button, labels[i]))
                    actions[i]();
            }
            y += rows * 36f + 8f;
            DrawHeading(ref y, width, "Summary");
            DrawParagraph(ref y, width,
                CAPoliticalOrderModel.ShortSummary(beliefs), Color.white);

            if (foundingArrangement != null)
                DrawFoundingRelation(ref y, width);
            if (representedInstitutions != null)
                DrawRepresentedInstitutions(ref y, width);
        }

        private void DrawFoundingRelation(ref float y, float width)
        {
            DrawHeading(ref y, width, "Rules at landing");
            List<CAPoliticalBeliefPractice.CAFoundingBeliefReading> readings =
                CAPoliticalBeliefPractice.ReadAgainstPoliticalBeliefs(
                    beliefs, foundingArrangement);
            int tensions = readings.Count(item => !item.Silent
                && !item.conforms);
            string summary = tensions == 0
                ? "The landing rules agree with these beliefs wherever they "
                    + "cover the same subject."
                : tensions + " difference" + (tensions == 1 ? "" : "s")
                    + " between these beliefs and the landing rules.";
            DrawParagraph(ref y, width, summary, tensions == 0
                ? CACreationUI.Authored : ColorLibrary.Yellow);
            foreach (CAPoliticalBeliefPractice.CAFoundingBeliefReading reading
                in readings.Where(item => !item.Silent && !item.conforms))
                DrawParagraph(ref y, width, reading.title + ": "
                    + reading.belief + "; landing rule: "
                    + reading.adopted + ".", ColorLibrary.Yellow,
                    GameFont.Tiny);
        }

        private void DrawRepresentedInstitutions(ref float y, float width)
        {
            DrawHeading(ref y, width, "Institutions in use");
            string summary = representedInstitutions.Count == 0
                ? "No institutions have been set. Political beliefs do not "
                    + "add them automatically."
                : CAFactionStructureModel.Summary(representedInstitutions);
            DrawParagraph(ref y, width, summary,
                representedInstitutions.Count == 0
                    ? CACreationUI.Unset : Color.white);
            List<string> tensions = CAFactionStructureModel.Tensions(
                beliefs, representedInstitutions);
            if (tensions.Count == 0) return;
            DrawParagraph(ref y, width, tensions.Count
                + " difference" + (tensions.Count == 1 ? "" : "s")
                + " between beliefs and institutions.",
                ColorLibrary.Yellow);
            foreach (string tension in tensions)
                DrawParagraph(ref y, width, tension, ColorLibrary.Yellow,
                    GameFont.Tiny);
        }

        private void DrawQuestions(ref float y, float width,
            string selectedGroup)
        {
            foreach (CAPoliticalQuestionDef definition in
                CAPoliticalQuestionRegistry.InGroup(selectedGroup))
            {
                CAPoliticalQuestionState state =
                    CAPoliticalOrderModel.State(beliefs, definition.Key);
                string sentence = CAPoliticalOrderModel.QuestionSentence(
                    beliefs, definition.Key) ?? "Not yet generated.";
                // MEASURE THE COLUMN THAT IS ACTUALLY DRAWN. These were
                // measured at width-170 and drawn at width-180, so every
                // sentence wrapped into more lines than the reserved height
                // allowed and the last line was clipped away mid-word --
                // "...with a limited role for" and then nothing. The label
                // column is width-180; measure width-180.
                const float labelColumn = 180f;
                float promptHeight = Text.CalcHeight(definition.Prompt,
                    width - labelColumn);
                float sentenceHeight = Text.CalcHeight(sentence,
                    width - labelColumn);
                float rowHeight = Mathf.Max(86f,
                    promptHeight + sentenceHeight + 38f);
                Rect row = new Rect(0f, y, width, rowHeight);
                Widgets.DrawBoxSolid(row, CAOpeningTheme.Surface);
                CAOpeningTheme.Border(row, CAOpeningTheme.Hairline);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(10f, y + 7f, width - 180f, 26f),
                    definition.Label);
                Text.Font = GameFont.Tiny;
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(new Rect(10f, y + 31f, width - 180f,
                    promptHeight), definition.Prompt);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(10f, y + 34f + promptHeight,
                    width - 180f, sentenceHeight), sentence);
                Rect edit = new Rect(width - 158f,
                    y + (rowHeight - 34f) * 0.5f, 148f, 34f);
                if (CAOpeningTheme.GhostButton(edit,
                        definition.Blendable ? "Set mix..." : "Change..."))
                    OpenQuestion(definition, state);
                y += rowHeight + 8f;
            }
        }

        private void OpenQuestion(CAPoliticalQuestionDef definition,
            CAPoliticalQuestionState state)
        {
            if (definition.Blendable)
            {
                Find.WindowStack.Add(new Dialog_CAPoliticalMixtureEditor(
                    beliefs, definition, changed));
                return;
            }
            var choices = new List<CACreationChoice>();
            string current = state?.options?.OrderByDescending(item =>
                item.share).FirstOrDefault()?.optionKey;
            foreach (CAPoliticalOptionDef option in definition.Options)
            {
                CAPoliticalOptionDef local = option;
                choices.Add(new CACreationChoice
                {
                    Key = local.Key,
                    Name = local.Label.CapitalizeFirst(),
                    Summary = local.Clause,
                    Details = definition.Prompt,
                    Badge = current == local.Key ? "Selected" : "Available",
                    Accent = current == local.Key
                        ? CACreationUI.Authored : CACreationUI.Accent,
                    Selected = current == local.Key,
                    ConfirmLabel = "Use this position",
                    Choose = delegate
                    {
                        CAPoliticalOrderModel.SetQuestion(beliefs,
                            definition.Key,
                            new[] { new KeyValuePair<string, int>(
                                local.Key, 100) }, CAAxisSource.Authored);
                        changed?.Invoke();
                    }
                });
            }
            CACreationUI.OpenChoices(definition.Label, definition.Prompt,
                choices);
        }

        private void OpenPresets()
        {
            var choices = new List<CACreationChoice>();
            foreach (CAPoliticalOrderPreset preset in
                CAPoliticalOrderModel.Presets)
            {
                CAPoliticalOrderPreset local = preset;
                var preview = new CAPoliticalBeliefs();
                CAPoliticalOrderModel.ApplyPreset(preview, local.Key,
                    CAAxisSource.Generated);
                choices.Add(new CACreationChoice
                {
                    Key = local.Key,
                    Name = local.Label,
                    Summary = local.Summary,
                    CompactSummary = CAPoliticalOrderModel.Identity(preview),
                    Details = CAPoliticalOrderModel.Description(preview),
                    Badge = "Complete order",
                    Accent = CACreationUI.Authored,
                    Selected = false,
                    ConfirmLabel = "Use this order",
                    Choose = delegate
                    {
                        CAPoliticalOrderModel.ApplyPreset(beliefs,
                            local.Key, CAAxisSource.Authored);
                        changed?.Invoke();
                    }
                });
            }
            CACreationUI.OpenChoices("Political Orders",
                "Each preset answers every Political Order question. You can "
                    + "change any answer afterward.",
                choices);
        }

        private void OpenSaved()
        {
            var choices = new List<CACreationChoice>();
            foreach (CAUserPoliticalOrderProfile profile in
                CAAuthoringProfileLibrary.PoliticalOrders.OrderBy(item =>
                    item.displayName))
            {
                CAUserPoliticalOrderProfile local = profile;
                choices.Add(new CACreationChoice
                {
                    Key = local.key,
                    Name = local.displayName,
                    Summary = CAPoliticalOrderModel.ShortSummary(local.values),
                    CompactSummary = CAPoliticalOrderModel.Identity(
                        local.values),
                    Details = CAPoliticalOrderModel.Description(local.values),
                    Badge = "Saved order",
                    Accent = CACreationUI.Authored,
                    ConfirmLabel = "Use this order",
                    Choose = delegate
                    {
                        CAAuthoringProfileLibrary.Apply(local, beliefs);
                        changed?.Invoke();
                    }
                });
            }
            CACreationUI.OpenChoices("Saved Political Orders",
                "Choose a saved Political Order for this population.",
                choices);
        }

        private void GenerateAnother()
        {
            CAPoliticalOrderModel.Randomize(beliefs, seed);
            changed?.Invoke();
        }

        private void RerollName()
        {
            CAPoliticalOrderModel.RerollName(beliefs);
            changed?.Invoke();
        }

        private void Rename()
        {
            Find.WindowStack.Add(new Dialog_CAProfileName(
                "Name this Political Order",
                CAPoliticalOrderModel.Identity(beliefs), value =>
                {
                    CAPoliticalOrderModel.Rename(beliefs, value);
                    changed?.Invoke();
                }));
        }

        private void Save()
        {
            Find.WindowStack.Add(new Dialog_CAProfileName(
                "Save Political Order",
                CAPoliticalOrderModel.Identity(beliefs), value =>
                {
                    CAAuthoringProfileLibrary.SavePoliticalOrder(value,
                        beliefs);
                    changed?.Invoke();
                }));
        }

        private static void DrawHeading(ref float y, float width,
            string value)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, width, 32f), value);
            Text.Font = GameFont.Small;
            y += 36f;
        }

        private static void DrawParagraph(ref float y, float width,
            string value, Color color, GameFont font = GameFont.Small)
        {
            Text.Font = font;
            float height = Text.CalcHeight(value, width);
            GUI.color = color;
            Widgets.Label(new Rect(0f, y, width, height), value);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += height + 8f;
        }
    }

    internal sealed class Dialog_CAPoliticalMixtureEditor : Window
    {
        private readonly CAPoliticalBeliefs beliefs;
        private readonly CAPoliticalQuestionDef definition;
        private readonly Action changed;
        private readonly Dictionary<string, float> shares =
            new Dictionary<string, float>(StringComparer.Ordinal);

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(760f, UI.screenWidth - 48f),
            Mathf.Min(680f, UI.screenHeight - 48f));

        internal Dialog_CAPoliticalMixtureEditor(
            CAPoliticalBeliefs beliefs, CAPoliticalQuestionDef definition,
            Action changed)
        {
            this.beliefs = beliefs;
            this.definition = definition;
            this.changed = changed;
            CAPoliticalQuestionState state = CAPoliticalOrderModel.State(
                beliefs, definition.Key);
            foreach (CAPoliticalOptionDef option in definition.Options)
                shares[option.Key] = state?.options?.FirstOrDefault(item =>
                    item != null && item.optionKey == option.Key)?.share ?? 0f;
            if (shares.Values.Sum() <= 0f)
                shares[definition.Options[0].Key] = 100f;
            doCloseX = true;
            doWindowBackground = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        protected override float Margin => 0f;

        public override void DoWindowContents(Rect inRect)
        {
            inRect = CAOpeningTheme.BeginWindowSurface(inRect);
            try
            {
                DoEditorContents(inRect);
            }
            finally
            {
                CAOpeningTheme.EndWindowSurface();
            }
        }

        private void DoEditorContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            GUI.color = CAOpeningTheme.TextHi;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f),
                definition.Label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            float promptHeight = Text.CalcHeight(definition.Prompt,
                inRect.width);
            Widgets.Label(new Rect(0f, 38f, inRect.width, promptHeight),
                definition.Prompt);
            string note = "Set how common each answer is. The values are "
                + "scaled to 100% when applied.";
            float noteHeight = Text.CalcHeight(note, inRect.width);
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(0f, 44f + promptHeight, inRect.width,
                noteHeight), note);
            GUI.color = Color.white;
            float y = 56f + promptHeight + noteHeight;
            foreach (CAPoliticalOptionDef option in definition.Options)
            {
                Rect row = new Rect(0f, y, inRect.width, 76f);
                Widgets.DrawBoxSolid(row, CAOpeningTheme.Surface);
                CAOpeningTheme.Border(row, CAOpeningTheme.Hairline);
                Widgets.Label(new Rect(10f, y + 7f, 220f, 26f),
                    option.Label.CapitalizeFirst());
                float value = Widgets.HorizontalSlider(new Rect(238f,
                        y + 8f, inRect.width - 318f, 24f),
                    shares[option.Key], 0f, 100f);
                shares[option.Key] = Mathf.Round(value / 5f) * 5f;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(inRect.width - 72f, y + 6f,
                    62f, 28f), Mathf.RoundToInt(shares[option.Key]) + "%");
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Tiny;
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(new Rect(10f, y + 40f, inRect.width - 20f,
                    30f), option.Clause);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += 82f;
            }
            float total = shares.Values.Sum();
            GUI.color = total <= 0f ? ColorLibrary.RedReadable
                : ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(0f, y + 2f, inRect.width - 130f, 30f),
                total <= 0f ? "Choose at least one answer."
                    : "Total: " + Mathf.RoundToInt(total)
                        + "%" + (Mathf.RoundToInt(total) == 100
                            ? "." : ". This will be scaled to 100%."));
            GUI.color = Color.white;
            if (CAOpeningTheme.PrimaryButton(new Rect(inRect.width - 120f,
                    y, 120f, 32f), "Apply", total > 0f) && total > 0f)
            {
                CAPoliticalOrderModel.SetQuestion(beliefs, definition.Key,
                    shares.Select(pair => new KeyValuePair<string, int>(
                        pair.Key, Mathf.RoundToInt(pair.Value))),
                    CAAxisSource.Authored);
                changed?.Invoke();
                Close();
            }
        }
    }
}
