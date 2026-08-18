using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Squad board: columns of colonist chips with permanent HQ / Fire team A / Fire team B
    // lanes. Drag a chip onto a lane to assign it there; right-click a chip for the full
    // awareness menu; click the FT box to cycle; click the star for squad leader.
    public class MainTabWindow_Squads : MainTabWindow
    {
        private static Pawn dragged;
        private const float ChipH = 34f;

        public override Vector2 RequestedTabSize
        {
            get { return new Vector2(940f, 520f); }
        }

        private static int viewMode; // 0 squads, 1 relations

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            var map = Find.CurrentMap;
            if (map == null) return;

            // One button on the bottom bar, two views inside.
            Rect tabSquads = new Rect(inRect.x, inRect.y, 110f, 24f);
            Rect tabRelations = new Rect(inRect.x + 114f, inRect.y, 110f, 24f);
            if (Widgets.ButtonText(tabSquads, viewMode == 0 ? "> Squads" : "Squads")) viewMode = 0;
            if (Widgets.ButtonText(tabRelations, viewMode == 1 ? "> Relations" : "Relations")) viewMode = 1;
            Rect content = new Rect(inRect.x, inRect.y + 28f, inRect.width, inRect.height - 28f);
            if (viewMode == 1)
            {
                RelationsView.Draw(content);
                return;
            }
            inRect = content;

            var hq = new List<Pawn>[5];
            var teamA = new List<Pawn>[5];
            var teamB = new List<Pawn>[5];
            for (int i = 0; i < 5; i++) { hq[i] = new List<Pawn>(); teamA[i] = new List<Pawn>(); teamB[i] = new List<Pawn>(); }
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                var p = colonists[i];
                int s = SquadComponent.SquadOf(p);
                int c = s >= 1 && s <= 4 ? s : 0;
                int ft = c == 0 ? 0 : SquadComponent.FireteamOf(p);
                if (ft == 1) teamA[c].Add(p);
                else if (ft == 2) teamB[c].Add(p);
                else hq[c].Add(p);
            }

            float colW = inRect.width / 5f;
            var ev = Event.current;

            for (int c = 0; c < 5; c++)
            {
                int count = hq[c].Count + teamA[c].Count + teamB[c].Count;
                Rect col = new Rect(inRect.x + c * colW + 4f, inRect.y, colW - 8f, inRect.height - 4f);
                Widgets.DrawMenuSection(col);
                Rect title = new Rect(col.x, col.y + 4f, col.width, 24f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(title, (c == 0 ? "Unassigned" : "Squad " + c) + " (" + count + ")");
                Text.Anchor = TextAnchor.UpperLeft;

                float y = col.y + 32f;
                float hqStart = y;
                DrawChips(hq[c], col, c, ev, ref y);
                Rect hqZone = new Rect(col.x, hqStart, col.width, Mathf.Max(y - hqStart, 30f));
                if (hq[c].Count == 0 && c > 0) y += 6f;

                Rect zoneA = default(Rect), zoneB = default(Rect);
                if (c > 0)
                {
                    zoneA = DrawLane("Fire team A", teamA[c], col, c, ev, ref y);
                    zoneB = DrawLane("Fire team B", teamB[c], col, c, ev, ref y);
                }

                if (dragged != null && ev.type == EventType.MouseUp && col.Contains(ev.mousePosition))
                {
                    if (c == 0)
                    {
                        SquadComponent.SetFireteam(dragged, 0);
                        SquadComponent.Leave(dragged);
                    }
                    else
                    {
                        SquadComponent.Assign(dragged, c);
                        if (zoneA.Contains(ev.mousePosition)) SquadComponent.SetFireteam(dragged, 1);
                        else if (zoneB.Contains(ev.mousePosition)) SquadComponent.SetFireteam(dragged, 2);
                        else SquadComponent.SetFireteam(dragged, 0);
                    }
                    dragged = null;
                    ev.Use();
                }
            }

            if (dragged != null)
            {
                if (ev.type == EventType.MouseUp) { dragged = null; }
                else
                {
                    Rect ghost = new Rect(ev.mousePosition.x + 8f, ev.mousePosition.y - 12f, 200f, 24f);
                    GUI.color = new Color(1f, 1f, 1f, 0.8f);
                    Widgets.Label(ghost, "> " + dragged.LabelShortCap);
                    GUI.color = Color.white;
                }
            }
        }

        private Rect DrawLane(string label, List<Pawn> members, Rect col, int c, Event ev, ref float y)
        {
            float start = y;
            Rect header = new Rect(col.x + 6f, y, col.width - 12f, 18f);
            GUI.color = new Color(0.7f, 0.9f, 1f, 0.6f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(header, label + (members.Count == 0 ? "  (drop here)" : ""));
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            y += 20f;
            DrawChips(members, col, c, ev, ref y);
            if (members.Count == 0) y += 16f;
            return new Rect(col.x, start, col.width, y - start);
        }

        private void DrawChips(List<Pawn> list, Rect col, int c, Event ev, ref float y)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p == dragged) continue;
                Rect chip = new Rect(col.x + 4f, y, col.width - 8f, ChipH);
                Widgets.DrawOptionBackground(chip, false);
                Rect icon = new Rect(chip.x + 2f, chip.y + 2f, ChipH - 4f, ChipH - 4f);
                Widgets.ThingIcon(icon, p);
                bool lead = SquadComponent.IsLeader(p);
                Rect name = new Rect(icon.xMax + 4f, chip.y, chip.width - ChipH - 66f, ChipH);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(name, p.LabelShortCap);
                Text.Anchor = TextAnchor.UpperLeft;

                Rect ftBox = new Rect(chip.xMax - 58f, chip.y + 4f, 26f, 26f);
                Rect star = new Rect(chip.xMax - 30f, chip.y + 3f, 28f, 28f);
                if (c > 0)
                {
                    int ft = SquadComponent.FireteamOf(p);
                    bool ftLead = SquadComponent.IsFireteamLeader(p);
                    Widgets.DrawOptionBackground(ftBox, false);
                    if (ev.type == EventType.MouseDown && ev.button == 0 && ftBox.Contains(ev.mousePosition))
                    {
                        SquadComponent.SetFireteam(p, (ft + 1) % 3);
                        ev.Use();
                    }
                    GUI.color = ft == 0 ? new Color(1f, 1f, 1f, 0.4f) : (ftLead ? Color.cyan : new Color(0.7f, 0.9f, 1f, 0.95f));
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(ftBox, ft == 0 ? "-" : (ft == 1 ? "A" : "B") + (ftLead ? "*" : ""));
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(ftBox, "Fire team - click to cycle. Right-click the row for the full menu.");

                    if (ev.type == EventType.MouseDown && ev.button == 0 && star.Contains(ev.mousePosition))
                    {
                        SquadComponent.SetLeader(p);
                        ev.Use();
                    }
                    GUI.color = lead ? Color.yellow : new Color(1f, 1f, 1f, 0.35f);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(star, lead ? "[*]" : "*");
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(star, lead ? "Squad leader" : "Make squad leader");
                }

                if (ev.type == EventType.MouseDown && ev.button == 1 && chip.Contains(ev.mousePosition))
                {
                    AwarenessPawnMenu.Open(p);
                    ev.Use();
                }
                else if (ev.type == EventType.MouseDown && ev.button == 0 && chip.Contains(ev.mousePosition)
                    && !(c > 0 && (star.Contains(ev.mousePosition) || ftBox.Contains(ev.mousePosition))))
                {
                    dragged = p;
                    ev.Use();
                }
                y += ChipH + 4f;
            }
        }
    }

    // Drafting a squad leader relays the command to members who receive and
    // accept it; pawn initiative is not an obedience requirement.
    [HarmonyPatch(typeof(Pawn_DraftController), "Drafted", MethodType.Setter)]
    public static class Patch_DraftChain
    {
        private static bool chaining;

        public static void Postfix(Pawn_DraftController __instance, bool value)
        {
            if (chaining) return;
            SquadComponent.ClearPropagatedDraftForDirectToggle(
                __instance.pawn, value);
            var leader = __instance.pawn;
            if (leader == null || leader.Map == null) return;
            if (!value)
            {
                SquadComponent.ReleaseDraftsOwnedBy(leader,
                    "originating leader was undrafted");
                return;
            }
            var s = AwarenessMod.Settings;
            if (s == null || !s.draftChain) return;
            // The cascade honors the whole gradient: a squad leader drafts
            // the squad; a fireteam leader drafts their own fireteam only,
            // and never upward - a team lead does not pull the squad leader.
            bool squadWide = SquadComponent.IsLeader(leader);
            if (!squadWide && !SquadComponent.IsFireteamLeader(leader)) return;
            int sq = SquadComponent.SquadOf(leader);
            if (sq == 0) return;
            int team = SquadComponent.FireteamOf(leader);

            chaining = true;
            try
            {
                var colonists = leader.Map.mapPawns.FreeColonistsSpawned;
                int episode = CACombatIntent.NewEpisode();
                for (int i = 0; i < colonists.Count; i++)
                {
                    var p = colonists[i];
                    if (p == leader || SquadComponent.SquadOf(p) != sq) continue;
                    if (!squadWide
                        && (SquadComponent.FireteamOf(p) != team
                            || SquadComponent.IsLeader(p))) continue;
                    if (p.Dead || p.Downed || p.InMentalState || p.drafter == null) continue;
                    if (!CommsModule.CanCommand(leader, p)) continue;
                    if (value)
                    {
                        if (p.drafter.Drafted) continue;
                        SquadComponent.RecordPropagatedDraft(leader, p, episode);
                        p.drafter.Drafted = true;
                        CATrace.Pawn(p, "draft-chain ACQUIRED from "
                            + leader.LabelShort
                            + (squadWide ? "" : " (fire team)"),
                            anchor: p.Position,
                            intent: CACombatIntent.Operator(leader, p,
                                CAIntentController.DraftCoordination,
                                episode));
                    }
                }
            }
            finally { chaining = false; }
        }
    }
}
