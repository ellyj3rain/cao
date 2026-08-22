using System;
using System.Collections.Generic;

namespace ColonistAwareness
{
    // WHAT A DECLARED OPERATIONAL ROLE IS, WHEN IT IS BUILT.
    //
    // The role mask was authored, saved, summarized in the overview and
    // drawn on the globe, and changed nothing that stood on the ground:
    // a settlement declared a combat outpost was built exactly like one
    // declared nothing. A declaration that costs nothing and yields
    // nothing is not a fact about the world.
    //
    // Each role is here as the settlement program it IS once built, so
    // the consequence goes through the same starting-composition path
    // that environment, habitat requirement, population standing,
    // history and coastal access already go through - and through the
    // same materialization screen, so a role can commit a settlement to
    // works but never exempt it from being able to raise them.
    //
    // Kept pure and kept as the only copy so the completeness of the
    // table is provable: a role added to the enum with no program
    // behind it fails a receipt rather than shipping as another
    // declaration nobody honours.
    [Flags]
    public enum CARegionalOperationalRole
    {
        None = 0,
        PatrolBase = 1 << 0,
        ObservationPost = 1 << 1,
        CombatOutpost = 1 << 2,
        LogisticsPoint = 1 << 3,
        CommunicationsRelay = 1 << 4,
        CasualtyCollection = 1 << 5,
        FallbackShelter = 1 << 6
    }

    public static class CAOperationalRoleProgramKernel
    {
        // Program keys come from the causal kernel that already owns
        // them rather than being spelled again here, so this table
        // cannot drift from the programs it names.
        public static readonly CARegionalOperationalRole[] AllRoles =
        {
            CARegionalOperationalRole.PatrolBase,
            CARegionalOperationalRole.ObservationPost,
            CARegionalOperationalRole.CombatOutpost,
            CARegionalOperationalRole.LogisticsPoint,
            CARegionalOperationalRole.CommunicationsRelay,
            CARegionalOperationalRole.CasualtyCollection,
            CARegionalOperationalRole.FallbackShelter
        };

        // Patrols, watch and fighting are all defensive works. A
        // logistics point holds goods and moves them. A relay is a
        // communications installation. A casualty collection point is
        // medical. A place people are meant to fall back TO keeps
        // reserves for the people who reach it with nothing.
        private static readonly Dictionary<CARegionalOperationalRole,
            string[]> Table =
            new Dictionary<CARegionalOperationalRole, string[]>
            {
                { CARegionalOperationalRole.PatrolBase,
                    new[] { CASettlementProgramCausalKernel.Defense } },
                { CARegionalOperationalRole.ObservationPost,
                    new[] { CASettlementProgramCausalKernel.Defense } },
                { CARegionalOperationalRole.CombatOutpost,
                    new[] { CASettlementProgramCausalKernel.Defense } },
                { CARegionalOperationalRole.LogisticsPoint,
                    new[] { CASettlementProgramCausalKernel.Storage,
                        CASettlementProgramCausalKernel.Transport } },
                { CARegionalOperationalRole.CommunicationsRelay,
                    new[] { CASettlementProgramCausalKernel.Communications } },
                { CARegionalOperationalRole.CasualtyCollection,
                    new[] { CASettlementProgramCausalKernel.Medicine } },
                { CARegionalOperationalRole.FallbackShelter,
                    new[] { CASettlementProgramCausalKernel.Storage } }
            };

        public static IReadOnlyList<string> ProgramKeysFor(
            CARegionalOperationalRole role)
        {
            return Table.TryGetValue(role, out string[] keys)
                ? keys : Array.Empty<string>();
        }

        // The programs a settlement takes on from everything it has
        // declared, in role order and without repeats, so two roles
        // that want the same works commit it once.
        public static List<string> ProgramKeysFor(int roleMask)
        {
            var result = new List<string>();
            foreach (CARegionalOperationalRole role in AllRoles)
            {
                if ((roleMask & (int)role) == 0) continue;
                foreach (string key in ProgramKeysFor(role))
                    if (!result.Contains(key)) result.Add(key);
            }
            return result;
        }

        // Every role carries at least one program. A role that carries
        // none is a declaration nobody honours, which is the exact
        // defect this table exists to close.
        public static List<CARegionalOperationalRole> RolesWithoutPrograms()
        {
            var missing = new List<CARegionalOperationalRole>();
            foreach (CARegionalOperationalRole role in AllRoles)
                if (ProgramKeysFor(role).Count == 0) missing.Add(role);
            return missing;
        }

    }
}
