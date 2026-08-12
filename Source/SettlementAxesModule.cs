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

        // ---- local capabilities ----------------------------------

        // Faction knowledge sets the upper limit before local constraints.
        internal static int CapabilityBasis(TechLevel eraPrior)
        {
            return Mathf.Clamp((int)eraPrior - 1, 0, 5);
        }

        // Local programs and infrastructure can lower what a settlement
        // can practice without changing what its faction knows.
        internal static int LocalPracticeCeiling(CASettlementProgram program,
            bool roadLinked, bool coastal, TechLevel knowledge)
        {
            int access = roadLinked && coastal ? 2
                : roadLinked || coastal ? 1 : 0;
            int services = program?.Has(CASettlementProgramRegistry.Research)
                == true ? 3
                : program?.Has(CASettlementProgramRegistry.Medicine) == true
                    ? 2 : 0;
            int civic = program?.Has(CASettlementProgramRegistry.Governance)
                == true ? 2 : 0;
            return LocalPracticeCeiling(program, access, services,
                civic, knowledge);
        }

        internal static int LocalPracticeCeiling(CASettlementProgram program,
            int accessInfrastructure, int serviceInfrastructure,
            int civicInfrastructure, TechLevel knowledge)
        {
            int basis = CapabilityBasis(knowledge);
            bool laboratory = program?.Has(
                CASettlementProgramRegistry.Research) == true;
            bool workshop = program?.Has(
                CASettlementProgramRegistry.Production) == true;
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

    // Infrastructure is derived independently of the open settlement program.
    internal static class CASettlementStartingState
    {
        internal static int Access(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            if (place != null && place.realizedAccessInfrastructure >= 0)
                return Mathf.Clamp(place.realizedAccessInfrastructure, 0, 3);
            return DerivedAccess(plan, place);
        }

        internal static int Services(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            if (place != null && place.realizedServiceInfrastructure >= 0)
                return Mathf.Clamp(place.realizedServiceInfrastructure, 0, 3);
            return DerivedServices(plan, place);
        }

        internal static int Civic(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            if (place != null && place.realizedCivicInfrastructure >= 0)
                return Mathf.Clamp(place.realizedCivicInfrastructure, 0, 3);
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

    }
}
