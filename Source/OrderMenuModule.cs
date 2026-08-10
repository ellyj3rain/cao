using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // The order-menu fork: every group-capable order asks WITH WHO. Each option is
    // preflighted against the same route plan execution will use. The actor remains
    // the player's direct hand and always complies; every other pawn first needs
    // physical delivery, then separately resolves obedience under DR-8.
    public static class OrderMenus
    {
        public delegate void GroupExecute(List<Pawn> team);
        public delegate void PartnerExecute(Pawn partner);
        public delegate string ParticipantEligibility(Pawn participant);

        private sealed class NamedRouteOption
        {
            public Pawn Member;
            public bool RouteReady;
            public string DisabledReason;
        }

        // Opens the with-who fork for an order anchored on the actor.
        public static void OpenWithWho(Pawn actor, Map map, string verb,
            bool combatOrder, GroupExecute execute, int minimumParticipants = 1)
        {
            // The operator's hand wakes a sleeping issuer, exactly as drafting
            // does natively - a command given THROUGH a pawn is a shake of the
            // shoulder. Without this, every relay dies at the unconscious
            // endpoint and only the sleeper's own order survives, silently.
            if (actor != null && !actor.Awake())
            {
                RestUtility.WakeUp(actor);
                CATrace.Pawn(actor, "woken by the operator's command - "
                    + "relays route through a conscious issuer",
                    anchor: actor.Position);
            }
            var opts = new List<FloatMenuOption>();

            if (minimumParticipants <= 1)
                opts.Add(new FloatMenuOption(verb + ": " + actor.LabelShort
                    + " alone", delegate
                {
                    execute(new List<Pawn> { actor });
                }));

            int sq = SquadComponent.SquadOf(actor);
            if (sq > 0)
            {
                // Command authority flows DOWN the org chart: squad scope
                // needs the squad leader's hand, fireteam scope the team
                // leader's (or the squad leader's). A subordinate sees the
                // greyed option and why, not silent power.
                bool squadAuthority = SquadComponent.IsLeader(actor)
                    || CAOrganizationAuthority.HasColonyCommand(actor);
                if (squadAuthority)
                {
                    string squadExclusion;
                    var squadmates = Squadmates(actor, map, 0, combatOrder,
                        out squadExclusion);
                    AddPreflightedGroupOption(opts, actor, verb, "squad " + sq,
                        squadmates, squadExclusion, minimumParticipants,
                        execute);
                }
                else
                {
                    opts.Add(new FloatMenuOption(verb + ": squad " + sq
                        + " (requires the squad leader or a colony command"
                        + " office)", null));
                }
                int ft = SquadComponent.FireteamOf(actor);
                if (ft > 0)
                {
                    if (squadAuthority
                        || SquadComponent.IsFireteamLeader(actor))
                    {
                        string teamExclusion;
                        var teammates = Squadmates(actor, map, ft, combatOrder,
                            out teamExclusion);
                        AddPreflightedGroupOption(opts, actor, verb,
                            "fire team " + (ft == 1 ? "A" : "B"), teammates,
                            teamExclusion, minimumParticipants, execute);
                    }
                    else
                    {
                        opts.Add(new FloatMenuOption(verb + ": fire team "
                            + (ft == 1 ? "A" : "B")
                            + " (requires the team leader)", null));
                    }
                }
            }

            // Named choices are map-wide candidates. The physical route, rather than
            // an arbitrary distance cutoff, decides whether the order can reach one.
            List<NamedRouteOption> named = NamedColonists(actor, map, verb,
                combatOrder, minimumParticipants);
            for (int i = 0; i < named.Count; i++)
            {
                NamedRouteOption choice = named[i];
                Pawn other = choice.Member;
                if (!choice.RouteReady)
                {
                    opts.Add(new FloatMenuOption(verb + ": with " + other.LabelShort
                        + " - unavailable: " + choice.DisabledReason, null));
                    continue;
                }
                opts.Add(new FloatMenuOption(verb + ": with " + other.LabelShort,
                    delegate
                    {
                        execute(Authority.FilterObedient(actor,
                            new List<Pawn> { actor, other }, verb));
                    }));
            }

            if (opts.Count == 0)
            {
                string reason = verb + " requires at least "
                    + minimumParticipants + " eligible colonists; no partner is currently available.";
                opts.Add(new FloatMenuOption(reason, null));
                CATrace.Skip(actor, verb, "no eligible partner at menu-open time");
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        // Pair-only orders do not silently auto-recruit a nearby pawn. The player
        // names the partner, sees the same physical-route preflight used at
        // execution, and the partner resolves obedience before either paired
        // action begins.
        public static void OpenWithPartner(Pawn actor, Map map, string verb,
            bool combatOrder, ParticipantEligibility eligibility,
            PartnerExecute execute)
        {
            var opts = new List<FloatMenuOption>();
            List<NamedRouteOption> named = NamedColonists(actor, map, verb,
                combatOrder, 2, eligibility);
            for (int i = 0; i < named.Count; i++)
            {
                NamedRouteOption choice = named[i];
                Pawn other = choice.Member;
                if (!choice.RouteReady)
                {
                    opts.Add(new FloatMenuOption(verb + ": with "
                        + other.LabelShort + " - unavailable: "
                        + choice.DisabledReason, null));
                    continue;
                }
                opts.Add(new FloatMenuOption(verb + ": with "
                    + other.LabelShort, delegate
                {
                    List<Pawn> accepted = Authority.FilterObedient(actor,
                        new List<Pawn> { actor, other }, verb);
                    if (accepted.Contains(other)) execute(other);
                    else CATrace.Skip(actor, verb,
                        other.LabelShort
                        + " did not accept; no paired order was issued",
                        anchor: actor.Position);
                }));
            }
            if (opts.Count == 0)
                opts.Add(new FloatMenuOption(verb
                    + " requires an eligible partner; none is available.",
                    null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private static void AddPreflightedGroupOption(List<FloatMenuOption> opts,
            Pawn actor, string verb, string groupLabel, List<Pawn> candidates,
            string firstEligibilityFailure, int minimumParticipants,
            GroupExecute execute)
        {
            string firstFailure;
            List<Pawn> routed = PreflightTeam(actor, candidates, verb,
                out firstFailure);
            string label = verb + ": " + groupLabel + " (" + routed.Count + ")";
            if (candidates.Count < minimumParticipants)
            {
                opts.Add(new FloatMenuOption(label + " - unavailable: only "
                    + candidates.Count + " eligible participants; requires "
                    + minimumParticipants
                    + (firstEligibilityFailure != null
                        ? "; " + firstEligibilityFailure
                        : "; no other assigned member is available"), null));
                return;
            }
            if (routed.Count < minimumParticipants)
            {
                opts.Add(new FloatMenuOption(label + " - unavailable: "
                    + (firstFailure ?? "no physical delivery route"), null));
                return;
            }

            List<Pawn> routedAtOpen = routed;
            opts.Add(new FloatMenuOption(label, delegate
            {
                // The route was real at menu-open time. Authority rechecks delivery
                // at click time in case movement or an outage changed it, then
                // adjudicates obedience as a separate event.
                execute(Authority.FilterObedient(actor, routedAtOpen, verb));
            }));
        }

        // Propagation physics v1: a leader whose group order could not reach
        // a member CARRIES the intent and delivers it personally when contact
        // returns - the member then joins the leader's standing arrangement.
        // Information flows through the authority, not through selection.
        public static class CAPendingRelay
        {
            private sealed class Held
            {
                public int MapId;
                public int LeaderId;
                public int MemberId;
                public string Verb;
                public int ExpiryTick;
            }

            private static readonly List<Held> held = new List<Held>();

            public static void Hold(Pawn leader, Pawn member, string verb)
            {
                if (leader?.Map == null || member == null) return;
                for (int i = 0; i < held.Count; i++)
                    if (held[i].LeaderId == leader.thingIDNumber
                        && held[i].MemberId == member.thingIDNumber) return;
                held.Add(new Held
                {
                    MapId = leader.Map.uniqueID,
                    LeaderId = leader.thingIDNumber,
                    MemberId = member.thingIDNumber,
                    Verb = verb,
                    ExpiryTick = Find.TickManager.TicksGame + 15000
                });
            }

            public static void Process(Map map)
            {
                if (held.Count == 0) return;
                int now = Find.TickManager.TicksGame;
                var colonists = map.mapPawns.FreeColonistsSpawned;
                for (int i = held.Count - 1; i >= 0; i--)
                {
                    Held entry = held[i];
                    if (entry.MapId != map.uniqueID) continue;
                    Pawn leader = null, member = null;
                    for (int c = 0; c < colonists.Count; c++)
                    {
                        if (colonists[c].thingIDNumber == entry.LeaderId)
                            leader = colonists[c];
                        if (colonists[c].thingIDNumber == entry.MemberId)
                            member = colonists[c];
                    }
                    if (leader == null || member == null || leader.Dead
                        || now > entry.ExpiryTick)
                    { held.RemoveAt(i); continue; }
                    int kind;
                    IntVec3 cell, watch;
                    var job = CATactical.JobOf(map);
                    if (job == null || !job.TryGetOrder(leader, out kind,
                            out cell, out watch)
                        || kind != LordJob_CATactical.KindHold)
                    {
                        CATrace.Pawn(leader, "held " + entry.Verb
                            + " order for " + member.LabelShort
                            + " LAPSED - the arrangement ended before contact",
                            anchor: leader.Position);
                        held.RemoveAt(i);
                        continue;
                    }
                    if (!member.Awake() || member.Downed
                        || member.InMentalState) continue;
                    CommandRouteReceipt receipt;
                    if (!CommsModule.CanRelayOrder(leader, member,
                        new List<Pawn> { leader, member }, out receipt))
                        continue;
                    var holdComponent = map.GetComponent<HoldMapComponent>();
                    if (holdComponent == null) { held.RemoveAt(i); continue; }
                    IntVec3 slot = IntVec3.Invalid;
                    int limit = GenRadial.NumCellsInRadius(6.9f);
                    for (int r = 0; r < limit; r++)
                    {
                        IntVec3 c2 = cell + GenRadial.RadialPattern[r];
                        if (!c2.InBounds(map) || !c2.Standable(map)) continue;
                        if (!member.CanReach(c2, PathEndMode.OnCell,
                            Danger.Deadly)) continue;
                        bool clash = false;
                        for (int p = 0; p < colonists.Count; p++)
                        {
                            int k2; IntVec3 oc, ow;
                            if (job.TryGetOrder(colonists[p], out k2, out oc,
                                    out ow)
                                && k2 == LordJob_CATactical.KindHold
                                && c2.InHorDistOf(oc, 1.9f))
                            { clash = true; break; }
                        }
                        if (clash) continue;
                        slot = c2;
                        break;
                    }
                    if (!slot.IsValid) continue;
                    bool accepted = watch.IsValid
                        ? holdComponent.OrderHoldWatching(member, slot, watch,
                            CACombatIntent.Operator(leader, member,
                                CAIntentController.Hold))
                        : holdComponent.OrderHold(member, slot,
                            CACombatIntent.Operator(leader, member,
                                CAIntentController.Hold));
                    if (accepted)
                        CATrace.Pawn(member, "held order DELIVERED by "
                            + leader.LabelShort + " on contact ("
                            + entry.Verb + ") - joins the standing "
                            + "arrangement at " + slot,
                            destination: slot, anchor: member.Position);
                    held.RemoveAt(i);
                }
            }
        }

        private static List<Pawn> PreflightTeam(Pawn actor, List<Pawn> candidates,
            string verb, out string firstFailure)
        {
            var routed = new List<Pawn> { actor };
            firstFailure = null;
            int routeSkipped = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn member = candidates[i];
                if (member == actor) continue;
                CommandRouteReceipt receipt;
                if (CommsModule.CanRelayOrder(actor, member, candidates, out receipt))
                {
                    routed.Add(member);
                    CATrace.Pawn(member, verb + " selection ROUTE READY via "
                        + receipt.Channel.ToString().ToLowerInvariant() + " - "
                        + receipt.Detail + "; obedience not yet evaluated",
                        anchor: actor.Position);
                }
                else
                {
                    string reason = MenuRouteReason(receipt);
                    if (firstFailure == null) firstFailure = reason;
                    CATrace.Skip(member, verb + " selection",
                        "route blocked [" + receipt.Failure + "]: "
                        + receipt.Detail + "; obedience not evaluated",
                        anchor: actor.Position);
                    routeSkipped++;
                }
            }
            // The operator sees what the log sees: a partially-routed group
            // order is announced ON SCREEN at issue time, never discovered
            // mid-battle as "they're doing some other bullshit." And the
            // LEADER CARRIES the undelivered intent - unreachable is not
            // dropped; he delivers it himself when contact returns.
            if (routeSkipped > 0)
            {
                Messages.Message(verb + ": " + routeSkipped
                    + " squadmate(s) unreachable - " + firstFailure
                    + "; " + actor.LabelShort + " carries the order and will "
                    + "deliver on contact", actor,
                    MessageTypeDefOf.CautionInput, false);
                for (int i = 0; i < candidates.Count; i++)
                {
                    Pawn member = candidates[i];
                    if (member == actor || routed.Contains(member)) continue;
                    CAPendingRelay.Hold(actor, member, verb);
                }
            }
            return routed;
        }

        private static string ParticipantExclusion(Pawn member, Map map,
            bool combatOrder)
        {
            if (member == null || !member.Spawned || member.Map != map)
                return "not spawned on this map";
            if (member.Dead) return "dead";
            if (member.Downed) return "downed";
            if (member.InMentalState) return "in a mental state";
            if (combatOrder && member.WorkTagIsDisabled(WorkTags.Violent))
                return "incapable of violence";
            if (combatOrder
                && member.DevelopmentalStage != DevelopmentalStage.Adult)
                return "not an adult combatant";
            // Congruency: a member actively trading fire from a position is
            // not silently ripped out by a group order. The leader can still
            // move them individually - that is a deliberate hand, not a
            // side effect.
            if (combatOrder && ActivelyEngaged(member))
                return "engaged with the enemy (order individually to displace)";
            return null;
        }

        private static bool ActivelyEngaged(Pawn member)
        {
            var busy = member.stances?.curStance as Stance_Busy;
            var aimTarget = busy?.focusTarg.Thing as Pawn;
            if (aimTarget != null && aimTarget.HostileTo(member)) return true;
            Job current = member.CurJob;
            if (current == null) return false;
            if (current.def != JobDefOf.AttackStatic
                && current.def != JobDefOf.AttackMelee
                && current.def != JobDefOf.Wait_Combat) return false;
            var jobTarget = current.targetA.Thing as Pawn;
            return jobTarget != null && jobTarget.HostileTo(member);
        }

        private static string MenuRouteReason(CommandRouteReceipt receipt)
        {
            if (receipt == null) return "no physical delivery receipt";
            if (receipt.Failure == CommandRouteFailure.MentalNetworkOutage)
                return "mental network offline (map electricity disabled); no local voice or gesture";
            if (receipt.Failure == CommandRouteFailure.CapacityExceeded)
                return "provisional voice/radio command span exhausted";
            return receipt.Detail ?? receipt.Failure.ToString();
        }

        // Squad or fire-team members including the operator-direct actor. Physical
        // delivery and obedience are resolved later; only participation incapacity
        // is excluded here.
        private static List<Pawn> Squadmates(Pawn actor, Map map, int fireteam,
            bool combatOrder, out string firstExclusion)
        {
            var result = new List<Pawn> { actor };
            firstExclusion = null;
            int sq = SquadComponent.SquadOf(actor);
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                var m = colonists[i];
                if (m == actor) continue;
                if (SquadComponent.SquadOf(m) != sq) continue;
                if (fireteam > 0 && SquadComponent.FireteamOf(m) != fireteam) continue;
                string exclusion = ParticipantExclusion(m, map, combatOrder);
                if (exclusion != null)
                {
                    if (firstExclusion == null)
                        firstExclusion = m.LabelShort + " is " + exclusion;
                    continue;
                }
                result.Add(m);
            }
            return result;
        }

        private static List<NamedRouteOption> NamedColonists(Pawn actor, Map map,
            string verb, bool combatOrder, int minimumParticipants,
            ParticipantEligibility extraEligibility = null)
        {
            var result = new List<NamedRouteOption>();
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn member = colonists[i];
                if (member == actor) continue;
                var option = new NamedRouteOption { Member = member };
                string exclusion = ParticipantExclusion(member, map, combatOrder);
                if (exclusion != null)
                {
                    option.DisabledReason = exclusion;
                    CATrace.Skip(member, verb + " selection",
                        "excluded: " + exclusion, anchor: actor.Position);
                }
                else if (extraEligibility != null
                    && (exclusion = extraEligibility(member)) != null)
                {
                    option.DisabledReason = exclusion;
                    CATrace.Skip(member, verb + " selection",
                        "excluded: " + exclusion, anchor: actor.Position);
                }
                else if (minimumParticipants > 2)
                {
                    option.DisabledReason = "a named pair has 2 participants; requires "
                        + minimumParticipants;
                    CATrace.Skip(member, verb + " selection",
                        option.DisabledReason, anchor: actor.Position);
                }
                else
                {
                    var pair = new List<Pawn> { actor, member };
                    CommandRouteReceipt receipt;
                    option.RouteReady = CommsModule.CanRelayOrder(actor, member,
                        pair, out receipt);
                    if (option.RouteReady)
                    {
                        CATrace.Pawn(member, verb + " selection ROUTE READY via "
                            + receipt.Channel.ToString().ToLowerInvariant() + " - "
                            + receipt.Detail + "; obedience not yet evaluated",
                            anchor: actor.Position);
                    }
                    else
                    {
                        option.DisabledReason = MenuRouteReason(receipt);
                        CATrace.Skip(member, verb + " selection",
                            "route blocked [" + receipt.Failure + "]: "
                            + receipt.Detail + "; obedience not evaluated",
                            anchor: actor.Position);
                    }
                }
                result.Add(option);
            }
            result.Sort(delegate (NamedRouteOption a, NamedRouteOption b)
            {
                if (a.RouteReady != b.RouteReady) return a.RouteReady ? -1 : 1;
                int distance = actor.Position.DistanceTo(a.Member.Position)
                    .CompareTo(actor.Position.DistanceTo(b.Member.Position));
                return distance != 0 ? distance
                    : a.Member.thingIDNumber.CompareTo(b.Member.thingIDNumber);
            });
            return result;
        }

        // Synchronized ambush is a composition task rather than a one-click pair
        // choice. Keep the operator-direct actor in the plan, expose assigned-unit
        // presets when they actually exist, and let the player add or remove any
        // route-ready named participant before authority is adjudicated once.
        public static void OpenAmbushComposer(Pawn actor, Map map,
            IntVec3 near)
        {
            if (actor == null || map == null || !near.IsValid) return;
            Find.WindowStack.Add(new Dialog_AmbushParticipants(actor, map,
                near));
        }

        private sealed class Dialog_AmbushParticipants : Window
        {
            private readonly Pawn actor;
            private readonly Map map;
            private readonly IntVec3 near;
            private readonly List<NamedRouteOption> candidates;
            private readonly HashSet<int> selected = new HashSet<int>();
            private Vector2 scroll;

            public override Vector2 InitialSize
            {
                get { return new Vector2(720f, 620f); }
            }

            public Dialog_AmbushParticipants(Pawn actor, Map map,
                IntVec3 near)
            {
                this.actor = actor;
                this.map = map;
                this.near = near;
                doCloseX = true;
                closeOnClickedOutside = false;
                absorbInputAroundWindow = true;
                draggable = true;
                selected.Add(actor.thingIDNumber);
                candidates = NamedColonists(actor, map,
                    "Synchronized ambush", true, 2);
            }

            public override void DoWindowContents(Rect inRect)
            {
                if (actor == null || actor.Dead || !actor.Spawned
                    || actor.Map != map || Find.CurrentMap != map)
                {
                    Widgets.Label(inRect,
                        "The ambush issuer is no longer available on this map.");
                    return;
                }

                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f),
                    "Compose synchronized ambush");
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(inRect.x, inRect.y + 38f,
                    inRect.width, 44f),
                    actor.LabelShortCap + " is the operator-direct participant. "
                    + "Add route-ready colonists individually or load an assigned "
                    + "unit preset; obedience is resolved only when you commit.");

                float presetY = inRect.y + 88f;
                float buttonW = (inRect.width - 18f) / 4f;
                if (Widgets.ButtonText(new Rect(inRect.x, presetY,
                        buttonW, 30f), "Issuer only"))
                    SelectIssuerOnly();

                int squad = SquadComponent.SquadOf(actor);
                int fireteam = SquadComponent.FireteamOf(actor);
                bool haveFireteam = squad > 0 && fireteam > 0;
                bool haveSquad = squad > 0;

                GUI.color = haveFireteam ? Color.white : Color.gray;
                if (Widgets.ButtonText(new Rect(inRect.x + buttonW + 6f,
                        presetY, buttonW, 30f), haveFireteam
                            ? "My fire team" : "No fire team assigned")
                    && haveFireteam)
                    SelectAssigned(squad, fireteam);

                GUI.color = haveSquad ? Color.white : Color.gray;
                if (Widgets.ButtonText(new Rect(inRect.x + (buttonW + 6f) * 2f,
                        presetY, buttonW, 30f), haveSquad
                            ? "My squad" : "No squad assigned")
                    && haveSquad)
                    SelectAssigned(squad, 0);

                GUI.color = Color.white;
                if (Widgets.ButtonText(new Rect(inRect.x + (buttonW + 6f) * 3f,
                        presetY, buttonW, 30f), "All route-ready"))
                    SelectAllRouteReady();

                float listY = presetY + 40f;
                Rect outRect = new Rect(inRect.x, listY, inRect.width,
                    inRect.height - listY - 62f);
                float viewHeight = 38f + candidates.Count * 34f;
                Rect viewRect = new Rect(0f, 0f, outRect.width - 18f,
                    Mathf.Max(outRect.height, viewHeight));
                Widgets.BeginScrollView(outRect, ref scroll, viewRect);

                DrawLockedActorRow(new Rect(0f, 0f, viewRect.width, 30f));
                float y = 38f;
                for (int i = 0; i < candidates.Count; i++)
                {
                    NamedRouteOption option = candidates[i];
                    Pawn member = option.Member;
                    Rect row = new Rect(0f, y, viewRect.width, 30f);
                    bool included = member != null
                        && selected.Contains(member.thingIDNumber);
                    string state = option.RouteReady
                        ? (included ? "[x] " : "[ ] ")
                            + member.LabelShortCap
                            + " - route ready; click to "
                            + (included ? "remove" : "add")
                        : "[-] " + (member != null
                            ? member.LabelShortCap : "Unavailable colonist")
                            + " - unavailable: " + option.DisabledReason;
                    GUI.color = option.RouteReady ? Color.white : Color.gray;
                    if (Widgets.ButtonText(row, state) && option.RouteReady)
                    {
                        if (included) selected.Remove(member.thingIDNumber);
                        else selected.Add(member.thingIDNumber);
                    }
                    y += 34f;
                }
                GUI.color = Color.white;
                Widgets.EndScrollView();

                int count = SelectedCount();
                Rect commit = new Rect(inRect.x, inRect.yMax - 46f,
                    inRect.width, 38f);
                bool canCommit = count >= 2;
                GUI.color = canCommit ? Color.white : Color.gray;
                string commitLabel = canCommit
                    ? "Establish synchronized ambush with " + count
                    : "Add at least one participant to synchronize";
                if (Widgets.ButtonText(commit, commitLabel) && canCommit)
                    Commit();
                GUI.color = Color.white;
            }

            private void DrawLockedActorRow(Rect row)
            {
                Widgets.DrawHighlight(row);
                Widgets.Label(new Rect(row.x + 8f, row.y + 5f,
                    row.width - 16f, row.height - 8f),
                    "[x] " + actor.LabelShortCap
                    + " - operator-direct issuer and participant");
            }

            private void SelectIssuerOnly()
            {
                selected.Clear();
                selected.Add(actor.thingIDNumber);
            }

            private void SelectAllRouteReady()
            {
                SelectIssuerOnly();
                for (int i = 0; i < candidates.Count; i++)
                    if (candidates[i].RouteReady
                        && candidates[i].Member != null)
                        selected.Add(candidates[i].Member.thingIDNumber);
            }

            private void SelectAssigned(int squad, int fireteam)
            {
                SelectIssuerOnly();
                for (int i = 0; i < candidates.Count; i++)
                {
                    NamedRouteOption option = candidates[i];
                    Pawn member = option.Member;
                    if (!option.RouteReady || member == null
                        || SquadComponent.SquadOf(member) != squad
                        || fireteam > 0
                            && SquadComponent.FireteamOf(member) != fireteam)
                        continue;
                    selected.Add(member.thingIDNumber);
                }
            }

            private int SelectedCount()
            {
                int count = selected.Contains(actor.thingIDNumber) ? 1 : 0;
                for (int i = 0; i < candidates.Count; i++)
                    if (candidates[i].Member != null
                        && selected.Contains(
                            candidates[i].Member.thingIDNumber)) count++;
                return count;
            }

            private void Commit()
            {
                var requested = new List<Pawn> { actor };
                for (int i = 0; i < candidates.Count; i++)
                {
                    NamedRouteOption option = candidates[i];
                    if (option.RouteReady && option.Member != null
                        && selected.Contains(option.Member.thingIDNumber))
                        requested.Add(option.Member);
                }
                string routeFailure;
                List<Pawn> routed = PreflightTeam(actor, requested,
                    "Synchronized ambush commit", out routeFailure);
                List<Pawn> accepted = Authority.FilterObedient(actor,
                    routed, "Synchronized ambush");
                GroupAmbush(actor, accepted, near, map);
                Close();
            }
        }

        // ---- Group executors: the same primitives the autonomous modes use ----

        public static void IndividualAmbush(Pawn issuer, IntVec3 near,
            Map map)
        {
            if (issuer == null || map == null) return;
            IntVec3 spot;
            if (!HiddenRegistry.TryFindConcealmentNearExcluding(map, issuer,
                    near, 7.9f, new List<IntVec3>(), out spot))
            {
                Messages.Message("No reachable concealment slot for "
                    + issuer.LabelShort + ".", new LookTargets(near, map),
                    MessageTypeDefOf.RejectInput, false);
                CATrace.Skip(issuer, "individual ambush",
                    "no reachable concealment slot",
                    destination: near, anchor: issuer.Position);
                return;
            }
            int episode = CACombatIntent.NewEpisode();
            HiddenRegistry.BeginAmbushGroup(new List<Pawn> { issuer }, map,
                episode);
            CAIntentContext context = CACombatIntent.Operator(issuer, issuer,
                CAIntentController.Ambush, episode);
            if (!HiddenRegistry.OrderAmbush(issuer, spot, context))
            {
                HiddenRegistry.AbortAmbushGroup(map, episode,
                    "individual establishment failed");
                Messages.Message("The individual ambush could not be established.",
                    new LookTargets(near, map),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            Messages.Message(issuer.LabelShort
                + " moving into an individual ambush.",
                new LookTargets(near, map), MessageTypeDefOf.SilentInput,
                false);
        }

        // Spread the team into concealment around the point, one spot each.
        public static void GroupAmbush(Pawn issuer, List<Pawn> team,
            IntVec3 near, Map map)
        {
            if (team == null || team.Count < 2)
            {
                Messages.Message("A synchronized ambush requires at least two accepted colonists.",
                    new LookTargets(near, map), MessageTypeDefOf.RejectInput, false);
                CATrace.Skip(issuer, "synchronized ambush",
                    "route-ready cardinality collapsed below two after obedience",
                    destination: near, anchor: issuer.Position);
                return;
            }
            var used = new List<IntVec3>();
            var placedPawns = new List<Pawn>();
            var placedSpots = new List<IntVec3>();
            for (int i = 0; i < team.Count; i++)
            {
                var p = team[i];
                IntVec3 spot;
                if (!HiddenRegistry.TryFindConcealmentNearExcluding(map, p,
                    near, 7.9f, used, out spot))
                {
                    Messages.Message("The synchronized ambush cannot be established: "
                        + p.LabelShort + " has no reachable concealment slot.",
                        new LookTargets(near, map), MessageTypeDefOf.RejectInput, false);
                    CATrace.Skip(p, "synchronized ambush manifest",
                        "no reachable unshared concealment slot; no member was ordered",
                        destination: near, anchor: p.Position);
                    return;
                }
                used.Add(spot);
                placedPawns.Add(p);
                placedSpots.Add(spot);
                CATrace.Pawn(p, "synchronized ambush manifest SLOT " + spot,
                    destination: spot, anchor: p.Position);
            }
            int placed = placedPawns.Count;
            int episode = CACombatIntent.NewEpisode();
            HiddenRegistry.BeginAmbushGroup(placedPawns, map, episode);
            bool established = true;
            for (int i = 0; i < placedPawns.Count; i++)
                if (!HiddenRegistry.OrderAmbush(placedPawns[i], placedSpots[i],
                        CACombatIntent.Operator(issuer, placedPawns[i],
                            CAIntentController.Ambush, episode)))
                {
                    established = false;
                    break;
                }
            if (!established)
            {
                HiddenRegistry.AbortAmbushGroup(map, episode,
                    "transactional establishment failed; every member released");
                Messages.Message("The synchronized ambush could not be established.",
                    new LookTargets(near, map),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            Messages.Message(placed + " moving into one synchronized ambush.",
                new LookTargets(near, map),
                MessageTypeDefOf.SilentInput, false);
        }

        // Spread the team into pure hides - concealment, no springing.
        public static void GroupHide(Pawn issuer, List<Pawn> team,
            IntVec3 near, Map map)
        {
            var used = new List<IntVec3>();
            int placed = 0;
            int episode = CACombatIntent.NewEpisode();
            for (int i = 0; i < team.Count; i++)
            {
                var p = team[i];
                IntVec3 spot;
                if (!HiddenRegistry.TryFindConcealmentNearExcluding(map, p, near, 7.9f, used, out spot)) continue;
                used.Add(spot);
                if (HiddenRegistry.OrderHide(p, spot,
                    CACombatIntent.Operator(issuer, p,
                        CAIntentController.Hide, episode))) placed++;
            }
            if (placed > 1)
                Messages.Message(placed + " going to ground.", new LookTargets(near, map),
                    MessageTypeDefOf.SilentInput, false);
            else if (placed == 0)
                Messages.Message("No concealment there for anyone.", MessageTypeDefOf.RejectInput, false);
        }

        // Deploy a team to hold slots along a painted command layer. Slots
        // follow the drawn shape; the team is ordered by squad and fire team
        // first, so fire teams occupy contiguous stretches of the line and
        // buddies end up beside each other. Each holder watches their own
        // nearest known contact when they have one.
        public static void DeployToPaintedLine(Pawn issuer, List<Pawn> team,
            CAOverlayKind kind, Map map)
        {
            var overlay = CAOverlayMapComponent.For(map);
            var hold = map.GetComponent<HoldMapComponent>();
            if (overlay == null || hold == null || team == null
                || team.Count == 0) return;
            // The line is a goal, not ground to occupy: solve fighting
            // positions that serve it - cover on the friendly side, fields of
            // fire across. The issuer's threat picture orients the enemy side.
            IntVec3 threatHint = IntVec3.Invalid;
            var issuerKnow = KnowledgeMapComponent.For(map);
            if (issuerKnow != null && issuer != null)
            {
                var known = issuerKnow.FreshContacts(issuer);
                float nearestContact = float.MaxValue;
                for (int c = 0; c < known.Count; c++)
                {
                    if (!known[c].Cell.IsValid) continue;
                    float d = known[c].Cell.DistanceToSquared(issuer.Position);
                    if (d < nearestContact)
                    { nearestContact = d; threatHint = known[c].Cell; }
                }
            }
            var solutions = CALineInterpreter.Solve(map, kind, team.Count,
                threatHint);
            var slots = new List<IntVec3>(solutions.Count);
            for (int i = 0; i < solutions.Count; i++)
                slots.Add(solutions[i].Cell);
            if (slots.Count == 0)
            {
                Messages.Message("The painted line has no standable ground.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            var ordered = new List<Pawn>(team);
            ordered.Sort(delegate (Pawn a, Pawn b)
            {
                int squadCompare = SquadComponent.SquadOf(a)
                    .CompareTo(SquadComponent.SquadOf(b));
                if (squadCompare != 0) return squadCompare;
                int teamCompare = SquadComponent.FireteamOf(a)
                    .CompareTo(SquadComponent.FireteamOf(b));
                if (teamCompare != 0) return teamCompare;
                return a.thingIDNumber.CompareTo(b.thingIDNumber);
            });
            var know = KnowledgeMapComponent.For(map);
            var assignedPawns = new HashSet<int>();
            var usedSlots = new HashSet<int>();
            int placed = 0;
            int episode = CACombatIntent.NewEpisode();
            for (int i = 0; i < ordered.Count; i++)
            {
                Pawn p = ordered[i];
                int slotIndex = -1;
                for (int offset = 0; offset < slots.Count; offset++)
                {
                    int candidate = (i + offset) % slots.Count;
                    if (usedSlots.Contains(candidate)) continue;
                    if (!p.CanReach(slots[candidate], PathEndMode.OnCell,
                        Danger.Deadly)) continue;
                    slotIndex = candidate;
                    break;
                }
                if (slotIndex < 0) continue;
                IntVec3 slot = slots[slotIndex];
                IntVec3 watch = IntVec3.Invalid;
                if (know != null)
                {
                    var contacts = know.FreshContacts(p);
                    float nearest = float.MaxValue;
                    for (int c = 0; c < contacts.Count; c++)
                    {
                        if (!contacts[c].Cell.IsValid) continue;
                        float d = slot.DistanceTo(contacts[c].Cell);
                        if (d < nearest)
                        { nearest = d; watch = contacts[c].Cell; }
                    }
                }
                // No personal contact: watch across the line, where the
                // interpreter says the fight comes from.
                if (!watch.IsValid && slotIndex < solutions.Count)
                    watch = solutions[slotIndex].Watch;
                bool accepted = watch.IsValid
                    ? hold.OrderHoldWatching(p, slot, watch,
                        CACombatIntent.Operator(issuer, p,
                            CAIntentController.Hold, episode))
                    : hold.OrderHold(p, slot,
                        CACombatIntent.Operator(issuer, p,
                            CAIntentController.Hold, episode));
                if (accepted)
                {
                    usedSlots.Add(slotIndex);
                    assignedPawns.Add(p.thingIDNumber);
                    placed++;
                }
            }
            // One new group intent, like GroupHold: a pawn that could not take
            // a slot must not silently keep an old Hold behind the new line.
            for (int i = 0; i < team.Count; i++)
                if (team[i] != null
                    && !assignedPawns.Contains(team[i].thingIDNumber)
                    && hold.IsHolding(team[i])) hold.Release(team[i]);
            string layerName = kind == CAOverlayKind.DefensiveLine
                ? "defensive line"
                : kind == CAOverlayKind.FallbackLine
                ? "fallback line" : "offensive line";
            if (placed > 0)
                Messages.Message(placed + " deploying along the painted "
                    + layerName + ".", new LookTargets(slots[0], map),
                    MessageTypeDefOf.SilentInput, false);
            else
                Messages.Message("No selected fighter can reach the painted "
                    + layerName + ".", MessageTypeDefOf.RejectInput, false);
        }

        // Right-click a painted line to join it: the selected pawn alone
        // takes the nearest open interpreter position to the clicked cell.
        public static void JoinPaintedLineAt(Pawn actor, CAOverlayKind kind,
            IntVec3 clickedCell, Map map)
        {
            var overlay = CAOverlayMapComponent.For(map);
            var hold = map.GetComponent<HoldMapComponent>();
            if (overlay == null || hold == null || actor == null) return;
            int lineLength = overlay.CellsOf(kind).Count;
            int stationCount = UnityEngine.Mathf.Clamp(lineLength / 8, 2, 8);
            IntVec3 hint = IntVec3.Invalid;
            var know = KnowledgeMapComponent.For(map);
            if (know != null)
            {
                var contacts = know.FreshContacts(actor);
                float nearestContact = float.MaxValue;
                for (int c = 0; c < contacts.Count; c++)
                {
                    if (!contacts[c].Cell.IsValid) continue;
                    float d = contacts[c].Cell.DistanceToSquared(clickedCell);
                    if (d < nearestContact)
                    { nearestContact = d; hint = contacts[c].Cell; }
                }
            }
            var solutions = CALineInterpreter.Solve(map, kind, stationCount,
                hint);
            // Occupied stations: any standing hold anchored within 1.9.
            var holdCells = new List<IntVec3>();
            var job = CATactical.JobOf(map);
            if (job != null)
            {
                var colonists = map.mapPawns.FreeColonistsSpawned;
                for (int i = 0; i < colonists.Count; i++)
                {
                    int k2;
                    IntVec3 cell, watch;
                    if (job.TryGetOrder(colonists[i], out k2, out cell,
                            out watch)
                        && k2 == LordJob_CATactical.KindHold)
                        holdCells.Add(cell);
                }
            }
            CALinePosition best = default(CALinePosition);
            float bestDistance = float.MaxValue;
            bool found = false;
            for (int s = 0; s < solutions.Count; s++)
            {
                bool taken = false;
                for (int h = 0; h < holdCells.Count; h++)
                    if (solutions[s].Cell.InHorDistOf(holdCells[h], 1.9f))
                    { taken = true; break; }
                if (taken) continue;
                if (!actor.CanReach(solutions[s].Cell, PathEndMode.OnCell,
                    Danger.Deadly)) continue;
                float d = solutions[s].Cell.DistanceTo(clickedCell);
                if (d < bestDistance)
                { bestDistance = d; best = solutions[s]; found = true; }
            }
            if (!found)
            {
                Messages.Message(actor.LabelShort
                    + " cannot reach an open position on that line.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            var context = CACombatIntent.Operator(actor, actor,
                CAIntentController.Hold);
            bool accepted = best.Watch.IsValid
                ? hold.OrderHoldWatching(actor, best.Cell, best.Watch, context)
                : hold.OrderHold(actor, best.Cell, context);
            if (accepted)
                Messages.Message(actor.LabelShort + " joins the line.",
                    new LookTargets(best.Cell, map),
                    MessageTypeDefOf.SilentInput, false);
        }

        // Spread holds clustered on the point, 2-cell spacing.
        public static void GroupHold(Pawn issuer, List<Pawn> team,
            IntVec3 center, Map map)
        {
            var hold = map.GetComponent<HoldMapComponent>();
            if (hold == null) return;
            var used = new List<IntVec3>();
            var assignedPawns = new HashSet<int>();
            int placed = 0;
            int episode = CACombatIntent.NewEpisode();
            for (int i = 0; i < team.Count; i++)
            {
                IntVec3 slot = IntVec3.Invalid;
                int limit = GenRadial.NumCellsInRadius(6.9f);
                for (int r = 0; r < limit; r++)
                {
                    var c = center + GenRadial.RadialPattern[r];
                    if (!c.InBounds(map) || !c.Standable(map)) continue;
                    if (!team[i].CanReach(c, PathEndMode.OnCell,
                        Danger.Deadly)) continue;
                    bool clash = false;
                    for (int j = 0; j < used.Count; j++)
                        if (c.InHorDistOf(used[j], 1.9f)) { clash = true; break; }
                    if (clash) continue;
                    slot = c;
                    break;
                }
                if (!slot.IsValid) continue;
                if (hold.OrderHold(team[i], slot,
                    CACombatIntent.Operator(issuer, team[i],
                        CAIntentController.Hold, episode)))
                {
                    used.Add(slot);
                    assignedPawns.Add(team[i].thingIDNumber);
                    placed++;
                }
            }
            // The right-click formation is one new group intent, just like a painted
            // line. A pawn that cannot accept a new slot must not silently keep an old
            // Hold after its teammates have been reassigned around a different point.
            for (int i = 0; i < team.Count; i++)
                if (team[i] != null
                    && !assignedPawns.Contains(team[i].thingIDNumber)
                    && hold.IsHolding(team[i])) hold.Release(team[i]);
            if (placed > 1)
                Messages.Message(placed + " holding the position.", new LookTargets(center, map),
                    MessageTypeDefOf.SilentInput, false);
            else if (placed == 0)
                Messages.Message("No selected fighter can reach a hold near that point.",
                    MessageTypeDefOf.RejectInput, false);
        }
    }
}
