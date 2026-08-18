using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Relations view (hosted inside the Squads tab): colonists as nodes ordered by
    // subfaction cluster; edges are strong mutual opinions. Header uses the game's own
    // nomenclature: the ideoligion's name and its meme labels.
    public static class RelationsView
    {
        public static void Draw(Rect inRect)
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            var pawns = new List<Pawn>(map.mapPawns.FreeColonistsSpawned);
            if (pawns.Count == 0) return;

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect header = new Rect(inRect.x, inRect.y + 2f, inRect.width, 30f);
            Widgets.Label(header, OrgIdentity.Compute(map, pawns));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(header, OrgIdentity.Explanation(map, pawns));

            var clusters = Cluster(pawns);
            var ordered = new List<Pawn>();
            foreach (var cl in clusters) ordered.AddRange(cl);

            Vector2 center = new Vector2(inRect.x + inRect.width / 2f, inRect.y + 40f + (inRect.height - 80f) / 2f);
            float radius = Mathf.Min(inRect.width, inRect.height - 80f) / 2f - 60f;
            var pos = new Dictionary<Pawn, Vector2>();
            for (int i = 0; i < ordered.Count; i++)
            {
                float ang = (Mathf.PI * 2f * i) / ordered.Count - Mathf.PI / 2f;
                pos[ordered[i]] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                for (int j = i + 1; j < ordered.Count; j++)
                {
                    var a = ordered[i]; var b = ordered[j];
                    float avg = (a.relations.OpinionOf(b) + b.relations.OpinionOf(a)) / 2f;
                    if (Mathf.Abs(avg) < 15f) continue;
                    float t = Mathf.Clamp01(Mathf.Abs(avg) / 80f);
                    Color col = avg > 0
                        ? new Color(0.3f, 0.9f, 0.3f, 0.25f + 0.55f * t)
                        : new Color(0.95f, 0.3f, 0.25f, 0.25f + 0.55f * t);
                    Widgets.DrawLine(pos[a], pos[b], col, 1f + 2f * t);
                }
            }

            var ringColors = new Color[] {
                new Color(0.55f, 0.75f, 1f), new Color(1f, 0.8f, 0.45f),
                new Color(0.75f, 1f, 0.6f), new Color(1f, 0.6f, 0.9f),
                new Color(0.8f, 0.8f, 0.8f)
            };
            var clusterOf = new Dictionary<Pawn, int>();
            for (int ci = 0; ci < clusters.Count; ci++)
                foreach (var p in clusters[ci]) clusterOf[p] = ci;

            var ev = Event.current;
            for (int i = 0; i < ordered.Count; i++)
            {
                var p = ordered[i];
                Vector2 c = pos[p];
                Rect node = new Rect(c.x - 20f, c.y - 20f, 40f, 40f);
                int ci = clusterOf[p];
                GUI.color = ringColors[ci % ringColors.Length];
                Widgets.DrawBox(node.ExpandedBy(2f), 2);
                GUI.color = Color.white;
                Widgets.DrawOptionBackground(node, false);
                Widgets.ThingIcon(node.ContractedBy(2f), p);
                Rect nameRect = new Rect(c.x - 50f, node.yMax + 1f, 100f, 18f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                bool anyLead = SquadComponent.IsLeader(p) || HasIdeoRole(p);
                GUI.color = anyLead ? new Color(1f, 0.95f, 0.6f) : Color.white;
                Widgets.Label(nameRect, p.LabelShortCap);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;

                TooltipHandler.TipRegion(node, NodeTooltip(p, ordered));
                if (ev.type == EventType.MouseDown && node.Contains(ev.mousePosition))
                {
                    if (ev.button == 1) AwarenessPawnMenu.Open(p);
                    else { CameraJumper.TryJumpAndSelect(p); }
                    ev.Use();
                }
            }

            float ly = inRect.yMax - 20f * clusters.Count - 4f;
            Text.Font = GameFont.Tiny;
            for (int ci = 0; ci < clusters.Count; ci++)
            {
                Rect row = new Rect(inRect.x + 6f, ly + ci * 20f, inRect.width / 2f, 18f);
                GUI.color = ringColors[ci % ringColors.Length];
                var names = new List<string>();
                foreach (var p in clusters[ci]) names.Add(p.LabelShortCap);
                Widgets.Label(row, (clusters[ci].Count > 1 ? "Group: " : "Independent: ") + string.Join(", ", names));
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private static bool HasIdeoRole(Pawn p)
        {
            try { return p.Ideo != null && p.Ideo.GetRole(p) != null; } catch { return false; }
        }

        private static string NodeTooltip(Pawn p, List<Pawn> all)
        {
            Pawn love = null, hate = null; int lo = int.MinValue, ho = int.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                var q = all[i]; if (q == p) continue;
                int op = p.relations.OpinionOf(q);
                if (op > lo) { lo = op; love = q; }
                if (op < ho) { ho = op; hate = q; }
            }
            string s = p.LabelShortCap;
            if (SquadComponent.IsLeader(p)) s += "\nSquad leader";
            if (SquadComponent.IsFireteamLeader(p)) s += "\nFire team leader";
            if (HasIdeoRole(p)) s += "\nHolds an ideoligion role";
            if (love != null && lo >= 15) s += "\nClosest to: " + love.LabelShortCap + " (+" + lo + ")";
            if (hate != null && ho <= -15) s += "\nColdest to: " + hate.LabelShortCap + " (" + ho + ")";
            s += "\n\nLeft-click: select. Right-click: awareness menu.";
            return s;
        }

        private static List<List<Pawn>> Cluster(List<Pawn> pawns)
        {
            var clusters = new List<List<Pawn>>();
            var sorted = new List<Pawn>(pawns);
            sorted.Sort((a, b) => TotalWarmth(b, pawns).CompareTo(TotalWarmth(a, pawns)));
            foreach (var p in sorted)
            {
                List<Pawn> best = null;
                float bestAvg = 12f;
                foreach (var cl in clusters)
                {
                    float sum = 0f;
                    foreach (var q in cl) sum += (p.relations.OpinionOf(q) + q.relations.OpinionOf(p)) / 2f;
                    float avg = sum / cl.Count;
                    if (avg > bestAvg) { bestAvg = avg; best = cl; }
                }
                if (best != null) best.Add(p);
                else clusters.Add(new List<Pawn> { p });
            }
            clusters.Sort((a, b) => b.Count.CompareTo(a.Count));
            return clusters;
        }

        private static float TotalWarmth(Pawn p, List<Pawn> all)
        {
            float t = 0f;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != p) t += p.relations.OpinionOf(all[i]);
            return t;
        }
    }

    // Identity in the game's own nomenclature: the ideoligion's generated name plus its
    // actual meme labels. The creative layer can build on this later.
    public static class OrgIdentity
    {
        public static string Compute(Map map, List<Pawn> pawns)
        {
            string ideoName = null;
            var memeLabels = new List<string>();
            try
            {
                var ideo = Faction.OfPlayer.ideos != null ? Faction.OfPlayer.ideos.PrimaryIdeo : null;
                if (ideo != null)
                {
                    ideoName = ideo.name;
                    foreach (var m in ideo.memes)
                        if (m.category != MemeCategory.Structure) memeLabels.Add(m.label);
                }
            }
            catch { }

            if (ideoName == null) return Faction.OfPlayer.Name;
            if (memeLabels.Count == 0) return ideoName;
            return ideoName + " - " + string.Join(", ", memeLabels);
        }

        public static string Explanation(Map map, List<Pawn> pawns)
        {
            int n = pawns.Count;
            int armed = 0, squadded = 0, ideoRoles = 0, male = 0;
            float ageSum = 0f;
            foreach (var p in pawns)
            {
                if (p.equipment != null && p.equipment.Primary != null) armed++;
                if (SquadComponent.SquadOf(p) > 0) squadded++;
                try { if (p.Ideo != null && p.Ideo.GetRole(p) != null) ideoRoles++; } catch { }
                if (p.gender == Gender.Male) male++;
                ageSum += p.ageTracker != null ? p.ageTracker.AgeBiologicalYearsFloat : 30f;
            }
            return "The colony's belief system and its memes, in the game's own terms.\n\nComposition:\n"
                + "Colonists: " + n
                + "\nArmed: " + armed + "  In squads: " + squadded
                + "\nIdeoligion roles held: " + ideoRoles
                + "\nAverage age: " + (ageSum / Mathf.Max(1, n)).ToString("F0")
                + "\nMale/female: " + male + "/" + (n - male);
        }
    }
}
