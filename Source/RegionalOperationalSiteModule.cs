using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
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

    internal sealed class CARegionalOperationalRoleDefinition
    {
        internal readonly CARegionalOperationalRole Role;
        internal readonly string Label;
        internal readonly string Description;

        internal CARegionalOperationalRoleDefinition(
            CARegionalOperationalRole role, string label, string description)
        {
            Role = role;
            Label = label;
            Description = description;
        }
    }

    internal static class CARegionalOperationalRoles
    {
        internal const int KnownMask =
            (int)(CARegionalOperationalRole.PatrolBase
                | CARegionalOperationalRole.ObservationPost
                | CARegionalOperationalRole.CombatOutpost
                | CARegionalOperationalRole.LogisticsPoint
                | CARegionalOperationalRole.CommunicationsRelay
                | CARegionalOperationalRole.CasualtyCollection
                | CARegionalOperationalRole.FallbackShelter);

        internal static readonly IReadOnlyList<
            CARegionalOperationalRoleDefinition> Definitions = new[]
        {
            new CARegionalOperationalRoleDefinition(
                CARegionalOperationalRole.PatrolBase,
                "Patrol base",
                "Intended home and sustainment point for local patrols, relief, and return-to-post."),
            new CARegionalOperationalRoleDefinition(
                CARegionalOperationalRole.ObservationPost,
                "Observation post",
                "Intended fixed observation and reporting position."),
            new CARegionalOperationalRoleDefinition(
                CARegionalOperationalRole.CombatOutpost,
                "Combat outpost",
                "Intended defended position for local security and rapid response."),
            new CARegionalOperationalRoleDefinition(
                CARegionalOperationalRole.LogisticsPoint,
                "Logistics point",
                "Intended cache, resupply, maintenance, or distribution location."),
            new CARegionalOperationalRoleDefinition(
                CARegionalOperationalRole.CommunicationsRelay,
                "Communications relay",
                "Intended communications node or relay."),
            new CARegionalOperationalRoleDefinition(
                CARegionalOperationalRole.CasualtyCollection,
                "Casualty-collection site",
                "Intended protected collection, triage, and evacuation point."),
            new CARegionalOperationalRoleDefinition(
                CARegionalOperationalRole.FallbackShelter,
                "Fallback shelter",
                "Intended fallback and temporary survival position.")
        };

        internal static bool Has(int mask, CARegionalOperationalRole role)
        {
            return (mask & (int)role) != 0;
        }

        internal static int Set(int mask, CARegionalOperationalRole role,
            bool enabled)
        {
            return enabled ? mask | (int)role : mask & ~(int)role;
        }

        internal static string Summary(int mask)
        {
            List<string> labels = Definitions.Where(item => Has(mask,
                    item.Role)).Select(item => item.Label).ToList();
            return labels.Count == 0 ? "No operational roles"
                : string.Join(", ", labels);
        }
    }

    internal sealed class Dialog_CARegionalOperationalRoles : Window
    {
        private readonly CARegionalSettlementPlan site;

        public override Vector2 InitialSize => new Vector2(680f, 550f);

        internal Dialog_CARegionalOperationalRoles(CARegionalSettlementPlan site)
        {
            this.site = site;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f),
                "Operational roles for Base " + (site.slot + 1));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 38f, inRect.width, 58f),
                "These roles record intended use. They do not create buildings, "
                + "supplies, communications, medical capacity, observation, "
                + "awareness, or orders; those remain consequences of the "
                + "site's actual people, equipment, facilities, and access.");

            float y = 108f;
            foreach (CARegionalOperationalRoleDefinition definition in
                     CARegionalOperationalRoles.Definitions)
            {
                bool enabled = CARegionalOperationalRoles.Has(
                    site.operationalRoleMask, definition.Role);
                Widgets.CheckboxLabeled(new Rect(0f, y, 220f, 32f),
                    definition.Label, ref enabled);
                site.operationalRoleMask = CARegionalOperationalRoles.Set(
                    site.operationalRoleMask, definition.Role, enabled);
                Widgets.Label(new Rect(230f, y + 2f,
                    inRect.width - 230f, 42f), definition.Description);
                y += 48f;
            }

            if (Widgets.ButtonText(new Rect(0f, inRect.height - 42f,
                    130f, 42f), "Clear all"))
                site.operationalRoleMask &= ~CARegionalOperationalRoles.KnownMask;
            if (Widgets.ButtonText(new Rect(inRect.width - 150f,
                    inRect.height - 42f, 150f, 42f), "Done"))
                Close();
        }
    }
}
