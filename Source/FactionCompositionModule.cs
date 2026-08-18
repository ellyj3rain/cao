using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Compatibility questions shared by Political Order projections and
    // represented institutions. Each entry is
    // one mechanism. Several mechanisms may coexist on an axis unless the
    // axis declares a genuine absence invariant (for example no standing
    // defense cannot coexist with a professional force).
    // Leadership, decisions, participation, dissent, ownership, economy, work,
    // support, membership, status, local order, defense, and war conduct remain
    // separate because they produce different game state.
    public enum CAAxisSource : byte
    {
        Unset = 0,
        Generated = 1,
        Authored = 2,
    }

    public sealed class CAAxisEntry : IExposable
    {
        public string axisKey;
        public string optionKey;
        public byte source;

        public void ExposeData()
        {
            Scribe_Values.Look(ref axisKey, "axis");
            Scribe_Values.Look(ref optionKey, "option");
            Scribe_Values.Look(ref source, "source", (byte)0);
        }
    }

    public sealed class CAAxisOption
    {
        public string Key;
        public string Label;
        public string Words;

        public CAAxisOption(string key, string label, string words)
        { Key = key; Label = label; Words = words; }
    }

    public sealed class CAAxisDef
    {
        public string Key;
        public string Label;
        public string Question;
        public string Consumers;
        public CAAxisOption[] Options;
    }

    internal static class CAFactionAxes
    {
        internal const string Leadership = "leadership";
        internal const string Decisions = "decisions";
        internal const string Participation = "participation";
        internal const string Dissent = "dissent";
        internal const string Ownership = "ownership";
        internal const string Economy = "economy";
        internal const string Work = "work";
        internal const string Support = "support";
        internal const string Membership = "membership";
        internal const string Status = "status";
        internal const string LocalOrder = "localOrder";
        internal const string Defense = "defense";
        internal const string WarConduct = "warConduct";

        internal static readonly CAAxisDef[] Axes =
        {
            new CAAxisDef
            {
                Key = Leadership, Label = "Leadership",
                Question = "Who should lead the faction?",
                Consumers = "Faction governance, offices, and founding suggestions.",
                Options = new[]
                {
                    new CAAxisOption("single", "single leader",
                        "one leader has final authority"),
                    new CAAxisOption("council", "council",
                        "a standing council leads"),
                    new CAAxisOption("whole", "all members",
                        "all members share authority"),
                    new CAAxisOption("federated", "delegated leaders",
                        "local leaders grant limited authority upward"),
                    new CAAxisOption("none", "no permanent leader",
                        "leadership exists only when needed")
                }
            },
            new CAAxisDef
            {
                Key = Decisions, Label = "Decisions",
                Question = "How should faction-wide decisions be made?",
                Consumers = "Faction decisions, institutional legitimacy, and political reactions.",
                Options = new[]
                {
                    new CAAxisOption("decree", "decree",
                        "the leader decides"),
                    new CAAxisOption("majority", "majority vote",
                        "the eligible vote; the majority binds all"),
                    new CAAxisOption("consensus", "consensus",
                        "the eligible must consent for it to bind"),
                    new CAAxisOption("custom", "custom and standing",
                        "custom and personal standing decide")
                }
            },
            new CAAxisDef
            {
                Key = Participation, Label = "Participation",
                Question = "Who should take part in faction decisions?",
                Consumers = "Founding terms, public voice, and political conflict.",
                Options = new[]
                {
                    new CAAxisOption("universal", "all residents",
                        "all residents take part"),
                    new CAAxisOption("members", "members only",
                        "enrolled members take part; others do not"),
                    new CAAxisOption("standing", "earned standing",
                        "those with earned standing take part"),
                    new CAAxisOption("heads", "household heads",
                        "one voice per household")
                }
            },
            new CAAxisDef
            {
                Key = Dissent, Label = "Dissent",
                Question = "How should political dissent be treated?",
                Consumers = "Political conflict, organization membership, and social response.",
                Options = new[]
                {
                    new CAAxisOption("plural", "protected",
                        "open dissent is protected"),
                    new CAAxisOption("majoritarian", "majority rule",
                        "the majority decides; minorities are tolerated"),
                    new CAAxisOption("orthodoxy", "one doctrine",
                        "open dissent is suppressed"),
                    new CAAxisOption("customary", "custom and rank",
                        "treatment depends on custom and status")
                }
            },
            new CAAxisDef
            {
                Key = Ownership, Label = "Ownership",
                Question = "Who should own farms and workshops?",
                Consumers = "Founding supplies, property acts, and economic organization.",
                Options = new[]
                {
                    new CAAxisOption("private", "private owners",
                        "individuals own farms and workshops"),
                    new CAAxisOption("cooperative", "cooperatives",
                        "workers and communities own them together"),
                    new CAAxisOption("common", "shared ownership",
                        "productive property is held in common"),
                    new CAAxisOption("state", "faction ownership",
                        "the faction owns farms and workshops")
                }
            },
            new CAAxisDef
            {
                Key = Economy, Label = "Economy",
                Question = "How should goods and work be distributed?",
                Consumers = "Trade, distribution, settlement economies, and organizations.",
                Options = new[]
                {
                    new CAAxisOption("market", "trade",
                        "prices and bargains distribute goods"),
                    new CAAxisOption("planned", "planned distribution",
                        "leaders allocate goods and work"),
                    new CAAxisOption("communal", "shared stores",
                        "goods are pooled and shared")
                }
            },
            new CAAxisDef
            {
                Key = Work, Label = "Work",
                Question = "How should work be assigned?",
                Consumers = "Founding work rules, work assignment, and compelled-work reactions.",
                Options = new[]
                {
                    new CAAxisOption("contract", "hired work",
                        "workers choose paid jobs"),
                    new CAAxisOption("organized", "organized workers",
                        "worker groups organize jobs"),
                    new CAAxisOption("duty", "required service",
                        "members owe work to the faction"),
                    new CAAxisOption("household", "household work",
                        "families and households assign work")
                }
            },
            new CAAxisDef
            {
                Key = Support, Label = "Support",
                Question = "Who should provide food, shelter, and care during hardship?",
                Consumers = "Emergency support, provisions, and shared-provision reactions.",
                Options = new[]
                {
                    new CAAxisOption("private", "self-provided",
                        "each household provides for itself"),
                    new CAAxisOption("public", "faction support",
                        "the faction supplies basic needs"),
                    new CAAxisOption("communal", "shared stores",
                        "common stores supply basic needs"),
                    new CAAxisOption("charitable", "charity",
                        "religious and voluntary groups provide support")
                }
            },
            new CAAxisDef
            {
                Key = Membership, Label = "Membership",
                Question = "Who should be allowed to join the faction?",
                Consumers = "Faction membership, population affiliation, and organizations.",
                Options = new[]
                {
                    new CAAxisOption("open", "open",
                        "whoever comes and stays may belong"),
                    new CAAxisOption("vetted", "approved",
                        "joining requires service, sponsorship, or an oath"),
                    new CAAxisOption("hereditary", "inherited",
                        "membership passes through families"),
                    new CAAxisOption("closed", "closed",
                        "outsiders are rarely admitted")
                }
            },
            new CAAxisDef
            {
                Key = Status, Label = "Status",
                Question = "What should determine social rank?",
                Consumers = "Offices, succession, rank, and social influence.",
                Options = new[]
                {
                    new CAAxisOption("equal", "broadly equal",
                        "members have no fixed rank"),
                    new CAAxisOption("earned", "earned ranks",
                        "standing follows deeds and service"),
                    new CAAxisOption("hereditary", "hereditary ranks",
                        "standing is inherited and persists"),
                    new CAAxisOption("castes", "castes",
                        "each member belongs to a fixed caste")
                }
            },
            new CAAxisDef
            {
                Key = LocalOrder, Label = "Local order",
                Question = "Who should enforce order inside settlements?",
                Consumers = "Settlement guards, enforcement acts, and local institutions.",
                Options = new[]
                {
                    new CAAxisOption("none", "no standing watch",
                        "no permanent watch exists"),
                    new CAAxisOption("watch", "community watch",
                        "residents keep watch in turns"),
                    new CAAxisOption("constabulary", "guards",
                        "professional guards keep order"),
                    new CAAxisOption("rulers", "ruler's guard",
                        "order answers to the ruler, not the whole")
                }
            },
            new CAAxisDef
            {
                Key = Defense, Label = "Defense",
                Question = "Who should defend the faction?",
                Consumers = "Defense organizations, military service, and settlement defense.",
                Options = new[]
                {
                    new CAAxisOption("none", "no standing defense",
                        "defenders assemble only when needed"),
                    new CAAxisOption("levy", "general levy",
                        "every able member owes war service"),
                    new CAAxisOption("militia", "trained militia",
                        "part-time trained; mustered at need"),
                    new CAAxisOption("professional", "soldiers",
                        "professional soldiers defend the faction"),
                    new CAAxisOption("caste", "warrior caste",
                        "war belongs to a hereditary warrior caste")
                }
            },
            new CAAxisDef
            {
                Key = WarConduct, Label = "Treatment in war",
                Question = "How should defeated enemies and noncombatants be treated?",
                Consumers = "Combat conduct, surrender and quarter, and conflict legitimacy.",
                Options = new[]
                {
                    new CAAxisOption("quarter", "surrender accepted",
                        "defeated enemies are spared"),
                    new CAAxisOption("strength", "victors decide",
                        "defeat offers no protection"),
                    new CAAxisOption("combatants", "combatants only",
                        "only those who bear arms may be struck")
                }
            }
        };

        internal static CAAxisDef AxisDef(string axisKey)
        {
            for (int i = 0; i < Axes.Length; i++)
                if (Axes[i].Key == axisKey) return Axes[i];
            return null;
        }

        // ---- keyed state access ----------------------------------------

        internal static List<CAAxisEntry> EntriesOf(List<CAAxisEntry> axes,
            string axisKey)
        {
            return (axes ?? new List<CAAxisEntry>())
                .Where(entry => entry != null && entry.axisKey == axisKey
                    && entry.source != (byte)CAAxisSource.Unset
                    && !entry.optionKey.NullOrEmpty())
                .GroupBy(entry => entry.optionKey, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(entry =>
                    entry.source).First()).ToList();
        }

        internal static CAAxisSource StateOf(List<CAAxisEntry> axes,
            string axisKey)
        {
            List<CAAxisEntry> entries = EntriesOf(axes, axisKey);
            if (entries.Count == 0) return CAAxisSource.Unset;
            return entries.Any(entry => entry.source
                    == (byte)CAAxisSource.Authored)
                ? CAAxisSource.Authored : CAAxisSource.Generated;
        }

        internal static IReadOnlyList<string> KeysOf(List<CAAxisEntry> axes,
            string axisKey)
        {
            CAAxisDef def = AxisDef(axisKey);
            var keys = new HashSet<string>(EntriesOf(axes, axisKey)
                .Select(entry => entry.optionKey), StringComparer.Ordinal);
            return def == null ? keys.OrderBy(value => value,
                    StringComparer.Ordinal).ToList()
                : def.Options.Where(option => keys.Contains(option.Key))
                    .Select(option => option.Key).ToList();
        }

        internal static IReadOnlyList<CAAxisOption> OptionsOf(
            List<CAAxisEntry> axes, string axisKey)
        {
            CAAxisDef def = AxisDef(axisKey);
            if (def == null) return new List<CAAxisOption>();
            var keys = new HashSet<string>(KeysOf(axes, axisKey),
                StringComparer.Ordinal);
            return def.Options.Where(option => keys.Contains(option.Key))
                .ToList();
        }

        internal static bool HasOption(List<CAAxisEntry> axes,
            string axisKey, string optionKey)
        {
            return EntriesOf(axes, axisKey).Any(entry =>
                entry.optionKey == optionKey);
        }

        internal static void Set(List<CAAxisEntry> axes, string axisKey,
            string optionKey, CAAxisSource source)
        {
            if (axes == null) return;
            axes.RemoveAll(entry => entry != null
                && entry.axisKey == axisKey);
            Add(axes, axisKey, optionKey, source);
        }

        internal static void Add(List<CAAxisEntry> axes, string axisKey,
            string optionKey, CAAxisSource source)
        {
            if (axes == null || AxisDef(axisKey)?.Options.All(option =>
                    option.Key != optionKey) != false) return;
            if (IsAbsenceOption(axisKey, optionKey))
                axes.RemoveAll(entry => entry != null
                    && entry.axisKey == axisKey);
            else
                axes.RemoveAll(entry => entry != null
                    && entry.axisKey == axisKey
                    && IsAbsenceOption(axisKey, entry.optionKey));
            CAAxisEntry existing = axes.FirstOrDefault(entry => entry != null
                && entry.axisKey == axisKey
                && entry.optionKey == optionKey);
            if (existing == null)
                axes.Add(new CAAxisEntry
                {
                    axisKey = axisKey,
                    optionKey = optionKey,
                    source = (byte)source
                });
            else if (source == CAAxisSource.Authored)
                existing.source = (byte)source;
        }

        internal static void Remove(List<CAAxisEntry> axes, string axisKey,
            string optionKey)
        {
            axes?.RemoveAll(entry => entry != null
                && entry.axisKey == axisKey
                && entry.optionKey == optionKey);
        }

        private static bool IsAbsenceOption(string axisKey,
            string optionKey)
        {
            return optionKey == "none" && (axisKey == Leadership
                || axisKey == LocalOrder || axisKey == Defense);
        }

        internal static void Release(List<CAAxisEntry> axes,
            string axisKey)
        {
            axes?.RemoveAll(entry => entry != null
                && entry.axisKey == axisKey);
        }

        internal static int CountByState(List<CAAxisEntry> axes,
            CAAxisSource state)
        {
            int count = 0;
            for (int i = 0; i < Axes.Length; i++)
                if (StateOf(axes, Axes[i].Key) == state) count++;
            return count;
        }

        // ---- generation ------------------------------------------------
        //
        // Current structure has one generator. This entry point establishes
        // Culture and Political Order first, then delegates every unset
        // structure field to that generator. It never bypasses an explicitly
        // incomplete institutional state.
        internal static int Derive(CARegionalPlan plan,
            CARegionalFactionPlan group)
        {
            if (group == null) return 0;
            group.EnsureCultureAndPolitics(plan);
            if (group.institutionalStateIncomplete) return 0;
            int generated = CAFactionStructureModel.PreserveEstablishedUnset(
                group.factionStructure);
            if (Axes.Any(axis => StateOf(group.factionStructure, axis.Key)
                    == CAAxisSource.Unset))
                group.institutionalStateIncomplete = true;
            return generated;
        }

        internal static void Author(CARegionalFactionPlan group,
            string axisKey, string optionKey)
        {
            Set(group.factionStructure, axisKey, optionKey,
                CAAxisSource.Authored);
        }

        internal static string Characterize(CARegionalPlan plan,
            CARegionalFactionPlan group)
        {
            int specified = Axes.Length
                - CountByState(group.factionStructure, CAAxisSource.Unset);
            if (specified == 0) return "No established institutions represented";
            var parts = new List<string>();
            foreach (string axisKey in new[] { Leadership, Ownership, Defense })
            {
                IReadOnlyList<CAAxisOption> mechanisms = OptionsOf(
                    group.factionStructure, axisKey);
                if (mechanisms.Count > 0)
                    parts.Add(string.Join(" + ", mechanisms.Select(option =>
                        option.Label)));
            }

            CASettlementAuthority authority =
                CARegionalSettlements.SettlementAuthorityOf(plan, group);
            int held = plan.settlements.Count(b => b != null
                && b.factionKey == group.key);
            if (held > 1)
                parts.Add(CARegionalSettlements.SettlementAuthorityWords(authority)
                    + " between settlements");
            if (specified < Axes.Length)
                parts.Add((Axes.Length - specified) + " fields unset");

            string joined = string.Join(", ", parts.ToArray());
            return joined.NullOrEmpty() ? "No established institutions represented"
                : joined.CapitalizeFirst();
        }

        // Political commitments and represented-institution differences are
        // preserved.
        internal static List<string> Conflicts(
            CARegionalFactionPlan group)
        {
            return CAFactionStructureModel.Tensions(
                group?.politicalBeliefs, group?.factionStructure);
        }
    }
}
