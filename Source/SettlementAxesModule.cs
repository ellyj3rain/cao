using System.Collections.Generic;
using System.Linq;
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

        // Faction knowledge and local ability are separate. The faction's
        // authored knowledge supplies the broad compatibility projection;
        // facilities and infrastructure determine what a settlement practices.
        // The three tiers everything physical keys off. Kept here so
        // there is exactly one definition of the boundary; it used to
        // be re-read independently by the morphology adapter.
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

        // ---- native Ideoligion ------------------------------------

        // A settlement uses its faction's Ideoligion.
        internal static Ideo IdeoOf(Faction faction)
        {
            try { return faction?.ideos?.PrimaryIdeo; }
            catch { return null; }
        }

        // Developer-facing receipt of the concrete material causes used for
        // this settlement. Player UI names the resulting facts directly.
        internal static string Provenance(int authoredForm,
            CASettlementProgram program)
        {
            return "form=" + (authoredForm >= 0 ? "specified" : "faction")
                + "; program=population+role+institutions+knowledge+ground"
                + "; programs=" + (program?.entries?.Count ?? 0)
                + "; capacities=population+land+links+history+programs";
        }
    }

    // These values are read models over saved ground and complete operational
    // contracts. They never create a service, institution, or program.
    internal static class CASettlementStartingState
    {
        internal static int Access(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            if (place != null && place.realizedAccessInfrastructure >= 0)
                return Mathf.Clamp(place.realizedAccessInfrastructure, 0, 3);
            return DerivedAccess(plan, place);
        }

        internal static int ExpectedAccess(CARegionalSettlementPlan place)
        {
            if (place == null) return 0;
            int links = (place.hasRoadAccess ? 1 : 0)
                + (place.hasRiverAccess ? 1 : 0)
                + (place.hasCoastalAccess ? 1 : 0);
            return Mathf.Clamp(links, 0, 3);
        }

        internal static int Services(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            if (place != null && place.realizedServiceInfrastructure >= 0)
                return Mathf.Clamp(place.realizedServiceInfrastructure, 0, 3);
            return DerivedServices(plan, place);
        }

        internal static int ExpectedServices(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            return DerivedServices(plan, place);
        }

        internal static int Civic(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            if (place != null && place.realizedCivicInfrastructure >= 0)
                return Mathf.Clamp(place.realizedCivicInfrastructure, 0, 3);
            return DerivedCivic(plan, place);
        }

        internal static int ExpectedCivic(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            return DerivedCivic(plan, place);
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
            return Mathf.Clamp(links, 0, 3);
        }

        private static int DerivedServices(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            if (place == null) return 0;
            return CountComplete(plan, place,
                CASettlementProgramRegistry.FoodPreparation,
                CASettlementProgramRegistry.Storage,
                CASettlementProgramRegistry.Medicine,
                CASettlementProgramRegistry.Gathering,
                CASettlementProgramRegistry.Recreation,
                CASettlementProgramRegistry.Communications);
        }

        private static int DerivedCivic(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            if (place == null) return 0;
            return CountComplete(plan, place,
                CASettlementProgramRegistry.Governance,
                CASettlementProgramRegistry.Custody,
                CASettlementProgramRegistry.AuthorityProvision);
        }

        private static int CountComplete(CARegionalPlan plan,
            CARegionalSettlementPlan place, params string[] keys)
        {
            if (place == null || keys == null || keys.Length == 0) return 0;
            HashSet<string> wanted = new HashSet<string>(keys);
            int count = CASettlementProgramOperationalResolver.Build(plan,
                    place, CASettlementProgramRegistry.Find)
                .Count(item => item != null && item.Complete
                    && wanted.Contains(item.Key));
            return Mathf.Clamp(count, 0, 3);
        }

    }
}
