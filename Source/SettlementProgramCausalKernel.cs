using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    // Pure authority for which starting programs an established settlement
    // has and which functional asset roles each program must satisfy. Runtime
    // authoring and the governed fixture translator both call this kernel.
    // Loaded-def validation remains a runtime responsibility; applicability,
    // counts, scopes, candidate order, and signatures do not have a second
    // implementation.
    public sealed class CASettlementProgramProvisionFact
    {
        public string OperatorKind;
        public int Nodes = 1;
    }

    public sealed class CASettlementProgramFacts
    {
        public string CandidateId;
        public int Slot;
        public int TileId;
        public int FactionKey;
        public string PopulationOrigin;
        public int Population;
        public int Land;
        public int Access;
        public int Services;
        public int Civic;
        public int Economy;
        public int Trade;
        public int Specialization;
        public int History;
        public int Role;
        public int Scale;
        public int Operations;
        public int TechnologyTier;
        public string Technology;
        public string Axes;
        public string Beliefs;
        public string Culture;
        public bool IdeoligionPlanned;
        public string Provisions;
        public string Relations;
        public string DefenseRule;
        public string LocalOrderRule;
        public bool HostileRelation;
        public bool PublicGatheringMeaning;
        public bool HasRoad;
        public bool HasRiver;
        public bool HasCoast;
        public List<CASettlementProgramProvisionFact> ProvisionFacts =
            new List<CASettlementProgramProvisionFact>();
    }

    public sealed class CASettlementProgramCausalSpec
    {
        public string Key;
        public string Scope = "settlement";
        public List<string[]> CandidateGroups = new List<string[]>();
        public int Count = 1;
        public int Extent = 1;
        public bool NativeSpatialContract;
        public bool MaterializeSpatialContract;
    }

    public static class CASettlementProgramCausalKernel
    {
        public const string Housing = "ca.settlement.housing";
        public const string FoodPreparation =
            "ca.settlement.food-preparation";
        public const string Storage = "ca.settlement.storage";
        public const string Medicine = "ca.settlement.medicine";
        public const string Production = "ca.settlement.production";
        public const string SpecializedIndustry =
            "ca.settlement.specialized-industry";
        public const string Trade = "ca.settlement.trade";
        public const string Governance = "ca.settlement.governance";
        public const string Custody = "ca.settlement.custody";
        public const string Defense = "ca.settlement.defense";
        public const string Research = "ca.settlement.research";
        public const string Religion = "ca.settlement.religion";
        public const string Gathering = "ca.settlement.gathering";
        public const string Recreation = "ca.settlement.recreation";
        public const string ArtAndMemory = "ca.settlement.art-memory";
        public const string Agriculture = "ca.settlement.agriculture";
        public const string Animals = "ca.settlement.animals";
        public const string Communications =
            "ca.settlement.communications";
        public const string Transport = "ca.settlement.transport";
        public const string CommunalProvision =
            "ca.settlement.provision.communal";
        public const string AuthorityProvision =
            "ca.settlement.provision.authority";
        public const string HouseholdProvision =
            "ca.settlement.provision.household";

        public static List<CASettlementProgramCausalSpec> Derive(
            CASettlementProgramFacts facts)
        {
            var result = new List<CASettlementProgramCausalSpec>();
            if (facts == null) return result;
            bool populated = facts.Population > 0;
            bool tier1 = facts.TechnologyTier >= 1;
            bool tier2 = facts.TechnologyTier >= 2;

            void Add(string key, IEnumerable<string[]> groups = null,
                int count = 1, int extent = 1,
                string scope = "settlement", bool spatial = false,
                bool materializeSpatial = false)
            {
                result.Add(new CASettlementProgramCausalSpec
                {
                    Key = key,
                    Scope = scope,
                    CandidateGroups = (groups ?? Enumerable.Empty<string[]>())
                        .Select(group => (group ?? new string[0]).ToArray())
                        .ToList(),
                    Count = Math.Max(1, count),
                    Extent = Math.Max(1, extent),
                    NativeSpatialContract = spatial,
                    MaterializeSpatialContract = materializeSpatial
                });
            }

            if (populated)
            {
                Add(Housing, new[] { tier1
                    ? new[] { "Bed", "Bedroll" }
                    : new[] { "Bedroll", "Bed" } },
                    Clamp(2 + facts.Population / 350, 2, 6));
                Add(FoodPreparation, new[]
                {
                    tier1 ? new[] { "FueledStove", "Campfire" }
                        : new[] { "Campfire", "FueledStove" },
                    new[] { "TableButcher" }
                });
            }
            if (populated && (facts.Access > 0 || facts.Services > 0
                    || facts.ProvisionFacts.Count > 0))
                Add(Storage, new[] { new[] { "Shelf" } },
                    Clamp(1 + facts.Population / 450, 1, 3));
            if (populated && (facts.Services > 0 || facts.History > 0))
                Add(Medicine, new[] { tier2
                    ? new[] { "HospitalBed", "Bed", "Bedroll" }
                    : tier1 ? new[] { "Bed", "Bedroll" }
                    : new[] { "Bedroll", "Bed" } },
                    facts.Services >= 2 ? 2 : 1);
            if (populated && (facts.Economy > 0
                    || facts.Specialization > 0 || facts.History > 0))
                Add(Production, new[] { tier1
                    ? new[] { "FueledSmithy", "CraftingSpot" }
                    : new[] { "CraftingSpot", "FueledSmithy" } });
            if (facts.Specialization >= 2 && facts.Economy >= 2)
                Add(SpecializedIndustry, new[] { tier2
                    ? new[] { "ElectricSmithy", "HandTailoringBench",
                        "FueledSmithy" }
                    : new[] { "HandTailoringBench", "FueledSmithy" } });
            if (tier2 && facts.Trade >= 2 && facts.Economy >= 1)
                Add(Trade, new[]
                {
                    new[] { "WoodFiredGenerator" },
                    new[] { "CommsConsole" },
                    new[] { "OrbitalTradeBeacon" }
                });
            if (populated && (facts.Civic > 0 || facts.Role == 1))
                Add(Governance, new[]
                {
                    new[] { "Table2x2c", "Table1x2c" },
                    tier2
                        ? new[] { "Anon2CushionedChair", "DiningChair",
                            "Stool" }
                        : new[] { "Stool", "DiningChair" }
                });
            if (facts.Civic >= 2 && (facts.LocalOrderRule == "constabulary"
                    || facts.LocalOrderRule == "rulers"))
                Add(Custody, new[] { tier1
                    ? new[] { "Bed", "Bedroll" }
                    : new[] { "Bedroll", "Bed" } });
            if (populated && (facts.HostileRelation
                    || facts.DefenseRule == "standing" || facts.Role == 1))
                Add(Defense, new[] { new[] { "Barricade", "Sandbags" } },
                    extent: Clamp(2 + facts.Population / 300, 2, 6),
                    scope: "settlement perimeter");
            if (tier2 && facts.Services >= 2 && facts.Civic >= 2
                    && facts.Economy >= 2
                    && (facts.History >= 2 || facts.Role == 1))
                Add(Research, new[] { new[] { "SimpleResearchBench" } });
            if (populated && facts.IdeoligionPlanned)
                Add(Religion, new[] { new[] { "RitualSpot", "Ideogram" } });
            bool nonHouseholdProvision = facts.ProvisionFacts.Any(item =>
                item != null && item.OperatorKind != "Household");
            if (populated && (facts.Services > 0 || nonHouseholdProvision
                    || facts.PublicGatheringMeaning))
                Add(Gathering, new[]
                {
                    new[] { "Table2x2c", "Table1x2c" },
                    tier2
                        ? new[] { "Anon2CushionedChair", "DiningChair",
                            "Stool" }
                        : new[] { "Stool", "DiningChair" }
                });
            if (populated && (facts.Services > 0 || facts.History > 0))
                Add(Recreation, new[] { tier2
                    ? new[] { "ChessTable", "GameOfUrBoard",
                        "HorseshoesPin" }
                    : tier1 ? new[] { "GameOfUrBoard", "HorseshoesPin" }
                    : new[] { "HorseshoesPin" } },
                    scope: "usable settlement ground");
            if (facts.History >= 2 || facts.Civic >= 2 || facts.Role == 1)
                // The ported bust satisfies the same native art contract as
                // vanilla sculpture and proves that compatible content enters
                // through capability, not a separate facility list.
                Add(ArtAndMemory, new[] { new[] { "DankPyon_Bust" } });
            if (populated && facts.Land >= 2)
                Add(Agriculture, count: Clamp(1 + facts.Land, 2, 4),
                    spatial: true, materializeSpatial: true,
                    scope: "workable settlement ground");
            if (populated && facts.Land >= 2
                    && (facts.Specialization > 0 || facts.History > 0))
                Add(Animals, new[] { tier1
                    ? new[] { "AnimalBed", "AnimalSleepingBox" }
                    : new[] { "AnimalSleepingBox", "AnimalBed" } },
                    facts.Land >= 3 ? 2 : 1,
                    scope: "usable settlement ground");
            if (tier2 && facts.Access >= 2
                    && (facts.Civic >= 2 || facts.Role == 1))
                Add(Communications, new[]
                {
                    new[] { "WoodFiredGenerator" },
                    new[] { "CommsConsole" }
                });
            if (facts.HasRoad || facts.HasRiver || facts.HasCoast)
                Add(Transport, scope: "settlement area", spatial: true);

            AddProvision(CommunalProvision, "Communal",
                new[] { "FueledStove", "Campfire" }, true);
            AddProvision(AuthorityProvision, "Authority",
                new[] { "Shelf" }, false);
            AddProvision(HouseholdProvision, "Household",
                new[] { "FueledStove", "Campfire" }, false);
            return result;

            void AddProvision(string key, string kind, string[] primary,
                bool dining)
            {
                List<CASettlementProgramProvisionFact> provisions = facts
                    .ProvisionFacts.Where(item => item != null
                        && item.OperatorKind == kind).ToList();
                if (provisions.Count == 0) return;
                var roles = new List<string[]> { primary, new[] { "Shelf" } };
                if (dining)
                {
                    roles.Add(new[] { "Table2x2c", "Table1x2c" });
                    roles.Add(tier2
                        ? new[] { "Anon2CushionedChair", "DiningChair",
                            "Stool" }
                        : new[] { "Stool", "DiningChair" });
                }
                Add(key, roles, provisions.Sum(item =>
                    Math.Max(1, item.Nodes)), scope: "saved provision nodes");
            }
        }

        public static string SourceFacts(CASettlementProgramFacts facts)
        {
            if (facts == null) return "missing";
            return (facts.CandidateId ?? "ca") + "|slot=" + facts.Slot
                + "|tile=" + facts.TileId + "|faction=" + facts.FactionKey
                + "|origin=" + (facts.PopulationOrigin ?? "Unset")
                + "|pop=" + facts.Population + "|land=" + facts.Land
                + "|access=" + facts.Access + "|services=" + facts.Services
                + "|civic=" + facts.Civic + "|economy=" + facts.Economy
                + "|trade=" + facts.Trade + "|specialization="
                + facts.Specialization + "|history=" + facts.History
                + "|role=" + facts.Role + "|scale=" + facts.Scale
                + "|operations=" + facts.Operations + "|tech="
                + (facts.Technology ?? "none") + "|axes="
                + (facts.Axes ?? "") + "|beliefs="
                + (facts.Beliefs ?? "") + "|culture="
                + (facts.Culture ?? "unrecorded") + "|ideo="
                + (facts.IdeoligionPlanned ? "planned" : "none")
                + "|routes=" + (facts.HasRoad ? "road" : "") + ","
                + (facts.HasRiver ? "river" : "") + ","
                + (facts.HasCoast ? "coast" : "") + "|provisions="
                + (facts.Provisions ?? "") + "|relations="
                + (facts.Relations ?? "");
        }

        public static string SourceSignature(CASettlementProgramFacts facts)
        {
            return unchecked((uint)StableStringHash(SourceFacts(facts)))
                .ToString("X8");
        }

        public static int StableCandidateIndex(string candidateId, int slot,
            string key, int groupIndex, int count)
        {
            if (count <= 1) return 0;
            int hash = StableStringHash((candidateId ?? "ca") + ":" + slot
                + ":" + (key ?? "program") + ":" + groupIndex)
                & int.MaxValue;
            return hash % count;
        }

        public static string EntrySignature(string source, string key,
            string scope, int count, int extent,
            IEnumerable<string> selectedCandidates, string blocker)
        {
            string facts = (source ?? "") + "|" + (key ?? "") + "|"
                + (scope ?? "") + "|" + count + "|" + extent + "|"
                + string.Join(",", selectedCandidates
                    ?? Enumerable.Empty<string>()) + "|" + (blocker ?? "");
            return unchecked((uint)StableStringHash(facts)).ToString("X8");
        }

        // Matches Verse.GenText.StableStringHash without a Verse dependency.
        public static int StableStringHash(string value)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in value ?? "") hash = hash * 31 + c;
                return hash;
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return value < minimum ? minimum
                : value > maximum ? maximum : value;
        }
    }
}
