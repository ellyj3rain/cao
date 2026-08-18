using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Tax is transferred from a real treasury. A shortfall is recorded.
    // Organizations pay from their own funds; pawn tax is withheld by the
    // organization that pays the pawn.
    internal static class CATaxation
    {
        internal static float RateOf(CAOrganization org)
        {
            if (org == null) return 0f;
            string value = null;
            for (int i = 0; i < org.policies.Count; i++)
                if (org.policies[i].key == "tax rate")
                { value = org.policies[i].value; break; }
            switch (value)
            {
                case "light": return 0.05f;
                case "standard": return 0.12f;
                case "heavy": return 0.25f;
                // No tax-rate policy means no collection.
                default: return 0f;
            }
        }

        // One income pulse for one body. Returns what was actually
        // collected, which is not what was owed.
        internal static float Collect(CAOrganization org,
            CAOrganizationWorldComponent world, int now)
        {
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            if (org == null || ledger == null || world == null)
                return 0f;
            float rate = RateOf(org);
            if (rate <= 0f) return 0f;

            List<CARelation> bound = ledger.RelationsIn(org.organizationKey);
            float taken = 0f;
            int excused = 0;
            for (int i = 0; i < bound.Count; i++)
            {
                CARelation r = bound[i];
                if (r.Expired(now)) continue;
                if (!r.Delegates(CAResponsibilities.Taxes)) continue;

                float owed;
                CAOrganization source = SourceOf(r, world, out owed);
                if (source == null || owed <= 0f) continue;
                owed *= rate;

                float paid = Mathf.Min(owed, source.treasury);
                if (paid <= 0.01f)
                {
                    excused++;
                    source.Record("relations", "tax owed to "
                        + org.name + " went unpaid - nothing in hand");
                    continue;
                }
                source.treasury -= paid;
                taken += paid;
                if (paid + 0.01f < owed)
                    source.Record("relations", "tax to " + org.name
                        + " met only in part");
                // Record the transfer so Political Order can judge it.
                CAActLedger.Current?.Emit("taxation",
                    org.organizationKey, -1, r.PawnPartyId,
                    "tax of " + (int)paid + " silver for "
                    + org.name, null, IntVec3.Invalid,
                    authorityClaimed: true,
                    obligationRecognized: true,
                    consentGiven: false,
                    compensated: false,
                    emergencyBasis: false,
                    procedureFollowed: true,
                    forceUsed: false);
            }

            if (taken > 0f) org.treasury += taken;
            if (taken > 0f || excused > 0)
                org.Record("organization", "tax collected "
                    + (int)taken + " silver from " + bound.Count
                    + " bound parties"
                    + (excused > 0
                        ? "; " + excused + " could not pay" : ""));
            return taken;
        }

        // Finds the payer and the amount subject to tax.
        private static CAOrganization SourceOf(CARelation r,
            CAOrganizationWorldComponent world, out float assessable)
        {
            assessable = 0f;
            if (world == null) return null;

            // an organization pays from its own treasury
            if (!string.IsNullOrEmpty(r.OrganizationPartyKey))
            {
                CAOrganization payer = world.ByKey(r.OrganizationPartyKey);
                if (payer == null) return null;
                assessable = payer.treasury;
                return payer;
            }

            // a person is assessed on what they are paid, withheld by
            // whoever pays it
            if (!r.IsPawnParty) return null;
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            if (ledger == null) return null;
            List<CARelation> theirs = ledger.RelationsOf(r.PawnPartyId);
            CAOrganization best = null;
            float bestPay = 0f;
            int now = Find.TickManager?.TicksGame ?? 0;
            for (int i = 0; i < theirs.Count; i++)
            {
                CARelation w = theirs[i];
                if (w.Expired(now)) continue;
                if (w.compensationRate <= 0f) continue;
                if (w.compensation != CACompensationKinds.Wage
                    && w.compensation != CACompensationKinds.Share)
                    continue;
                CAOrganization employer = world.ByKey(w.orgKey);
                if (employer == null) continue;
                if (w.compensationRate > bestPay)
                {
                    bestPay = w.compensationRate;
                    best = employer;
                }
            }
            assessable = bestPay;
            return best;
        }
    }
}
