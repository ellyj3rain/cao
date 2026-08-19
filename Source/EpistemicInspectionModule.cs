using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Human-facing inspection for the optional broader knowledge mode. It
    // describes the pawn's records; it never resolves or displays live truth.
    public sealed class Dialog_CAPawnKnowledge : Window
    {
        private readonly Pawn pawn;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(920f, 760f);

        public Dialog_CAPawnKnowledge(Pawn pawn)
        {
            this.pawn = pawn;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 36f),
                "Knowledge: " + (pawn?.LabelShortCap ?? "Unknown pawn"));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 42f, inRect.width,
                    46f),
                "These are this pawn's own observations and reports. "
                + "A confident record can still be old or contradicted.");

            IReadOnlyList<CAKnowledgePropositionRecord> records =
                CAPropositionKnowledgeWorldComponent.Current?.ForPawn(pawn)
                ?? Array.Empty<CAKnowledgePropositionRecord>();
            Rect outRect = new Rect(inRect.x, inRect.y + 94f,
                inRect.width, inRect.height - 144f);
            float height = Math.Max(outRect.height,
                records.Sum(RecordHeight) + 12f);
            Rect viewRect = new Rect(0f, 0f,
                outRect.width - 18f, height);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float y = 0f;
            if (records.Count == 0)
            {
                Widgets.Label(new Rect(0f, y, viewRect.width, 48f),
                    "No broader knowledge has been recorded for this pawn.");
            }
            else
            {
                int now = Find.TickManager?.TicksGame ?? 0;
                foreach (CAKnowledgePropositionRecord record in records)
                {
                    float rowHeight = RecordHeight(record);
                    DrawRecord(new Rect(0f, y, viewRect.width, rowHeight),
                        record, now);
                    y += rowHeight;
                }
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.center.x - 90f,
                    inRect.yMax - 40f, 180f, 36f), "Close"))
                Close();
        }

        private static float RecordHeight(CAKnowledgePropositionRecord record)
        {
            string content = record?.content ?? "";
            return 148f + Math.Min(70f,
                Text.CalcHeight(content, 820f));
        }

        private static void DrawRecord(Rect rect,
            CAKnowledgePropositionRecord record, int now)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(10f);
            string state = record.IsStale(now) ? "stale"
                : record.supersededByIdentity.NullOrEmpty()
                    ? "current record" : "superseded";
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 30f),
                FactLabel(record.factKind) + ": " + record.claim);
            Text.Font = GameFont.Small;
            float y = inner.y + 34f;
            Widgets.Label(new Rect(inner.x, y, inner.width, 24f),
                "Confidence " + Percent(record.confidence)
                + "  |  uncertainty " + Percent(record.uncertainty)
                + "  |  " + state + "  |  revision " + record.revision);
            y += 26f;
            float contentHeight = Math.Min(70f,
                Text.CalcHeight(record.content ?? "Not recorded",
                    inner.width));
            Widgets.Label(new Rect(inner.x, y, inner.width, contentHeight),
                record.content ?? "Not recorded");
            y += contentHeight + 4f;
            string reporter = record.immediateReporterIdentity
                    == record.holderIdentity
                ? "Observed directly"
                : "Reported by " + DisplayIdentity(
                    record.immediateReporterIdentity);
            Widgets.Label(new Rect(inner.x, y, inner.width, 24f), reporter
                + "  |  source event " + Age(record.sourceEventTick, now)
                + "  |  learned " + Age(record.acquiredTick, now));
            y += 24f;
            Widgets.Label(new Rect(inner.x, y, inner.width, 24f),
                "Source: " + (record.sourceType ?? "not recorded")
                + " via " + (record.acquisitionChannel ?? "not recorded")
                + "  |  contradictions "
                + (record.contradictions?.Count ?? 0)
                + "  |  " + PersistenceLabel(record.persistenceClass));
        }

        private static string FactLabel(CAKnowledgeFactKind kind)
        {
            switch (kind)
            {
                case CAKnowledgeFactKind.SiteAffiliation:
                    return "Settlement ownership and affiliation";
                case CAKnowledgeFactKind.SitePopulation:
                    return "Settlement population";
                case CAKnowledgeFactKind.PoliticalOrder:
                    return "Political order";
                case CAKnowledgeFactKind.TechnologicalKnowledge:
                    return "Technological knowledge";
                case CAKnowledgeFactKind.Officeholder:
                    return "Officeholders";
                default:
                    return kind.ToString();
            }
        }

        private static string PersistenceLabel(
            CAKnowledgePersistenceClass value)
        {
            switch (value)
            {
                case CAKnowledgePersistenceClass.Transient:
                    return "short-lived memory";
                case CAKnowledgePersistenceClass.Working:
                    return "working memory";
                case CAKnowledgePersistenceClass.Institutional:
                    return "institutionally durable";
                default:
                    return "long-lived memory";
            }
        }

        private static string DisplayIdentity(string identity)
        {
            if (identity.NullOrEmpty()) return "unknown source";
            if (identity.StartsWith("pawn:", StringComparison.Ordinal)
                && int.TryParse(identity.Substring(5), out int pawnId))
            {
                Pawn found = PawnsFinder.AllMapsWorldAndTemporary_Alive
                    .FirstOrDefault(value => value?.thingIDNumber == pawnId);
                if (found != null) return found.LabelShortCap;
            }
            return identity.Replace("#", " / ");
        }

        private static string Age(int tick, int now)
        {
            if (tick < 0) return "at an unknown time";
            int elapsed = Math.Max(0, now - tick);
            if (elapsed < 2500) return "just now";
            float days = elapsed / 60000f;
            return days < 1f ? days.ToString("0.0") + " days ago"
                : days.ToString("0.#") + " days ago";
        }

        private static string Percent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }
    }

    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    internal static class CAPawnKnowledgeInspectionPatch
    {
        [HarmonyPostfix]
        private static IEnumerable<Gizmo> Postfix(
            IEnumerable<Gizmo> values, Pawn __instance)
        {
            foreach (Gizmo value in values) yield return value;
            if (AwarenessMod.Settings?.experimentalBroaderPawnKnowledge
                    != true
                || __instance?.RaceProps?.Humanlike != true)
                yield break;
            yield return new Command_Action
            {
                defaultLabel = "Knowledge",
                defaultDesc = "Inspect this pawn's own observations, reports, "
                    + "confidence, contradictions, and stale information.",
                icon = TexButton.Info,
                action = () => Find.WindowStack.Add(
                    new Dialog_CAPawnKnowledge(__instance))
            };
        }
    }
}
