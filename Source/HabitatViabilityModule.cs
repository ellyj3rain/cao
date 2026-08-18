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

        internal static CAHabitatViabilityResult EvaluateSettlement(
            CARegionalPlan plan, CARegionalSettlementPlan settlement,
            CASettlementEnvironmentFacts facts)
        {
            CARegionalFactionPlan faction = plan?.FactionPlan(
                settlement?.factionKey ?? -1);
            int tier = KnowledgeCompatibilityTier(faction);
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
                faction?.technologicalKnowledge);
            bool protectedCultivationKnowledge =
                CATechnologicalKnowledgeModel.Rank(
                    faction?.technologicalKnowledge,
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
            if (!SatisfiesKnowledge(facts, faction?.technologicalKnowledge,
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
            SatisfiesKnowledge(facts, plan?.FactionPlan(
                    settlement.factionKey)?.technologicalKnowledge,
                false, null, out CATechnologyRequirement missingKnowledge);
            settlement.habitatBlocker = result.Viable ? null
                : result.MissingRequirementMask != 0
                    ? "Missing " + MissingWords(
                        result.MissingRequirementMask)
                    : MissingKnowledgeWords(missingKnowledge);
            return result;
        }

        internal static void ApplyFrontier(CAFrontierHoldingPlan holding,
            CASettlementEnvironmentFacts facts, int technologyTier,
            int supportingFactionKey = -1,
            int supportingFactionLoadId = -1)
        {
            if (holding == null || facts == null) return;
            bool supported = supportingFactionKey >= 0
                || supportingFactionLoadId >= 0;
            // A bare number is not an operating population. Frontier
            // capability above the unaffiliated baseline exists only when an
            // exact saved faction owns it.
            int effectiveTier = supported ? technologyTier : 0;
            CAHabitatViabilityResult result = FrontierPotential(facts,
                effectiveTier);
            holding.supportingFactionKey = supportingFactionKey;
            holding.supportingFactionLoadId = supportingFactionLoadId;
            holding.environmentSourceHash = facts.SourceHash;
            holding.habitatRequirementMask = facts.HabitatRequirementMask;
            holding.requiredCapabilityTier = facts.RequiredCapabilityTier;
            holding.capabilityTier = effectiveTier;
            holding.habitatCapabilityMask = result.CapabilityMask;
            holding.missingHabitatRequirementMask =
                result.MissingRequirementMask;
            holding.habitatFoodRoute = (byte)(supported
                    && (facts.FoodRoute
                            == CAHabitatFoodRoute.ProtectedCultivation
                        || facts.FoodRoute == CAHabitatFoodRoute
                            .ProtectedLowLightCultivation)
                ? CAHabitatFoodRoute.StoredAndSupported
                : facts.FoodRoute);
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
            bool regionalSupport = holding.supportingFactionKey >= 0;
            bool worldSupport = holding.supportingFactionLoadId >= 0;
            if (regionalSupport && worldSupport)
            {
                failure = "frontier habitat names two supporting factions";
                return false;
            }
            if (holding.capabilityTier > 0
                && !regionalSupport && !worldSupport)
            {
                failure = "frontier habitat claims technical capability "
                    + "without a supporting faction";
                return false;
            }
            if (holding.capabilityTier == 0
                && (regionalSupport || worldSupport))
            {
                failure = "frontier habitat support identity does not match "
                    + "its saved capability";
                return false;
            }
            CAHabitatViabilityResult expected = FrontierPotential(facts,
                holding.capabilityTier);
            if (holding.environmentSourceHash != facts.SourceHash
                || holding.habitatRequirementMask
                    != facts.HabitatRequirementMask
                || holding.requiredCapabilityTier
                    != facts.RequiredCapabilityTier
                || holding.habitatFoodRoute != (byte)((holding
                            .supportingFactionKey >= 0
                        || holding.supportingFactionLoadId >= 0)
                        && (facts.FoodRoute
                                == CAHabitatFoodRoute.ProtectedCultivation
                            || facts.FoodRoute == CAHabitatFoodRoute
                                .ProtectedLowLightCultivation)
                    ? CAHabitatFoodRoute.StoredAndSupported
                    : facts.FoodRoute)
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
            if ((regionalSupport || worldSupport) && supporter == null)
            {
                failure = "the supporting faction is unavailable";
                return false;
            }
            CATechnologicalKnowledge knowledge = supporter == null
                ? BaselineFrontierKnowledge()
                : CATechnologicalKnowledgeRuntime.ForFaction(supporter);
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
                .Select(item => item.factionKey));
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
            if (holding == null) return null;
            if (region != null && holding.supportingFactionKey >= 0)
            {
                CARegionalFactionPlan faction = region.FactionPlan(
                    holding.supportingFactionKey);
                return faction?.resolvedFaction
                    ?? CARegionalPlanUtility.FactionByLoadId(
                        faction?.resolvedFactionLoadId ?? -1)
                    ?? CARegionalPlanUtility.FactionByLoadId(
                        faction?.existingFactionLoadId ?? -1);
            }
            return CARegionalPlanUtility.FactionByLoadId(
                holding.supportingFactionLoadId);
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
                settlement), plan?.FactionPlan(
                    settlement?.factionKey ?? -1)?.technologicalKnowledge);
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
