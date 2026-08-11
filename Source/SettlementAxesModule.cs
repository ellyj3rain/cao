using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Independent settlement state: faction, knowledge, form, Ideoligion,
    // local capabilities, facilities, and infrastructure. Explicit local
    // values override generated defaults without changing the other fields.
    internal static class CASettlementAxes
    {
        internal const int Derive = -1;

        // Index into a settlement's capability vector. Named so a
        // caller cannot silently mean the wrong one.
        internal const int CapCommunications = 0;
        internal const int CapMedicine = 1;
        internal const int CapProduction = 2;
        internal const int CapLogistics = 3;
        internal const int CapFortification = 4;
        internal const int CapWeapons = 5;
        internal const int CapTraining = 6;
        internal const int CapOrganization = 7;
        internal const int CapCount = 8;

        internal static readonly string[] CapabilityNames =
        {
            "communications", "medicine", "production", "logistics",
            "fortification", "weapons", "training", "organization"
        };

        // Faction knowledge and local ability are separate. The faction
        // template supplies an era baseline; facilities and infrastructure
        // determine what a settlement can practice.
        internal static TechLevel TemplateEraPrior(Faction faction)
        {
            return faction?.def?.techLevel ?? TechLevel.Neolithic;
        }

        internal static TechLevel TemplateEraPrior(FactionDef def)
        {
            return def?.techLevel ?? TechLevel.Neolithic;
        }

        // The three tiers everything physical keys off. Kept here so
        // there is exactly one definition of the boundary; it used to
        // live in CAStartingFacilities and be re-read by the
        // morphology adapter.
        internal static int Tier(TechLevel tech)
        {
            int level = (int)tech;
            if (level >= (int)TechLevel.Industrial) return 2;
            if (level == (int)TechLevel.Medieval) return 1;
            return 0;
        }

        // ---- settlement form -------------------------------------

        // Form is separate from technology. A settlement defaults to a form
        // its faction can build, but the author may choose another available
        // form.
        // Homesteads and cabins belong to the frontier rule, outposts
        // are grown by patrols in play; neither is a settlement form
        // this axis may resolve to.
        internal static readonly CAMorphForm[] SettlementForms =
        {
            CAMorphForm.Tribal, CAMorphForm.Medieval,
            CAMorphForm.Industrial
        };

        internal static CAMorphForm Form(int authored, TechLevel tech)
        {
            if (authored >= 0)
                for (int i = 0; i < SettlementForms.Length; i++)
                    if ((int)SettlementForms[i] == authored)
                        return SettlementForms[i];
            int tier = Tier(tech);
            return tier == 0 ? CAMorphForm.Tribal
                : tier == 1 ? CAMorphForm.Medieval
                : CAMorphForm.Industrial;
        }

        // ---- local capabilities ----------------------------------

        // Faction knowledge sets the upper limit before local constraints.
        internal static int CapabilityBasis(TechLevel eraPrior)
        {
            return Mathf.Clamp((int)eraPrior - 1, 0, 5);
        }

        // Local facilities and infrastructure can lower what a settlement
        // can practice without changing what its faction knows.
        internal const int FacilityWorkshop = 8;
        internal const int FacilityLaboratory = 64;

        internal static int LocalPracticeCeiling(int startingFacilityMask,
            bool roadLinked, bool coastal, TechLevel knowledge)
        {
            int access = roadLinked && coastal ? 2
                : roadLinked || coastal ? 1 : 0;
            int services = startingFacilityMask < 0 ? 2
                : (startingFacilityMask & FacilityLaboratory) != 0 ? 3
                : (startingFacilityMask & FacilityWorkshop) != 0 ? 2 : 0;
            int civic = startingFacilityMask < 0 ? 2
                : (startingFacilityMask & FacilityWorkshop) != 0 ? 2 : 0;
            return LocalPracticeCeiling(startingFacilityMask, access, services,
                civic, knowledge);
        }

        internal static int LocalPracticeCeiling(int startingFacilityMask,
            int accessInfrastructure, int serviceInfrastructure,
            int civicInfrastructure, TechLevel knowledge)
        {
            int basis = CapabilityBasis(knowledge);
            bool laboratory = (startingFacilityMask & FacilityLaboratory) != 0;
            bool workshop = (startingFacilityMask & FacilityWorkshop) != 0;
            int drop = laboratory ? 0 : workshop ? 1 : 2;
            if (accessInfrastructure <= 0) drop++;
            if (serviceInfrastructure <= 0) drop++;
            if (civicInfrastructure >= 2 && workshop) drop--;
            return Mathf.Clamp(basis - drop, 0, 5);
        }

        // Deterministic capability before per-settlement variation. Preview
        // and materialization use this same calculation.
        internal static int PracticedCapabilityBasis(int index,
            TechLevel knowledge, int localCeiling)
        {
            return Mathf.Max(0,
                Mathf.Min(CapabilityBasis(knowledge), localCeiling));
        }

        internal static int PracticedCapability(int index,
            TechLevel knowledge, int localCeiling)
        {
            int basis = PracticedCapabilityBasis(index, knowledge,
                localCeiling);
            return Mathf.Clamp(basis + Rand.RangeInclusive(-1, 1), 0,
                Mathf.Max(0, localCeiling));
        }

        // ---- native Ideoligion ------------------------------------

        // A settlement uses its faction's Ideoligion.
        internal static Ideo IdeoOf(Faction faction)
        {
            try { return faction?.ideos?.PrimaryIdeo; }
            catch { return null; }
        }

        // Records which settlement settings were authored and which were
        // generated.
        internal static string Provenance(int authoredForm,
            int facilityAuthoredMask, int authoredAccess,
            int authoredServices, int authoredCivic)
        {
            var authored = new List<string>();
            var derived = new List<string>();
            derived.Add("knowledge (from faction)");
            (authoredForm >= 0 ? authored : derived).Add("form");
            derived.Add("capabilities");
            int facilityCount = 0;
            for (int bit = 1; bit <= CAStartingFacilities.MaskLab; bit <<= 1)
                if ((facilityAuthoredMask & bit) != 0) facilityCount++;
            (facilityCount > 0 ? authored : derived).Add(facilityCount > 0
                && facilityCount < 7
                    ? "starting facilities (" + facilityCount + " of 7)"
                    : "starting facilities");
            (authoredAccess >= 0 ? authored : derived).Add(
                "access capacity");
            (authoredServices >= 0 ? authored : derived).Add(
                "service capacity");
            (authoredCivic >= 0 ? authored : derived).Add(
                "civic capacity");
            string a = authored.Count == 0 ? "nothing"
                : string.Join(", ", authored.ToArray());
            string d = derived.Count == 0 ? "nothing"
                : string.Join(", ", derived.ToArray());
            return "explicit: " + a
                + "; generated from settlement and faction parameters: " + d;
        }
    }

    // Starting facilities and infrastructure are separate settlement state.
    // Each facility and infrastructure dimension can be overridden separately.
    internal static class CASettlementStartingState
    {
        internal const int AllFacilityMask =
            CAStartingFacilities.MaskHearth
            | CAStartingFacilities.MaskStores
            | CAStartingFacilities.MaskInfirmary
            | CAStartingFacilities.MaskWorkshop
            | CAStartingFacilities.MaskJail
            | CAStartingFacilities.MaskDining
            | CAStartingFacilities.MaskLab;

        internal static int Access(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            if (place != null && place.realizedAccessInfrastructure >= 0)
                return Mathf.Clamp(place.realizedAccessInfrastructure, 0, 3);
            return ResolveInfrastructure(place?.accessInfrastructure ?? -1,
                DerivedAccess(plan, place), place?.developmentProfile
                    ?? CASettlementDevelopmentProfile.Contextual);
        }

        internal static int Services(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            if (place != null && place.realizedServiceInfrastructure >= 0)
                return Mathf.Clamp(place.realizedServiceInfrastructure, 0, 3);
            return ResolveInfrastructure(place?.serviceInfrastructure ?? -1,
                DerivedServices(plan, place), place?.developmentProfile
                    ?? CASettlementDevelopmentProfile.Contextual);
        }

        internal static int Civic(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            if (place != null && place.realizedCivicInfrastructure >= 0)
                return Mathf.Clamp(place.realizedCivicInfrastructure, 0, 3);
            return ResolveInfrastructure(place?.civicInfrastructure ?? -1,
                DerivedCivic(plan, place), place?.developmentProfile
                    ?? CASettlementDevelopmentProfile.Contextual);
        }

        internal static int ResolveFacilityMask(CARegionalPlan plan,
            CARegionalSettlementPlan place, FactionDef ownerDef = null)
        {
            if (place == null) return 0;
            if (ownerDef == null)
                ownerDef = plan?.FactionPlan(place.factionKey)
                    ?.ResolvedFactionDef;
            int derived = DerivedFacilityMask(plan, place, ownerDef);
            return CACulturalExpressionCausalKernel.ResolveFacilityMask(
                derived, place.startingFacilityAuthoredMask,
                place.startingFacilityValues, AllFacilityMask);
        }

        internal static int Sync(CARegionalPlan plan,
            CARegionalSettlementPlan place, FactionDef ownerDef = null)
        {
            if (place == null) return 0;
            place.startingFacilityMask = ResolveFacilityMask(plan, place, ownerDef);
            return place.startingFacilityMask;
        }

        internal static void UseDerivedFacilities(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            if (place == null) return;
            place.startingFacilityAuthoredMask = 0;
            place.startingFacilityValues = 0;
            Sync(plan, place);
        }

        internal static void SetFacilityOverride(CARegionalPlan plan,
            CARegionalSettlementPlan place, int bit, bool? included)
        {
            if (place == null || (bit & AllFacilityMask) == 0) return;
            if (!included.HasValue)
                place.startingFacilityAuthoredMask &= ~bit;
            else
            {
                place.startingFacilityAuthoredMask |= bit;
                if (included.Value) place.startingFacilityValues |= bit;
                else place.startingFacilityValues &= ~bit;
            }
            Sync(plan, place);
        }

        internal static string InfrastructureWords(int value)
        {
            return value <= 0 ? "minimal" : value == 1 ? "limited"
                : value == 2 ? "established" : "developed";
        }

        internal static string ProfileWords(
            CASettlementDevelopmentProfile profile)
        {
            switch (profile)
            {
                case CASettlementDevelopmentProfile.Minimal: return "Minimal";
                case CASettlementDevelopmentProfile.Extensive: return "Extensive";
                default: return "Contextual";
            }
        }

        internal static string ProfileEffect(
            CASettlementDevelopmentProfile profile)
        {
            switch (profile)
            {
                case CASettlementDevelopmentProfile.Minimal:
                    return "Generated transport, services, and public works are one level lower where possible.";
                case CASettlementDevelopmentProfile.Extensive:
                    return "Generated transport, services, and public works are one level higher where possible.";
                default:
                    return "Generated development follows the settlement's population, land, role, links, knowledge, and history.";
            }
        }

        private static int ResolveInfrastructure(int authored, int derived,
            CASettlementDevelopmentProfile profile)
        {
            return CACulturalExpressionCausalKernel.ResolveDevelopment(
                authored, derived, (int)profile);
        }

        internal static int ApplyDevelopmentProfile(int derived,
            CASettlementDevelopmentProfile profile)
        {
            return CACulturalExpressionCausalKernel.ApplyDevelopmentProfile(
                derived, (int)profile);
        }

        private static int DerivedAccess(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            if (place == null) return 0;
            bool road = CARegionalPlanUtility.ConstituentHasRoad(
                place.memberTileId);
            bool river = CARegionalPlanUtility.ConstituentHasRiver(
                place.memberTileId);
            bool coast = CARegionalPlanUtility.ConstituentIsCoastal(
                place.memberTileId);
            int links = (road ? 1 : 0) + (river ? 1 : 0)
                + (coast ? 1 : 0);
            int value = links >= 2 ? 2 : links == 1 ? 1 : 0;
            if ((CASettlementRole)place.realizedRole
                    == CASettlementRole.Center
                && place.residentPopulation >= 500)
                value++;
            return Mathf.Clamp(value, 0, 3);
        }

        private static int DerivedServices(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            if (place == null) return 0;
            FactionDef owner = plan?.FactionPlan(place.factionKey)
                ?.ResolvedFactionDef;
            int tier = CASettlementAxes.Tier(
                CASettlementAxes.TemplateEraPrior(owner));
            int value = place.residentPopulation >= 500 ? 2
                : place.residentPopulation >= 140 ? 1 : 0;
            int explicitFacilities = place.startingFacilityValues
                & place.startingFacilityAuthoredMask;
            if ((explicitFacilities & (CAStartingFacilities.MaskInfirmary
                    | CAStartingFacilities.MaskDining
                    | CAStartingFacilities.MaskStores)) != 0)
                value++;
            if (tier >= 2 && place.residentPopulation >= 280) value++;
            if ((CASettlementRole)place.realizedRole
                    == CASettlementRole.Center
                && place.residentPopulation >= 280) value++;
            return Mathf.Clamp(value, 0, 3);
        }

        private static int DerivedCivic(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            if (place == null) return 0;
            int value = place.residentPopulation >= 700 ? 2
                : place.residentPopulation >= 220 ? 1 : 0;
            if (place.historicalDevelopment >= 2) value++;
            if ((CASettlementRole)place.realizedRole
                    == CASettlementRole.Center
                && place.residentPopulation >= 280)
                value++;
            return Mathf.Clamp(value, 0, 3);
        }

        private static int DerivedFacilityMask(CARegionalPlan plan,
            CARegionalSettlementPlan place, FactionDef ownerDef)
        {
            TechLevel knowledge = CASettlementAxes.TemplateEraPrior(ownerDef);
            int tier = CASettlementAxes.Tier(knowledge);
            int access = Access(plan, place);
            int services = Services(plan, place);
            int civic = Civic(plan, place);
            CARegionalFactionPlan faction = plan?.FactionPlan(
                place?.factionKey ?? -1);
            string Current(string axis)
            {
                return CAFactionAxes.KeyOf(faction?.factionStructure, axis)
                    ?? CAFactionAxes.KeyOf(
                        faction?.politicalBeliefs?.positions, axis);
            }
            string support = Current(CAFactionAxes.Support);
            string economy = Current(CAFactionAxes.Economy);
            string security = Current(CAFactionAxes.LocalOrder);

            int mask = CAStartingFacilities.MaskHearth;
            bool organizedMeals = services >= 1 || support == "public"
                || support == "communal" || support == "mixed"
                || economy == "planned" || economy == "communal";
            if (organizedMeals) mask |= CAStartingFacilities.MaskDining;
            if (access >= 1 || services >= 1 || support == "public"
                || support == "communal" || support == "mixed")
                mask |= CAStartingFacilities.MaskStores;
            if (services >= 1)
                mask |= CAStartingFacilities.MaskInfirmary;
            if (tier >= 1 && civic >= 1)
                mask |= CAStartingFacilities.MaskWorkshop;
            if (tier >= 1 && civic >= 2
                && (security == "constabulary" || security == "rulers"))
                mask |= CAStartingFacilities.MaskJail;
            if (tier >= 2 && services >= 2 && civic >= 2)
                mask |= CAStartingFacilities.MaskLab;
            return mask;
        }
    }
}
