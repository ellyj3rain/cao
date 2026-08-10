using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Questions shared by political beliefs and faction structure. Each
    // answer is keyed and independently authored or generated.
    // Leadership, decisions, participation, dissent, ownership, economy, work,
    // support, membership, status, local order, defense, and war conduct remain
    // separate because they produce different game state.
    public enum CAAxisSource : byte
    {
        Unset = 0,
        Generated = 1,
        Authored = 2,
        Preset = 3,
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
                Question = "Faction leadership.",
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
                Question = "Faction-wide decision method.",
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
                Question = "Participation in faction decisions.",
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
                Question = "Treatment of political dissent.",
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
                Question = "Ownership of farms and workshops.",
                Options = new[]
                {
                    new CAAxisOption("private", "private owners",
                        "individuals own farms and workshops"),
                    new CAAxisOption("cooperative", "cooperatives",
                        "workers and communities own them together"),
                    new CAAxisOption("common", "shared ownership",
                        "productive property is held in common"),
                    new CAAxisOption("state", "faction ownership",
                        "the faction owns farms and workshops"),
                    new CAAxisOption("mixed", "mixed ownership",
                        "private, cooperative, and shared ownership coexist")
                }
            },
            new CAAxisDef
            {
                Key = Economy, Label = "Economy",
                Question = "Distribution of goods and work.",
                Options = new[]
                {
                    new CAAxisOption("market", "trade",
                        "prices and bargains distribute goods"),
                    new CAAxisOption("planned", "planned distribution",
                        "leaders allocate goods and work"),
                    new CAAxisOption("communal", "shared stores",
                        "goods are pooled and shared"),
                    new CAAxisOption("mixed", "mixed economy",
                        "trade, planning, and shared stores coexist")
                }
            },
            new CAAxisDef
            {
                Key = Work, Label = "Work",
                Question = "Assignment of work.",
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
                Question = "Food, shelter, and care during hardship.",
                Options = new[]
                {
                    new CAAxisOption("private", "self-provided",
                        "each household provides for itself"),
                    new CAAxisOption("public", "faction support",
                        "the faction supplies basic needs"),
                    new CAAxisOption("communal", "shared stores",
                        "common stores supply basic needs"),
                    new CAAxisOption("charitable", "charity",
                        "religious and voluntary groups provide support"),
                    new CAAxisOption("mixed", "mixed support",
                        "several systems operate together")
                }
            },
            new CAAxisDef
            {
                Key = Membership, Label = "Membership",
                Question = "Requirements for faction membership.",
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
                Question = "Source of social rank.",
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
                Question = "Enforcement inside settlements.",
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
                Question = "Faction defense.",
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
                Question = "Treatment of defeated enemies and noncombatants.",
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

        internal static CAAxisEntry EntryOf(List<CAAxisEntry> axes,
            string axisKey)
        {
            if (axes == null) return null;
            for (int i = 0; i < axes.Count; i++)
                if (axes[i] != null && axes[i].axisKey == axisKey)
                    return axes[i];
            return null;
        }

        internal static CAAxisSource StateOf(List<CAAxisEntry> axes,
            string axisKey)
        {
            CAAxisEntry entry = EntryOf(axes, axisKey);
            return entry == null ? CAAxisSource.Unset
                : (CAAxisSource)entry.source;
        }

        internal static string KeyOf(List<CAAxisEntry> axes,
            string axisKey)
        {
            CAAxisEntry entry = EntryOf(axes, axisKey);
            return entry == null
                || entry.source == (byte)CAAxisSource.Unset
                ? null : entry.optionKey;
        }

        internal static CAAxisOption OptionOf(List<CAAxisEntry> axes,
            string axisKey)
        {
            string key = KeyOf(axes, axisKey);
            if (key == null) return null;
            CAAxisDef def = AxisDef(axisKey);
            return def?.Options.FirstOrDefault(o => o.Key == key);
        }

        internal static void Set(List<CAAxisEntry> axes, string axisKey,
            string optionKey, CAAxisSource source)
        {
            CAAxisEntry entry = EntryOf(axes, axisKey);
            if (entry == null)
            {
                entry = new CAAxisEntry { axisKey = axisKey };
                axes.Add(entry);
            }
            entry.optionKey = optionKey;
            entry.source = (byte)source;
        }

        internal static void Release(List<CAAxisEntry> axes,
            string axisKey)
        {
            CAAxisEntry entry = EntryOf(axes, axisKey);
            if (entry != null)
                entry.source = (byte)CAAxisSource.Unset;
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
        // Each generated field uses only related facts. Status does not decide
        // dissent or membership; each field rolls independently.
        internal static int Derive(CARegionalPlan plan,
            CARegionalFactionPlan group)
        {
            group?.EnsureCultureAndPolitics(plan);
            int tier = TechTier(group);
            bool conflict = plan?.relations != null
                && plan.relations.Any(relation => relation != null
                    && relation.relation == FactionRelationKind.Hostile
                    && (relation.leftFactionKey == group.key
                        || relation.rightFactionKey == group.key));
            int filled = 0;
            Rand.PushState(Gen.HashCombineInt(GenText.StableStringHash(
                (plan?.candidateId ?? "ca") + ":faction-structure"),
                group.key * 397));
            try
            {
                string Lean(string axisKey, string generated)
                {
                    string ideal = KeyOf(group?.politicalBeliefs?.positions,
                        axisKey);
                    // Political beliefs influence generated faction structure
                    // without forcing it to match.
                    return ideal != null && Rand.Chance(0.76f)
                        ? ideal : generated;
                }

                filled += Fill(group, Leadership, Lean(Leadership,
                    Pick("council", "whole", 0.5f)));
                filled += Fill(group, Decisions, Lean(Decisions,
                    KeyOf(group.factionStructure, Leadership) == "single"
                        ? Pick("decree", "majority", 0.7f)
                        : KeyOf(group.factionStructure, Leadership) == "whole"
                            ? Pick("consensus", "majority", 0.6f)
                            : Pick("majority", "custom", 0.6f)));
                filled += Fill(group, Participation, Lean(Participation,
                    Pick("universal", "members", 0.55f)));
                filled += Fill(group, Dissent, Lean(Dissent,
                    KeyOf(group.factionStructure, Participation) == "universal"
                        ? Pick("plural", "majoritarian", 0.55f)
                        : Pick("customary", "majoritarian", 0.55f)));
                filled += Fill(group, Ownership, Lean(Ownership,
                    Pick("mixed", "private", 0.5f)));
                filled += Fill(group, Economy, Lean(Economy,
                    Pick("market", "mixed", 0.7f)));
                filled += Fill(group, Work, Lean(Work,
                    Pick("contract", "household", 0.7f)));
                filled += Fill(group, Support, Lean(Support,
                    Pick("mixed", "private", 0.55f)));
                filled += Fill(group, Membership,
                    Lean(Membership, Pick("open", "vetted",
                        tier == 0 ? 0.5f : 0.65f)));
                filled += Fill(group, Status,
                    Lean(Status, tier == 0
                        ? Pick("earned", "equal", 0.55f)
                        : Pick("earned", "hereditary", 0.6f)));
                filled += Fill(group, LocalOrder, Lean(LocalOrder,
                    tier >= 1
                        ? Pick("constabulary", "watch", 0.55f)
                        : Pick("watch", "rulers", 0.7f)));
                filled += Fill(group, Defense, Lean(Defense,
                    conflict
                        ? Pick("militia", "professional",
                            tier >= 1 ? 0.5f : 0.8f)
                        : Pick("levy", "militia", 0.6f)));
                filled += Fill(group, WarConduct, Lean(WarConduct,
                    Pick("quarter", "strength", 0.6f)));
            }
            finally { Rand.PopState(); }
            return filled;
        }

        private static int TechTier(CARegionalFactionPlan group)
        {
            TechLevel tech = group.ResolvedFactionDef?.techLevel
                ?? TechLevel.Neolithic;
            return (int)tech >= (int)TechLevel.Industrial ? 2
                : (int)tech == (int)TechLevel.Medieval ? 1 : 0;
        }

        private static string Pick(string a, string b, float chanceA)
        {
            return Rand.Chance(chanceA) ? a : b;
        }

        private static int Fill(CARegionalFactionPlan group,
            string axisKey, string optionKey)
        {
            if (StateOf(group.factionStructure, axisKey)
                != CAAxisSource.Unset) return 0;
            CAAxisDef def = AxisDef(axisKey);
            if (def == null
                || !def.Options.Any(o => o.Key == optionKey)) return 0;
            Set(group.factionStructure, axisKey, optionKey,
                CAAxisSource.Generated);
            return 1;
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
            if (specified == 0) return "Faction structure not set";
            var parts = new List<string>();
            CAAxisOption leadership = OptionOf(group.factionStructure, Leadership);
            CAAxisOption ownership = OptionOf(group.factionStructure, Ownership);
            CAAxisOption defense = OptionOf(group.factionStructure, Defense);
            if (leadership != null) parts.Add(leadership.Label);
            if (ownership != null) parts.Add(ownership.Label);
            if (defense != null) parts.Add(defense.Label);

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
            return joined.NullOrEmpty() ? "Faction structure not set"
                : joined.CapitalizeFirst();
        }

        // Presets fill only the fields they define. Variants inherit a base
        // preset and replace individual answers. Explicit answers survive.
        internal sealed class PoliticalPreset
        {
            internal string Name;
            internal string Parent;
            internal Dictionary<string, string> Positions =
                new Dictionary<string, string>();
        }

        internal static readonly PoliticalPreset[] Presets =
        {
            new PoliticalPreset
            {
                Name = "Elected council and public support",
                Positions = new Dictionary<string, string>
                {
                    { Leadership, "council" }, { Decisions, "majority" },
                    { Participation, "universal" }, { Dissent, "plural" },
                    { Ownership, "mixed" }, { Economy, "market" },
                    { Work, "organized" }, { Support, "public" },
                    { Membership, "open" }
                },
            },
            new PoliticalPreset
            {
                Name = "Elected council and worker ownership",
                Parent = "Elected council and public support",
                Positions = new Dictionary<string, string>
                {
                    { Ownership, "cooperative" },
                    { Economy, "mixed" }
                },
            },
            new PoliticalPreset
            {
                Name = "Worker councils",
                Positions = new Dictionary<string, string>
                {
                    { Leadership, "federated" }, { Decisions, "majority" },
                    { Participation, "members" }, { Dissent, "plural" },
                    { Ownership, "cooperative" },
                    { Economy, "mixed" }, { Work, "organized" },
                    { Support, "communal" }
                }
            },
            new PoliticalPreset
            {
                Name = "Common ownership",
                Positions = new Dictionary<string, string>
                {
                    { Ownership, "common" }, { Economy, "planned" },
                    { Work, "duty" }, { Support, "communal" }
                },
            },
            new PoliticalPreset
            {
                Name = "Independent communes",
                Parent = "Common ownership",
                Positions = new Dictionary<string, string>
                {
                    { Leadership, "federated" }, { Decisions, "majority" },
                    { Participation, "members" }, { Economy, "communal" }
                },
            },
            new PoliticalPreset
            {
                Name = "Central party rule",
                Parent = "Common ownership",
                Positions = new Dictionary<string, string>
                {
                    { Leadership, "single" }, { Decisions, "decree" },
                    { Participation, "members" }, { Dissent, "orthodoxy" },
                    { LocalOrder, "constabulary" }
                }
            },
            new PoliticalPreset
            {
                Name = "Elected council and private trade",
                Positions = new Dictionary<string, string>
                {
                    { Leadership, "council" }, { Decisions, "majority" },
                    { Participation, "universal" }, { Dissent, "plural" },
                    { Ownership, "private" }, { Economy, "market" },
                    { Work, "contract" }, { Membership, "open" }
                },
            },
            new PoliticalPreset
            {
                Name = "Dictatorial state",
                Positions = new Dictionary<string, string>
                {
                    { Leadership, "single" }, { Decisions, "decree" },
                    { Participation, "members" }, { Dissent, "orthodoxy" },
                    { Ownership, "mixed" }, { Economy, "planned" },
                    { Work, "duty" }, { Membership, "closed" },
                    { Status, "earned" },
                    { LocalOrder, "rulers" }, { Defense, "professional" }
                }
            },
            new PoliticalPreset
            {
                Name = "No central rule",
                Positions = new Dictionary<string, string>
                {
                    { Leadership, "none" }, { Dissent, "plural" },
                    { Membership, "open" },
                    { Status, "equal" }, { LocalOrder, "none" }
                }
            },
            new PoliticalPreset
            {
                Name = "Common stores, no rulers",
                Parent = "No central rule",
                Positions = new Dictionary<string, string>
                {
                    { Decisions, "consensus" }, { Participation, "universal" },
                    { Ownership, "common" }, { Economy, "communal" },
                    { Support, "communal" }, { Defense, "levy" }
                },
            },
            new PoliticalPreset
            {
                Name = "Private trade, no rulers",
                Parent = "No central rule",
                Positions = new Dictionary<string, string>
                {
                    { Decisions, "custom" }, { Participation, "heads" },
                    { Dissent, "customary" }, { Ownership, "private" },
                    { Economy, "market" }, { Work, "contract" },
                    { Support, "private" }, { LocalOrder, "watch" }
                }
            },
            new PoliticalPreset
            {
                Name = "Hereditary ruler",
                Positions = new Dictionary<string, string>
                {
                    { Leadership, "single" }, { Participation, "standing" },
                    { Dissent, "customary" },
                    { Status, "hereditary" },
                    { LocalOrder, "rulers" }
                },
            },
            new PoliticalPreset
            {
                Name = "Landed houses",
                Parent = "Hereditary ruler",
                Positions = new Dictionary<string, string>
                {
                    { Decisions, "custom" },
                    { Membership, "hereditary" },
                    { Work, "duty" }, { Defense, "levy" },
                    { Support, "charitable" }
                }
            },
            new PoliticalPreset
            {
                Name = "Absolute ruler",
                Parent = "Hereditary ruler",
                Positions = new Dictionary<string, string>
                {
                    { Decisions, "decree" },
                    { Economy, "planned" },
                    { Defense, "professional" }
                }
            }
        };

        // Political-belief and current-structure differences are preserved.
        internal static List<string> Conflicts(
            CARegionalFactionPlan group)
        {
            return CAFactionStructureModel.Tensions(
                group?.politicalBeliefs, group?.factionStructure);
        }
    }
}
