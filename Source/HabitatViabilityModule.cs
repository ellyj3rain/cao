using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Runtime bridge for the pure habitat kernel. It reads existing factual
    // owners only: world terrain, saved settlement programs, and the faction's
    // authored technological knowledge. It does not create a program, culture,
    // institution, or settlement merely because the environment needs one.
    internal static class CAHabitatViability
    {
        internal static CAHabitatRequirementProfile Requirements(
            CASettlementEnvironmentFacts facts)
        {
            return new CAHabitatRequirementProfile
            {
                RequirementMask = facts?.HabitatRequirementMask ?? 0,
                RequiredCapabilityTier = facts?.RequiredCapabilityTier ?? 0,
                FoodRoute = facts?.FoodRoute
                    ?? CAHabitatFoodRoute.OutdoorCultivation
            };
        }

        internal static int KnowledgeCompatibilityTier(
            CARegionalFactionPlan faction)
        {
            return CATechnologicalKnowledgeModel.CompatibilityTier(
                faction?.technologicalKnowledge);
        }

        internal static int KnowledgeCompatibilityTier(Faction faction)
        {
            return CATechnologicalKnowledgeRuntime
                .CanonicalCompatibilityTier(faction);
        }

        // Placement may ask whether a population could establish the required
        // functions before those functions have been authored. Confirmation
        // separately evaluates the settlement's actual saved programs.
        internal static CAHabitatViabilityResult FrontierPotential(
            CASettlementEnvironmentFacts facts, int technologyTier)
        {
            return CAHabitatViabilityCausalKernel.Evaluate(
                Requirements(facts),
                CAHabitatViabilityCausalKernel.FrontierCapability(
                    technologyTier));
        }

        internal static CAHabitatViabilityResult SettlementPotential(
            CASettlementEnvironmentFacts facts, int technologyTier)
        {
            return CAHabitatViabilityCausalKernel.Evaluate(
                Requirements(facts),
                CAHabitatViabilityCausalKernel
                    .SettlementPotentialCapability(technologyTier));
        }

        internal static CAHabitatViabilityResult FrontierPotential(
            CASettlementEnvironmentFacts facts,
            CARegionalFactionPlan supporter)
        {
            CAHabitatViabilityResult result = FrontierPotential(facts,
                supporter == null ? 0
                    : KnowledgeCompatibilityTier(supporter));
            if (result.Viable && !SatisfiesKnowledge(facts,
                    supporter?.technologicalKnowledge
                        ?? BaselineFrontierKnowledge(),
                    false, null, out _))
                result.Viable = false;
            return result;
        }

        internal static CAHabitatViabilityResult FrontierPotential(
            CASettlementEnvironmentFacts facts, Faction supporter)
        {
            CAHabitatViabilityResult result = FrontierPotential(facts,
                supporter == null ? 0
                    : KnowledgeCompatibilityTier(supporter));
            CATechnologicalKnowledge knowledge = supporter == null
                ? BaselineFrontierKnowledge()
                : CATechnologicalKnowledgeRuntime.ForFaction(supporter);
            if (result.Viable && !SatisfiesKnowledge(facts, knowledge,
                    false, null, out _))
                result.Viable = false;
            return result;
        }

        internal static bool CanPotentiallySettle(CARegionalPlan plan,
            int factionKey, int tileId, out string failure)
        {
            failure = null;
            CASettlementEnvironmentFacts facts = CASettlementEnvironment
                .ForTile(tileId);
            if (!facts.Valid)
            {
                failure = "this ground cannot hold a settlement";
                return false;
            }
            CARegionalFactionPlan faction = plan?.FactionPlan(factionKey);
            if (faction?.ResolvedFactionDef == null)
            {
                failure = "choose a settlement-capable faction before assigning this ground";
                return false;
            }
            int tier = KnowledgeCompatibilityTier(faction);
            CAHabitatViabilityResult result = SettlementPotential(facts,
                Math.Max(tier, facts.RequiredCapabilityTier));
            bool knowsHow = SatisfiesKnowledge(facts,
                faction.technologicalKnowledge, false, null,
                out CATechnologyRequirement missingKnowledge);
            if (result.Viable && knowsHow) return true;
            string missing = MissingWords(result.MissingRequirementMask);
            failure = "this population cannot establish a viable habitat "
                + "here: " + (!knowsHow
                    ? MissingKnowledgeWords(missingKnowledge)
                    : "missing " + missing);
            return false;
        }

        internal static bool CanPotentiallySettle(CARegionalPlan plan,
            CARegionalSettlementPlan settlement, int tileId,
            out string failure)
        {
            failure = null;
            CASettlementEnvironmentFacts facts = CASettlementEnvironment
                .ForTile(tileId);
            if (!facts.Valid)
            {
                failure = "this ground cannot hold a settlement";
                return false;
            }
            CARegionalFactionPlan owner = CASiteState.OwnerPlan(plan,
                settlement?.factionLinks);
            FactionDef generationDef = owner?.ResolvedFactionDef
                ?? DefDatabase<FactionDef>.GetNamedSilentFail(
                    settlement?.generationFactionDefName);
            if (generationDef?.humanlikeFaction != true
                || generationDef.pawnGroupMakers == null
                || generationDef.pawnGroupMakers.Count == 0)
            {
                failure = "choose a settlement population before assigning this ground";
                return false;
            }
            CATechnologicalKnowledge knowledge = CASiteState.Knowledge(plan,
                settlement);
            int tier = CATechnologicalKnowledgeModel.CompatibilityTier(
                knowledge);
            CAHabitatViabilityResult result = SettlementPotential(facts,
                Math.Max(tier, facts.RequiredCapabilityTier));
            bool knowsHow = SatisfiesKnowledge(facts, knowledge, false, null,
                out CATechnologyRequirement missingKnowledge);
            if (result.Viable && knowsHow) return true;
            failure = "this population cannot establish a viable habitat "
                + "here: " + (!knowsHow
                    ? MissingKnowledgeWords(missingKnowledge)
                    : "missing " + MissingWords(
                        result.MissingRequirementMask));
            return false;
        }

        internal static CAHabitatViabilityResult EvaluateSettlement(
            CARegionalPlan plan, CARegionalSettlementPlan settlement,
            CASettlementEnvironmentFacts facts)
        {
            CATechnologicalKnowledge knowledge = CASiteState.Knowledge(plan,
                settlement);
            int tier = CATechnologicalKnowledgeModel.CompatibilityTier(
                knowledge);
            HashSet<string> programs = CompletePrograms(plan, settlement);
            bool foodPreparation = programs.Contains(
                CASettlementProgramRegistry.FoodPreparation)
                || programs.Contains(
                    CASettlementProgramRegistry.CommunalProvision)
                || programs.Contains(
                    CASettlementProgramRegistry.AuthorityProvision)
                || programs.Contains(
                    CASettlementProgramRegistry.DomesticProvision);
            bool storage = programs.Contains(
                CASettlementProgramRegistry.Storage)
                || programs.Contains(
                    CASettlementProgramRegistry.AuthorityProvision);
            bool gathering = programs.Contains(
                CASettlementProgramRegistry.Gathering);
            bool agriculture = programs.Contains(
                CASettlementProgramRegistry.Agriculture);
            bool animals = programs.Contains(
                CASettlementProgramRegistry.Animals);
            bool trade = programs.Contains(
                CASettlementProgramRegistry.Trade);
            bool production = programs.Contains(
                CASettlementProgramRegistry.Production)
                || programs.Contains(
                    CASettlementProgramRegistry.SpecializedIndustry);
            bool medicine = programs.Contains(
                CASettlementProgramRegistry.Medicine);
            bool provision = programs.Contains(
                    CASettlementProgramRegistry.CommunalProvision)
                || programs.Contains(
                    CASettlementProgramRegistry.AuthorityProvision)
                || programs.Contains(
                    CASettlementProgramRegistry.DomesticProvision);

            CAHabitatFoodRoute route = RealizedFoodRoute(facts, programs,
                knowledge);
            bool protectedCultivationKnowledge =
                CATechnologicalKnowledgeModel.Rank(
                    knowledge,
                    CATechnologyDomains.Agriculture,
                    CATechnologyCompetencies.Operate) >= 2;
            bool foodSource;
            switch (route)
            {
                case CAHabitatFoodRoute.ForageAndHunt:
                    foodSource = gathering || animals || trade;
                    break;
                case CAHabitatFoodRoute.ProtectedLowLightCultivation:
                case CAHabitatFoodRoute.ProtectedCultivation:
                    foodSource = agriculture && production
                        && protectedCultivationKnowledge;
                    break;
                case CAHabitatFoodRoute.StoredAndSupported:
                    foodSource = trade && storage || provision && storage;
                    break;
                default:
                    foodSource = agriculture || gathering || animals
                        || trade;
                    break;
            }

            var capability = new CAHabitatCapabilityInput
            {
                // The pure kernel retains this compatibility projection for
                // old causal receipts. Exact technological authority is the
                // faction-owned domain gate applied below.
                KnowledgeCompatibilityTier = Math.Max(tier,
                    facts?.RequiredCapabilityTier ?? 0),
                // Established-settlement morphology guarantees a roofed lot;
                // a frontier site has a separate material proof below.
                EnclosedShelter = settlement != null,
                ThermalControl = (production || storage),
                ReliableFood = foodPreparation && foodSource,
                SecuredFoodSupply = agriculture && production
                        && protectedCultivationKnowledge
                    || trade && storage,
                FoodReserve = storage,
                MedicalCare = medicine,
                // CA's loaded distillation recipes make a real campfire or
                // stove a treatment route. Storage is required so potable
                // output is not merely a momentary recipe product.
                WaterTreatment = foodPreparation && storage,
                ArtificialLight = settlement != null,
                HazardProtection = medicine && production,
                BreathableInterior = production
            };
            CAHabitatViabilityResult result =
                CAHabitatViabilityCausalKernel.Evaluate(
                Requirements(facts), capability);
            if (!SatisfiesKnowledge(facts, knowledge,
                    false, null, out _))
                result.Viable = false;
            return result;
        }

        internal static CAHabitatViabilityResult ApplySettlement(
            CARegionalPlan plan, CARegionalSettlementPlan settlement)
        {
            CASettlementEnvironmentFacts facts = CASettlementEnvironment
                .ForTile(settlement?.memberTileId ?? -1);
            CAHabitatViabilityResult result = EvaluateSettlement(plan,
                settlement, facts);
            if (settlement == null) return result;
            settlement.environmentSourceHash = facts.SourceHash;
            settlement.habitatRequirementMask = facts.HabitatRequirementMask;
            settlement.requiredCapabilityTier = facts.RequiredCapabilityTier;
            settlement.habitatCapabilityMask = result.CapabilityMask;
            settlement.missingHabitatRequirementMask =
                result.MissingRequirementMask;
            settlement.habitatFoodRoute = (byte)RealizedFoodRoute(plan,
                settlement, facts);
            settlement.habitatViable = result.Viable;
            SatisfiesKnowledge(facts, CASiteState.Knowledge(plan,
                    settlement),
                false, null, out CATechnologyRequirement missingKnowledge);
            settlement.habitatBlocker = result.Viable ? null
                : result.MissingRequirementMask != 0
                    ? "Missing " + MissingWords(
                        result.MissingRequirementMask)
                    : MissingKnowledgeWords(missingKnowledge);
            return result;
        }

        internal static void ApplyFrontier(CAFrontierHoldingPlan holding,
            CASettlementEnvironmentFacts facts,
            int supportingFactionKey = -1,
            int supportingFactionLoadId = -1,
            CATechnologicalKnowledge initialKnowledge = null)
        {
            if (holding == null || facts == null) return;
            string seed = "frontier:" + holding.memberTileId + ":"
                + holding.key;
            if (holding.siteName.NullOrEmpty())
                holding.siteName = (holding.form == 1 ? "Frontier homestead "
                    : "Frontier cabin ") + (holding.key + 1);
            if (holding.factionLinks == null)
                holding.factionLinks = new CASiteFactionLinks();
            if (supportingFactionKey >= 0)
                holding.factionLinks.SetRegionalSupport(
                    supportingFactionKey);
            else if (supportingFactionLoadId >= 0)
                holding.factionLinks.SetWorldSupport(
                    supportingFactionLoadId);
            else
                holding.factionLinks.SetNoSupport();
            if (holding.localSociety == null)
                holding.localSociety = new CASiteLocalSocietyState
                {
                    explicitLocalDivergence = true,
                    technologicalKnowledge = initialKnowledge?.Copy()
                        ?? BaselineFrontierKnowledge(),
                    politicalOrder = new CAPoliticalBeliefs(),
                    institutions = new List<CAAxisEntry>()
                };
            holding.localSociety.explicitLocalDivergence = true;
            if (holding.localSociety.politicalOrder == null)
                holding.localSociety.politicalOrder =
                    new CAPoliticalBeliefs();
            CAPoliticalBeliefsModel.Ensure(
                holding.localSociety.politicalOrder, seed + ":politics");
            if (holding.localSociety.technologicalKnowledge == null)
                holding.localSociety.technologicalKnowledge =
                    initialKnowledge?.Copy() ?? BaselineFrontierKnowledge();
            CATechnologicalKnowledgeModel.Ensure(
                holding.localSociety.technologicalKnowledge,
                seed + ":technology");
            if (holding.localSociety.institutions == null)
                holding.localSociety.institutions = new List<CAAxisEntry>();
            holding.localSociety.institutionalStateIncomplete = false;
            if (holding.localCulture == null)
                holding.localCulture = new CACulture
                {
                    name = holding.siteName + " culture"
                };
            CACultureModel.EnsureIdentity(holding.localCulture,
                seed + ":culture");
            CACultureAuthoringKernel.CompleteMissing(holding.localCulture,
                seed + ":culture", seed + ":population");
            if (holding.populationGroups == null)
                holding.populationGroups =
                    new List<CASettlementPopulationGroup>();
            if (holding.populationGroups.Count == 0)
                holding.populationGroups.Add(new CASettlementPopulationGroup
                {
                    key = 1,
                    kind = CAPopulationGroupKind.Unaffiliated,
                    isPrimary = true,
                    label = "Local residents",
                    share = 100,
                    factionKey = -1,
                    ideoligionCertainty = 1
                });
            int effectiveTier = CATechnologicalKnowledgeModel
                .CompatibilityTier(
                    holding.localSociety.technologicalKnowledge);
            CAHabitatViabilityResult result = FrontierPotential(facts,
                effectiveTier);
            holding.environmentSourceHash = facts.SourceHash;
            holding.habitatRequirementMask = facts.HabitatRequirementMask;
            holding.requiredCapabilityTier = facts.RequiredCapabilityTier;
            holding.capabilityTier = effectiveTier;
            holding.habitatCapabilityMask = result.CapabilityMask;
            holding.missingHabitatRequirementMask =
                result.MissingRequirementMask;
            holding.habitatFoodRoute = (byte)facts.FoodRoute;
            holding.materializationFailure = result.Viable ? null
                : "Missing " + MissingWords(result.MissingRequirementMask);
        }

        internal static bool ValidateFrontier(CAFrontierHoldingPlan holding,
            CASettlementEnvironmentFacts facts, out string failure,
            CARegionalPlan region = null)
        {
            failure = null;
            if (holding == null || facts == null || !facts.Valid)
            {
                failure = "frontier habitat evidence is unavailable";
                return false;
            }
            if (holding.schemaVersion
                    != CAFrontierHoldingPlan.CurrentSchemaVersion
                || holding.siteName.NullOrEmpty())
            {
                failure = "frontier site identity is incomplete";
                return false;
            }
            string linkFailure = holding.factionLinks?.ValidationFailure(
                region);
            if (!linkFailure.NullOrEmpty())
            {
                failure = linkFailure;
                return false;
            }
            string cultureFailure = CACultureModel.ValidationFailure(
                holding.localCulture, requireSubstantive: true);
            if (!cultureFailure.NullOrEmpty())
            {
                failure = "frontier Culture is invalid: " + cultureFailure;
                return false;
            }
            string politicalFailure = CAPoliticalBeliefsModel
                .ValidationFailure(holding.localSociety?.politicalOrder,
                    allowExactLegacy: false);
            if (!politicalFailure.NullOrEmpty())
            {
                failure = "frontier Political Order is invalid: "
                    + politicalFailure;
                return false;
            }
            string institutionFailure = CAPoliticalBeliefsModel
                .ValidationFailure(holding.localSociety?.institutions);
            if (!institutionFailure.NullOrEmpty())
            {
                failure = "frontier institutions are invalid: "
                    + institutionFailure;
                return false;
            }
            List<CASettlementPopulationGroup> populations = holding
                .populationGroups;
            if (populations == null || populations.Count == 0
                || populations.Any(value => value == null || value.key <= 0
                    || value.share <= 0 || value.share > 100)
                || populations.Select(value => value.key).Distinct().Count()
                    != populations.Count
                || populations.Count(value => value.isPrimary) != 1
                || populations.Sum(value => value.share) != 100)
            {
                failure = "frontier population composition is invalid";
                return false;
            }
            CATechnologicalKnowledge knowledge = holding.localSociety
                ?.technologicalKnowledge;
            if (knowledge == null)
            {
                failure = "frontier habitat has no technological knowledge";
                return false;
            }
            int expectedTier = CATechnologicalKnowledgeModel
                .CompatibilityTier(knowledge);
            CAHabitatViabilityResult expected = FrontierPotential(facts,
                holding.capabilityTier);
            if (holding.environmentSourceHash != facts.SourceHash
                || holding.habitatRequirementMask
                    != facts.HabitatRequirementMask
                || holding.requiredCapabilityTier
                    != facts.RequiredCapabilityTier
                || holding.capabilityTier != expectedTier
                || holding.habitatFoodRoute != (byte)facts.FoodRoute
                || holding.habitatCapabilityMask != expected.CapabilityMask
                || holding.missingHabitatRequirementMask
                    != expected.MissingRequirementMask)
            {
                failure = "the saved frontier habitat result does not match "
                    + "its world-tile facts and supporting capability";
                return false;
            }
            if (!expected.Viable)
            {
                failure = "frontier habitat is missing "
                    + MissingWords(expected.MissingRequirementMask);
                return false;
            }
            Faction supporter = ResolveSupporter(region, holding);
            if (holding.factionLinks?.support
                    != CASiteFactionReferenceKind.None
                && supporter == null)
            {
                failure = "the supporting faction is unavailable";
                return false;
            }
            if (!SatisfiesKnowledge(facts, knowledge,
                    false, null,
                    out CATechnologyRequirement missingKnowledge))
            {
                failure = MissingKnowledgeWords(missingKnowledge);
                return false;
            }
            return true;
        }

        internal static CARegionalFactionPlan RegionalSupporter(
            CARegionalPlan plan, CASettlementEnvironmentFacts facts)
        {
            if (plan == null || facts == null) return null;
            if (FrontierPotential(facts,
                    (CARegionalFactionPlan)null).Viable) return null;
            HashSet<int> settled = new HashSet<int>((plan.settlements
                    ?? new List<CARegionalSettlementPlan>())
                .Where(item => item != null)
                .Select(item => item.OwningFactionKey));
            return (plan.factions ?? new List<CARegionalFactionPlan>())
                .Where(item => item != null && settled.Contains(item.key))
                .Where(item => FrontierPotential(facts, item).Viable)
                .OrderBy(item => KnowledgeCompatibilityTier(item))
                .ThenBy(item => item.key).FirstOrDefault();
        }

        internal static Faction WorldSupporter(
            CASettlementEnvironmentFacts facts)
        {
            if (facts == null || FrontierPotential(facts, (Faction)null)
                    .Viable)
                return null;
            try
            {
                return Find.FactionManager.AllFactionsListForReading
                    .Where(faction => faction != null && !faction.IsPlayer
                        && !faction.defeated && !faction.Hidden
                        && !faction.temporary
                        && faction.def?.humanlikeFaction == true)
                    .Where(faction =>
                    {
                        try
                        {
                            return faction.PlayerRelationKind
                                != FactionRelationKind.Hostile;
                        }
                        catch { return false; }
                    })
                    .Where(faction => FrontierPotential(facts,
                        faction).Viable)
                    .OrderBy(faction => KnowledgeCompatibilityTier(faction))
                    .ThenBy(faction => faction.loadID).FirstOrDefault();
            }
            catch { return null; }
        }

        internal static Faction ResolveSupporter(CARegionalPlan region,
            CAFrontierHoldingPlan holding)
        {
            return holding == null ? null : CASiteState.ResolveSupport(
                region, holding.factionLinks);
        }

        internal static string MissingWords(int mask)
        {
            string[] words = CAHabitatViabilityCausalKernel.Enumerate(mask)
                .Select(CAHabitatViabilityCausalKernel.RequirementWords)
                .ToArray();
            return words.Length == 0 ? "the required technical capability"
                : string.Join(", ", words);
        }

        internal static string MissingKnowledgeWords(
            CATechnologyRequirement requirement)
        {
            if (requirement == null)
                return "required practical knowledge is unavailable";
            if (!requirement.ExplicitlyMapped
                || CATechnologyDomains.Find(requirement.DomainKey) == null)
                return "no technological knowledge mapping exists for "
                    + (requirement.SourceKey ?? "this requirement");
            string domain = CATechnologyDomains.Find(
                requirement.DomainKey)?.Label ?? requirement.DomainKey;
            string action = requirement.CompetencyKey
                == CATechnologyCompetencies.Construct ? "build"
                : requirement.CompetencyKey
                    == CATechnologyCompetencies.Operate ? "operate"
                : requirement.CompetencyKey
                    == CATechnologyCompetencies.Maintain ? "maintain"
                : "understand";
            return domain + " knowledge is too limited to " + action
                + " what this ground requires";
        }

        internal static CAHabitatFoodRoute RealizedFoodRoute(
            CARegionalPlan plan, CARegionalSettlementPlan settlement,
            CASettlementEnvironmentFacts facts)
        {
            return RealizedFoodRoute(facts, CompletePrograms(plan,
                settlement), CASiteState.Knowledge(plan, settlement));
        }

        private static CAHabitatFoodRoute RealizedFoodRoute(
            CASettlementEnvironmentFacts facts, HashSet<string> programs,
            CATechnologicalKnowledge knowledge)
        {
            CAHabitatFoodRoute route = facts?.FoodRoute
                ?? CAHabitatFoodRoute.OutdoorCultivation;
            bool protectedRoute = route
                    == CAHabitatFoodRoute.ProtectedCultivation
                || route == CAHabitatFoodRoute.ProtectedLowLightCultivation;
            if (!protectedRoute) return route;
            bool protectedCultivation =
                CATechnologicalKnowledgeModel.Rank(knowledge,
                    CATechnologyDomains.Agriculture,
                    CATechnologyCompetencies.Operate) >= 2
                && programs.Contains(CASettlementProgramRegistry.Agriculture)
                && (programs.Contains(
                        CASettlementProgramRegistry.Production)
                    || programs.Contains(CASettlementProgramRegistry
                        .SpecializedIndustry));
            if (protectedCultivation) return route;
            bool outsideSupply = programs.Contains(
                    CASettlementProgramRegistry.Trade)
                && programs.Contains(CASettlementProgramRegistry.Storage);
            return outsideSupply ? CAHabitatFoodRoute.StoredAndSupported
                : route;
        }

        private static bool SatisfiesKnowledge(
            CASettlementEnvironmentFacts facts,
            CATechnologicalKnowledge knowledge, bool distributed,
            Func<int, bool> pawnAvailable,
            out CATechnologyRequirement missing)
        {
            IEnumerable<CATechnologyRequirement> requirements =
                CAHabitatViabilityCausalKernel.Enumerate(
                    facts?.HabitatRequirementMask ?? 0)
                .SelectMany(CATechnologyRequirementResolver.ForHabitat);
            return CATechnologicalKnowledgeAvailability.Satisfies(knowledge,
                requirements, distributed, pawnAvailable, out missing);
        }

        private static CATechnologicalKnowledge BaselineFrontierKnowledge()
        {
            var knowledge = new CATechnologicalKnowledge();
            CATechnologicalKnowledgeModel.ApplyProfile(knowledge,
                "subsistence", CAAxisSource.Generated,
                "unaffiliated frontier subsistence");
            CATechnologicalKnowledgeModel.Ensure(knowledge,
                "frontier:subsistence", "subsistence");
            return knowledge;
        }

        private static HashSet<string> CompletePrograms(CARegionalPlan plan,
            CARegionalSettlementPlan settlement)
        {
            return new HashSet<string>(
                CASettlementProgramOperationalResolver.Build(plan,
                        settlement, CASettlementProgramRegistry.Find)
                    .Where(item => item != null && item.Complete)
                    .Select(item => item.Key), StringComparer.Ordinal);
        }
    }
}
