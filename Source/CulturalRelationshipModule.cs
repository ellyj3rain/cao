using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Culture changes how a pawn publicly approaches a relationship. Native
    // orientation, attraction, compatibility, Ideoligion, and relationship
    // rules remain authoritative; a zero native weight remains zero.
    [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt),
        "RandomSelectionWeight")]
    internal static class CAPatch_CulturalRomanceSelection
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn initiator, Pawn recipient,
            ref float __result)
        {
            if (__result <= 0f || initiator == null || recipient == null)
                return;
            CACulturalCognitionWorldComponent cognition =
                CACulturalCognitionWorldComponent.Current;
            if (cognition == null) return;

            if (initiator.gender != Gender.None
                && initiator.gender == recipient.gender)
            {
                CAPawnCulturalAttitude attitude = cognition.AttitudeFor(
                    initiator, CACultureQuestionRegistry.SameSexAcceptance);
                __result *= PublicApproachFactor(attitude);
            }

            bool alreadyPartnered = initiator.relations?.DirectRelations
                .Any(relation => relation?.otherPawn != null
                    && relation.otherPawn != recipient
                    && (relation.def == PawnRelationDefOf.Lover
                        || relation.def == PawnRelationDefOf.Fiance
                        || relation.def == PawnRelationDefOf.Spouse)) == true;
            if (alreadyPartnered)
            {
                CAPawnCulturalAttitude attitude = cognition.AttitudeFor(
                    initiator, CACultureQuestionRegistry.PluralityAcceptance);
                __result *= PublicApproachFactor(attitude);
            }
        }

        internal static float PublicApproachFactor(
            CAPawnCulturalAttitude attitude)
        {
            if (attitude == null) return 1f;
            return CACulturalCognitionPureKernel.RelationshipApproachFactor(
                attitude.publicExpression, attitude.knowledgeConfidence);
        }
    }
}
