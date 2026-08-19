using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonistAwareness
{
    public enum CommunicationChannel
    {
        None,
        Voice,
        Radio,
        Mental,
        Gesture
    }

    public enum CommandRouteFailure
    {
        None,
        NoPhysicalRoute,
        CapacityExceeded,
        DifferentMapOrFaction,
        EndpointUnavailable,
        MentalNetworkOutage
    }

    // One inspectable result for one attempted order delivery. Authority consumes
    // this receipt only after physical delivery succeeds; refusal is deliberately
    // not folded into route failure.
    public sealed class CommandRouteReceipt
    {
        public bool Delivered;
        public CommandRouteFailure Failure;
        public CommunicationChannel Channel;
        public Pawn From;
        public Pawn To;
        public Pawn Relay;
        public bool FromHasActiveMechlink;
        public bool ToHasActiveMechlink;
        public bool NetworkOutage;
        public bool GestureAvailable;
        public bool VoiceAvailable;
        public bool RadioAvailable;
        public bool MentalAvailable;
        public string Detail;
    }

    // Human communication has three distinct physical media.
    //
    // Voice is local and recipient-uncapped. Radio uses only the three literal
    // mechanitor headsets (Airwire, Array, Integrator) at both endpoints. A
    // mechlink is not a radio: every same-faction spawned pawn with an active
    // mechlink shares one map-wide silent mental network while CA comms is enabled.
    // Native mech bandwidth continues to govern mechanitors and their mechs; it
    // does not allocate human mental recipients. An engine-reported electricity
    // outage, including Solar Flare, takes the remote network offline.
    //
    // The physical channel and a leader's reliable command span remain separate.
    // Hearing a report does not prove that a leader can competently direct every
    // pawn on the net.
    public static class CommsModule
    {
        public const float VoiceRange = 15f;
        public const float ConcealedVoiceRange = 5f;

        private static readonly HashSet<string> HeadsetDefNames = new HashSet<string>
        {
            "Apparel_AirwireHeadset",
            "Apparel_ArrayHeadset",
            "Apparel_IntegratorHeadset"
        };

        private static StatDef bandwidthStat;
        private static HediffDef mechlinkDef;
        private static bool defsResolved;
        private static int topologyCacheTick = int.MinValue;
        private static int topologyCacheFrame = int.MinValue;
        private static readonly Dictionary<Pawn, List<Pawn>> RadioPeerCache
            = new Dictionary<Pawn, List<Pawn>>();

        private static void ResolveDefs()
        {
            if (defsResolved) return;
            bandwidthStat = DefDatabase<StatDef>.GetNamedSilentFail("MechBandwidth");
            mechlinkDef = DefDatabase<HediffDef>.GetNamedSilentFail("MechlinkImplant");
            defsResolved = true;
        }

        private static void EnsureTopologyCache()
        {
            int tick = Find.TickManager != null
                ? Find.TickManager.TicksGame : int.MinValue;
            int frame = UnityEngine.Time.frameCount;
            if (topologyCacheTick == tick && topologyCacheFrame == frame) return;
            topologyCacheTick = tick;
            topologyCacheFrame = frame;
            RadioPeerCache.Clear();
        }

        // Squad assignments can change through UI while the game is paused, when
        // TicksGame does not advance. Mutators call this so their new topology is
        // visible to command and debug queries immediately in the same frame.
        public static void InvalidateTopologyCache()
        {
            topologyCacheTick = int.MinValue;
            topologyCacheFrame = int.MinValue;
            RadioPeerCache.Clear();
        }

        private static float OffsetFor(ThingDef def, StatDef stat)
        {
            if (def == null || stat == null || def.equippedStatOffsets == null) return 0f;
            for (int i = 0; i < def.equippedStatOffsets.Count; i++)
            {
                StatModifier offset = def.equippedStatOffsets[i];
                if (offset.stat == stat) return offset.value;
            }
            return 0f;
        }

        private static Apparel HeadsetOf(Pawn pawn)
        {
            ResolveDefs();
            if (pawn == null || pawn.apparel == null) return null;
            Apparel best = null;
            float bestBandwidth = 0f;
            List<Apparel> worn = pawn.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++)
            {
                Apparel apparel = worn[i];
                if (apparel == null || apparel.def == null
                    || !HeadsetDefNames.Contains(apparel.def.defName)) continue;
                float bandwidth = OffsetFor(apparel.def, bandwidthStat);
                if (best == null || bandwidth > bestBandwidth)
                {
                    best = apparel;
                    bestBandwidth = bandwidth;
                }
            }
            return best;
        }

        public static float HeadsetBandwidth(Pawn pawn)
        {
            Apparel headset = HeadsetOf(pawn);
            return headset != null ? OffsetFor(headset.def, bandwidthStat) : 0f;
        }

        public static int RadioLineCapacity(Pawn pawn)
        {
            return Math.Max(0, (int)HeadsetBandwidth(pawn));
        }

        public static string HeadsetLabel(Pawn pawn)
        {
            Apparel headset = HeadsetOf(pawn);
            return headset != null ? headset.LabelShort : "none";
        }

        public static bool HasHeadset(Pawn pawn)
        {
            return HeadsetOf(pawn) != null;
        }

        public static bool HasMechlink(Pawn pawn)
        {
            ResolveDefs();
            return pawn != null && mechlinkDef != null && pawn.health != null
                && pawn.health.hediffSet != null
                && pawn.health.hediffSet.HasHediff(mechlinkDef);
        }

        public static bool HasActiveMechlink(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && !pawn.Dead && HasMechlink(pawn);
        }

        // RimWorld's own condition manager is authoritative here. Solar Flare is
        // a GameCondition_DisableElectricity, and compatible conditions can expose
        // the same outage without CA matching a particular Def name.
        public static bool NetworkOutage(Map map)
        {
            return map != null && map.gameConditionManager != null
                && map.gameConditionManager.ElectricityDisabled(map);
        }

        // Native total bandwidth includes the mechlink baseline and legitimate
        // mechanitor enhancements. This remains useful diagnostic evidence for the
        // native mech system, but is not a human mental-route limit.
        public static int FreeMentalBandwidth(Pawn pawn)
        {
            ResolveDefs();
            if (!HasMechlink(pawn) || bandwidthStat == null) return 0;
            if (pawn.mechanitor != null)
                return Math.Max(0, pawn.mechanitor.TotalBandwidth - pawn.mechanitor.UsedBandwidth);
            return Math.Max(0, (int)pawn.GetStatValue(bandwidthStat));
        }

        // Compatibility name retained for older callers. It now means an actual
        // human communications endpoint, not every source of MechBandwidth.
        public static bool HasComms(Pawn pawn)
        {
            return HasHeadset(pawn) || HasMechlink(pawn);
        }

        // Headset-radio reports follow the force structure: members use their
        // fire-team lead; fire-team leads talk to their members, each other, and the
        // squad leader; loose members use the squad leader. A newly acquired direct
        // contact may use the member-to-squad-leader mayday line. The synchronized
        // mental network is already shared across its same-faction map endpoints.
        public static bool TryGetKnowledgeChannel(Pawn teller, Pawn listener,
            float voiceRange, out CommunicationChannel channel)
        {
            return TryGetKnowledgeChannel(teller, listener, voiceRange,
                strategicEscalation: false,
                allowRepresentedCoResidents: false, out channel);
        }

        // The optional broader epistemic mode may carry ordinary reports
        // between people who live at the same represented independent site.
        // This leaves the tactical, command, welfare, and default knowledge
        // routes on their existing same-faction contract.
        public static bool TryGetBroaderKnowledgeChannel(Pawn teller,
            Pawn listener, float voiceRange,
            out CommunicationChannel channel)
        {
            return TryGetKnowledgeChannel(teller, listener, voiceRange,
                strategicEscalation: false,
                allowRepresentedCoResidents: true, out channel);
        }

        public static bool TryGetStrategicChannel(Pawn teller, Pawn listener,
            float voiceRange, out CommunicationChannel channel)
        {
            return TryGetKnowledgeChannel(teller, listener, voiceRange,
                strategicEscalation: true,
                allowRepresentedCoResidents: false, out channel);
        }

        private static bool TryGetKnowledgeChannel(Pawn teller, Pawn listener,
            float voiceRange, bool strategicEscalation,
            bool allowRepresentedCoResidents,
            out CommunicationChannel channel)
        {
            channel = CommunicationChannel.None;
            bool sameFaction = SameFactionMap(teller, listener);
            if (!sameFaction && (!allowRepresentedCoResidents
                || !SameRepresentedIndependentSite(teller, listener)))
                return false;

            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings != null && settings.commsSystem
                && HasMentalLine(teller, listener))
            {
                channel = CommunicationChannel.Mental;
                return true;
            }

            // Voice is a physical broadcast. It is not assigned a recipient count
            // and does not require membership in a squad graph. Concealment reduces
            // it to an immediately local quiet exchange instead of making two pawns
            // standing together inexplicably unable to communicate.
            if (HasVoiceLine(teller, listener, voiceRange))
            {
                channel = CommunicationChannel.Voice;
                return true;
            }

            // Remote force-structure channels still require their native
            // same-faction graph. Co-residence only opens direct voice.
            if (!sameFaction || settings == null || !settings.commsSystem
                || !HasHeadset(teller) || !HasHeadset(listener)
                || !ReportDirectionAllowed(teller, listener, strategicEscalation)
                || !HasRadioLine(teller, listener)) return false;

            channel = CommunicationChannel.Radio;
            return true;
        }

        private static bool SameFactionMap(Pawn first, Pawn second)
        {
            return first != null && second != null && first != second
                && first.Map != null && first.Map == second.Map
                && first.Faction != null && first.Faction == second.Faction;
        }

        private static bool SameRepresentedIndependentSite(Pawn first,
            Pawn second)
        {
            if (first == null || second == null || first == second
                || first.Map == null || first.Map != second.Map)
                return false;
            CARegionalWorldComponent regional =
                CARegionalWorldComponent.Current;
            if (regional == null) return false;
            foreach (CARegionalSettlementRecord site in regional
                .ForMap(first.Map).Where(value => value != null
                    && !CASiteState.HasOwner(value.factionLinks)))
            {
                List<Pawn> residents = CAPopulationProjection.Residents(
                    site, first.Map);
                if (residents.Contains(first) && residents.Contains(second))
                    return true;
            }
            CARegionalPlan plan = regional.FindRegionForMap(first.Map);
            IEnumerable<CAFrontierHoldingPlan> holdings =
                (plan?.frontierHoldings
                    ?? new List<CAFrontierHoldingPlan>())
                .Concat(CAOrganizationWorldComponent.Current
                    ?.FrontierHoldings
                    ?? Enumerable.Empty<CAFrontierHoldingPlan>());
            foreach (CAFrontierHoldingPlan holding in holdings)
                if (holding?.materialized == true
                    && holding.materializedMapId == first.Map.uniqueID
                    && !CASiteState.HasOwner(holding.factionLinks)
                    && holding.residentPawnIds?.Contains(
                        first.thingIDNumber) == true
                    && holding.residentPawnIds.Contains(
                        second.thingIDNumber))
                    return true;
            return false;
        }

        private static bool IsFireteamLead(Pawn pawn, int squad, Map map)
        {
            if (pawn == null) return false;
            int team = SquadComponent.FireteamOf(pawn);
            return team > 0 && SquadComponent.FireteamLeadPawn(squad, team, map) == pawn;
        }

        private static bool IsDirectReport(Pawn pawn, int squad, Map map)
        {
            if (pawn == null) return false;
            int team = SquadComponent.FireteamOf(pawn);
            return team == 0 || SquadComponent.FireteamLeadPawn(squad, team, map) == pawn;
        }

        // Player headset-radio reporting follows the explicit role graph: squad leader <->
        // member for deliberate escalation, fire-team lead <-> own members, and
        // fire-team lead <-> fire-team lead for routine traffic. Nonplayer Lords are
        // only incident groups: membership bounds potential remote peers but appoints
        // no leader and grants no shared knowledge. Nearby voice is independent.
        private static bool GraphRelationship(Pawn first, Pawn second)
        {
            if (!SameFactionMap(first, second)) return false;
            if (first.Faction != Faction.OfPlayer)
            {
                Lord lord = first.GetLord();
                return lord != null && second.GetLord() == lord;
            }

            int squad = SquadComponent.SquadOf(first);
            if (squad == 0 || SquadComponent.SquadOf(second) != squad) return false;
            Map map = first.Map;
            Pawn squadLead = SquadComponent.LeaderPawn(squad, map);
            if (first == squadLead || second == squadLead) return true;

            bool firstLead = IsFireteamLead(first, squad, map);
            bool secondLead = IsFireteamLead(second, squad, map);
            if (firstLead && secondLead) return true;

            int firstTeam = SquadComponent.FireteamOf(first);
            int secondTeam = SquadComponent.FireteamOf(second);
            return firstTeam > 0 && firstTeam == secondTeam
                && (firstLead || secondLead);
        }

        private static bool ReportDirectionAllowed(Pawn teller, Pawn listener,
            bool strategicEscalation)
        {
            if (!GraphRelationship(teller, listener)) return false;
            if (teller.Faction != Faction.OfPlayer) return true;

            int squad = SquadComponent.SquadOf(teller);
            Map map = teller.Map;
            Pawn squadLead = SquadComponent.LeaderPawn(squad, map);
            if (strategicEscalation && listener == squadLead) return true;

            bool tellerLead = IsFireteamLead(teller, squad, map);
            bool listenerLead = IsFireteamLead(listener, squad, map);
            int tellerTeam = SquadComponent.FireteamOf(teller);
            int listenerTeam = SquadComponent.FireteamOf(listener);
            if (tellerTeam > 0 && tellerTeam == listenerTeam
                && (tellerLead || listenerLead)) return true;
            if (teller == squadLead) return IsDirectReport(listener, squad, map);
            if (listener == squadLead) return IsDirectReport(teller, squad, map);
            if (tellerLead && listenerLead) return true;
            return false;
        }

        private static int PeerPriority(Pawn owner, Pawn peer)
        {
            if (owner.Faction != Faction.OfPlayer) return 0;
            int squad = SquadComponent.SquadOf(owner);
            Map map = owner.Map;
            Pawn squadLead = SquadComponent.LeaderPawn(squad, map);
            bool ownerLead = IsFireteamLead(owner, squad, map);
            bool peerLead = IsFireteamLead(peer, squad, map);

            if (owner != squadLead && !ownerLead)
            {
                int team = SquadComponent.FireteamOf(owner);
                Pawn teamLead = team > 0
                    ? SquadComponent.FireteamLeadPawn(squad, team, map) : null;
                if (peer == teamLead) return 0;
                if (peer == squadLead) return 1;
                return 4;
            }
            if (peer == squadLead) return 0;
            if (owner == squadLead && peerLead) return 0;
            if (ownerLead && peerLead) return 1;
            if (ownerLead && SquadComponent.FireteamOf(owner) == SquadComponent.FireteamOf(peer))
                return 2;
            if (owner == squadLead && SquadComponent.FireteamOf(peer) == 0) return 1;
            if (owner == squadLead) return 3;
            return 4;
        }

        private sealed class PeerEdge
        {
            public Pawn first;
            public Pawn second;
            public bool allocated;

            public PeerEdge(Pawn first, Pawn second)
            {
                this.first = first;
                this.second = second;
            }
        }

        private static float Utilization(Pawn pawn, Dictionary<Pawn, int> degree,
            Dictionary<Pawn, int> capacity)
        {
            return (float)degree[pawn] / capacity[pawn];
        }

        // Prefer the edge whose more-loaded endpoint is least utilized, then the
        // lower combined utilization. This prevents a low-ID clique from consuming
        // every line before later peers receive any connection. Role priority and
        // stable IDs break ties without making allocation depend on map movement.
        private static bool BetterAllocationEdge(PeerEdge candidate, PeerEdge best,
            Dictionary<Pawn, int> degree, Dictionary<Pawn, int> capacity)
        {
            if (best == null) return true;
            float candidateFirst = Utilization(candidate.first, degree, capacity);
            float candidateSecond = Utilization(candidate.second, degree, capacity);
            float bestFirst = Utilization(best.first, degree, capacity);
            float bestSecond = Utilization(best.second, degree, capacity);
            float candidateMax = Math.Max(candidateFirst, candidateSecond);
            float bestMax = Math.Max(bestFirst, bestSecond);
            int comparison = candidateMax.CompareTo(bestMax);
            if (comparison != 0) return comparison < 0;
            comparison = (candidateFirst + candidateSecond).CompareTo(
                bestFirst + bestSecond);
            if (comparison != 0) return comparison < 0;

            int candidateRole = PeerPriority(candidate.first, candidate.second)
                + PeerPriority(candidate.second, candidate.first);
            int bestRole = PeerPriority(best.first, best.second)
                + PeerPriority(best.second, best.first);
            if (candidateRole != bestRole) return candidateRole < bestRole;

            int candidateLow = Math.Min(candidate.first.thingIDNumber,
                candidate.second.thingIDNumber);
            int bestLow = Math.Min(best.first.thingIDNumber,
                best.second.thingIDNumber);
            if (candidateLow != bestLow) return candidateLow < bestLow;
            int candidateHigh = Math.Max(candidate.first.thingIDNumber,
                candidate.second.thingIDNumber);
            int bestHigh = Math.Max(best.first.thingIDNumber,
                best.second.thingIDNumber);
            return candidateHigh < bestHigh;
        }

        private static void AllocateRadioPeerNetwork(Map map,
            Dictionary<Pawn, List<Pawn>> cache)
        {
            var nodes = new List<Pawn>();
            var capacity = new Dictionary<Pawn, int>();
            var degree = new Dictionary<Pawn, int>();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead || pawn.Downed
                    || !HasHeadset(pawn)) continue;
                int lines = RadioLineCapacity(pawn);
                if (lines <= 0) continue;
                nodes.Add(pawn);
                capacity[pawn] = lines;
                degree[pawn] = 0;
                cache[pawn] = new List<Pawn>();
            }

            nodes.Sort(delegate(Pawn a, Pawn b)
            {
                return a.thingIDNumber.CompareTo(b.thingIDNumber);
            });
            var edges = new List<PeerEdge>();
            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    if (GraphRelationship(nodes[i], nodes[j]))
                        edges.Add(new PeerEdge(nodes[i], nodes[j]));
                }
            }

            while (true)
            {
                PeerEdge best = null;
                for (int i = 0; i < edges.Count; i++)
                {
                    PeerEdge candidate = edges[i];
                    if (candidate.allocated
                        || degree[candidate.first] >= capacity[candidate.first]
                        || degree[candidate.second] >= capacity[candidate.second])
                        continue;
                    if (BetterAllocationEdge(candidate, best, degree, capacity))
                        best = candidate;
                }
                if (best == null) break;
                best.allocated = true;
                cache[best.first].Add(best.second);
                cache[best.second].Add(best.first);
                degree[best.first]++;
                degree[best.second]++;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                Pawn owner = nodes[i];
                cache[owner].Sort(delegate(Pawn a, Pawn b)
                {
                    int priority = PeerPriority(owner, a).CompareTo(
                        PeerPriority(owner, b));
                    return priority != 0 ? priority
                        : a.thingIDNumber.CompareTo(b.thingIDNumber);
                });
            }
        }

        private static List<Pawn> CandidateRadioPeers(Pawn owner)
        {
            if (owner == null || owner.Map == null) return new List<Pawn>();
            EnsureTopologyCache();
            List<Pawn> cached;
            if (RadioPeerCache.TryGetValue(owner, out cached)) return cached;
            AllocateRadioPeerNetwork(owner.Map, RadioPeerCache);
            return RadioPeerCache.TryGetValue(owner, out cached)
                ? cached : new List<Pawn>();
        }

        private static bool WithinAllocatedRadioLines(Pawn owner, Pawn peer,
            int capacity)
        {
            if (capacity <= 0 || !HasHeadset(owner) || !HasHeadset(peer))
                return false;
            List<Pawn> candidates = CandidateRadioPeers(owner);
            return candidates.Contains(peer);
        }

        public static bool HasRadioLine(Pawn first, Pawn second)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || !settings.commsSystem
                || !GraphRelationship(first, second)
                || NetworkOutage(first.Map)) return false;
            return WithinAllocatedRadioLines(first, second, RadioLineCapacity(first))
                && WithinAllocatedRadioLines(second, first, RadioLineCapacity(second));
        }

        public static bool HasMentalLine(Pawn first, Pawn second)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            return settings != null && settings.commsSystem
                && SameFactionMap(first, second)
                && HasActiveMechlink(first) && HasActiveMechlink(second)
                && !NetworkOutage(first.Map);
        }

        // Current provisional voice/radio command-span model retained from the
        // pre-git prototype. Its precise formula remains unratified. It must not gate
        // raw hearing, reporting, gesture, or synchronized mental delivery.
        public static int PsychCapacityOf(Pawn leader)
        {
            int cap = 2;
            if (leader != null && leader.skills != null)
            {
                cap += (leader.skills.GetSkill(SkillDefOf.Intellectual).Level
                      + leader.skills.GetSkill(SkillDefOf.Social).Level) / 8;
            }
            TraitSet traits = leader != null && leader.story != null
                ? leader.story.traits : null;
            if (traits != null)
            {
                TraitDef nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");
                if (nerves != null)
                {
                    int degree = traits.DegreeOfTrait(nerves);
                    if (degree > 0) cap += 1;
                    else if (degree < 0) cap -= 1;
                }
                TraitDef wimp = DefDatabase<TraitDef>.GetNamedSilentFail("Wimp");
                if (wimp != null && traits.HasTrait(wimp)) cap -= 1;
                TraitDef psychopath = DefDatabase<TraitDef>.GetNamedSilentFail("Psychopath");
                if (psychopath != null && traits.HasTrait(psychopath)) cap -= 1;
            }
            return cap < 1 ? 1 : (cap > 8 ? 8 : cap);
        }

        private static bool ConnectedWithin(Pawn leader, Pawn member, float voiceRange)
        {
            if (!SameFactionMap(leader, member)) return false;
            if (leader.Dead || leader.Downed || member.Dead || member.Downed)
                return false;
            if (!leader.Awake() || !member.Awake()
                || leader.InMentalState || member.InMentalState) return false;
            if (HasMentalLine(leader, member) || HasRadioLine(leader, member)) return true;
            return HasVoiceLine(leader, member, voiceRange);
        }

        public static bool HasVoiceLine(Pawn speaker, Pawn listener,
            float requestedRange)
        {
            if (!SameFactionMap(speaker, listener) || requestedRange < 0f)
                return false;
            float effectiveRange = HiddenRegistry.IsHiddenOrOrdered(speaker)
                ? Math.Min(requestedRange, ConcealedVoiceRange)
                : requestedRange;
            return listener.Position.InHorDistOf(speaker.Position,
                effectiveRange);
        }

        // A deliberate combat order can be passed by an immediately adjacent visual
        // gesture even when shouting would break concealment and neither pawn has a
        // radio. This is an explicit command route, not a general knowledge broadcast:
        // it does not turn gesture into hearing or give the pair shared contacts.
        public static bool HasLocalCommandGesture(Pawn commander, Pawn member)
        {
            return SameFactionMap(commander, member)
                && !commander.Dead && !commander.Downed
                && !member.Dead && !member.Downed
                && commander.Awake() && member.Awake()
                && !commander.InMentalState && !member.InMentalState
                && commander.Position.InHorDistOf(member.Position, 2.9f)
                && GenSight.LineOfSight(commander.Position, member.Position,
                    commander.Map, true);
        }

        public static bool Connected(Pawn leader, Pawn member)
        {
            return ConnectedWithin(leader, member, VoiceRange);
        }

        private static string MissingMechlinkDetail(Pawn first, Pawn second,
            bool firstHas, bool secondHas)
        {
            if (!firstHas && !secondHas)
                return "neither endpoint has an active mechlink";
            return (!firstHas ? first : second).LabelShort
                + " has no active mechlink";
        }

        private static string LocalRouteAbsence(Pawn commander, Pawn member,
            float voiceRange)
        {
            string voice;
            if (voiceRange < 0f)
                voice = "voice is excluded from this route check";
            else if (HiddenRegistry.IsHiddenOrOrdered(commander))
                voice = "quiet voice is out of "
                    + Math.Min(voiceRange, ConcealedVoiceRange).ToString("F1")
                    + "-cell range (distance "
                    + commander.Position.DistanceTo(member.Position)
                        .ToString("F1") + ")";
            else
                voice = "voice is out of range (distance "
                    + commander.Position.DistanceTo(member.Position)
                        .ToString("F1") + ")";
            return voice + " and adjacent visible gesture is unavailable";
        }

        // Observe one physical command hop before authority is considered. Gesture
        // and the synchronized mental network are delivery-complete and never spend
        // provisional command-span slots. Voice and allocated headset radio retain
        // that separate provisional span for now.
        private static CommandRouteReceipt ObserveCommandHop(Pawn commander,
            Pawn member, float voiceRange)
        {
            var receipt = new CommandRouteReceipt
            {
                From = commander,
                To = member,
                Failure = CommandRouteFailure.None,
                Channel = CommunicationChannel.None
            };
            if (!SameFactionMap(commander, member))
            {
                receipt.Failure = CommandRouteFailure.DifferentMapOrFaction;
                receipt.Detail = "endpoints are not distinct same-faction spawned pawns on one map";
                return receipt;
            }

            receipt.FromHasActiveMechlink = HasActiveMechlink(commander);
            receipt.ToHasActiveMechlink = HasActiveMechlink(member);
            receipt.NetworkOutage = NetworkOutage(commander.Map);
            receipt.GestureAvailable = HasLocalCommandGesture(commander, member);
            receipt.VoiceAvailable = HasVoiceLine(commander, member,
                voiceRange);
            receipt.MentalAvailable = HasMentalLine(commander, member);
            receipt.RadioAvailable = HasRadioLine(commander, member);

            if (commander.Dead || commander.Downed || member.Dead || member.Downed
                || !commander.Awake() || !member.Awake()
                || commander.InMentalState || member.InMentalState)
            {
                receipt.Failure = CommandRouteFailure.EndpointUnavailable;
                if (commander.Dead) receipt.Detail = commander.LabelShort + " is dead";
                else if (commander.Downed) receipt.Detail = commander.LabelShort + " is downed";
                else if (!commander.Awake()) receipt.Detail = commander.LabelShort + " is not awake";
                else if (commander.InMentalState)
                    receipt.Detail = commander.LabelShort + " is in a mental state";
                else if (member.Dead) receipt.Detail = member.LabelShort + " is dead";
                else if (member.Downed)
                    receipt.Detail = member.LabelShort + " is downed";
                else if (!member.Awake())
                    receipt.Detail = member.LabelShort + " is not awake";
                else receipt.Detail = member.LabelShort + " is in a mental state";
                if (receipt.MentalAvailable)
                    receipt.Detail += "; the synchronized mental route exists, but "
                        + "this endpoint cannot issue or act on the command";
                return receipt;
            }

            if (receipt.GestureAvailable)
            {
                receipt.Delivered = true;
                receipt.Channel = CommunicationChannel.Gesture;
                receipt.Detail = "delivered by adjacent visible gesture";
                return receipt;
            }
            if (receipt.MentalAvailable)
            {
                receipt.Delivered = true;
                receipt.Channel = CommunicationChannel.Mental;
                receipt.Detail = "delivered directly on the synchronized mechlink mental network";
                return receipt;
            }
            if (receipt.RadioAvailable)
            {
                receipt.Delivered = true;
                receipt.Channel = CommunicationChannel.Radio;
                receipt.Detail = "delivered on an allocated headset-radio line";
                return receipt;
            }
            if (receipt.VoiceAvailable)
            {
                receipt.Delivered = true;
                receipt.Channel = CommunicationChannel.Voice;
                receipt.Detail = HiddenRegistry.IsHiddenOrOrdered(commander)
                    ? "delivered by quiet local voice within concealed posture"
                    : "delivered by local voice";
                return receipt;
            }

            string localAbsence = LocalRouteAbsence(commander, member,
                voiceRange);
            bool bothMental = receipt.FromHasActiveMechlink
                && receipt.ToHasActiveMechlink;
            bool bothRadio = HasHeadset(commander) && HasHeadset(member);
            if (bothMental && receipt.NetworkOutage)
            {
                receipt.Failure = CommandRouteFailure.MentalNetworkOutage;
                receipt.Detail = "the mechlink mental network is offline because map electricity "
                    + "is disabled (for example, Solar Flare); " + localAbsence;
            }
            else if (!bothMental && !bothRadio)
            {
                receipt.Failure = CommandRouteFailure.NoPhysicalRoute;
                receipt.Detail = MissingMechlinkDetail(commander, member,
                    receipt.FromHasActiveMechlink, receipt.ToHasActiveMechlink)
                    + "; headset radio requires a supported headset at both endpoints; "
                    + localAbsence;
            }
            else
            {
                receipt.Failure = CommandRouteFailure.NoPhysicalRoute;
                AwarenessSettings settings = AwarenessMod.Settings;
                string remote = bothMental
                        && (settings == null || !settings.commsSystem)
                    ? "CA communications are disabled, so the synchronized mechlink route is unavailable"
                    : receipt.NetworkOutage
                        ? "the remote network is offline because map electricity is disabled"
                        : "no headset-radio peer line is allocated in the current force graph";
                if (!bothMental)
                    remote += "; " + MissingMechlinkDetail(commander, member,
                        receipt.FromHasActiveMechlink, receipt.ToHasActiveMechlink);
                receipt.Detail = remote + "; " + localAbsence;
            }
            return receipt;
        }

        private static CommandRouteReceipt CommandWithinReceipt(Pawn commander,
            Pawn member, List<Pawn> peers, float voiceRange)
        {
            CommandRouteReceipt receipt = ObserveCommandHop(commander, member, voiceRange);
            if (!receipt.Delivered
                || receipt.Channel == CommunicationChannel.Gesture
                || receipt.Channel == CommunicationChannel.Mental)
                return receipt;

            var eligible = new List<Pawn>();
            if (peers != null)
            {
                for (int i = 0; i < peers.Count; i++)
                {
                    Pawn pawn = peers[i];
                    CommandRouteReceipt peerReceipt = ObserveCommandHop(
                        commander, pawn, voiceRange);
                    // Mental and gesture delivery neither consume nor displace a
                    // provisional voice/radio command-span slot.
                    if (peerReceipt.Delivered
                        && peerReceipt.Channel != CommunicationChannel.Gesture
                        && peerReceipt.Channel != CommunicationChannel.Mental)
                        eligible.Add(pawn);
                }
            }
            eligible.Sort(delegate(Pawn a, Pawn b)
            {
                return CompareCommandPeers(commander, a, b);
            });
            int index = eligible.IndexOf(member);
            if (index < 0)
            {
                receipt.Delivered = false;
                receipt.Failure = CommandRouteFailure.NoPhysicalRoute;
                receipt.Detail = "the observed physical route was not present in this order's route plan";
                return receipt;
            }
            if (index >= PsychCapacityOf(commander))
            {
                receipt.Delivered = false;
                receipt.Failure = CommandRouteFailure.CapacityExceeded;
                receipt.Detail = receipt.Channel.ToString().ToLowerInvariant()
                    + " delivery exists, but the provisional voice/radio command span is exhausted";
            }
            return receipt;
        }

        private static int CompareCommandPeers(Pawn commander, Pawn a, Pawn b)
        {
            int priority = PeerPriority(commander, a).CompareTo(
                PeerPriority(commander, b));
            if (priority != 0) return priority;
            int distance = commander.Position.DistanceToSquared(a.Position)
                .CompareTo(commander.Position.DistanceToSquared(b.Position));
            if (distance != 0) return distance;
            return a.thingIDNumber.CompareTo(b.thingIDNumber);
        }

        private static bool CanCommandWithin(Pawn commander, Pawn member,
            List<Pawn> peers, float voiceRange)
        {
            return CommandWithinReceipt(commander, member, peers, voiceRange).Delivered;
        }

        private static List<Pawn> SquadDirectReports(Pawn leader, int squad, Map map,
            List<Pawn> colonists)
        {
            var directs = new List<Pawn>();
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (pawn == leader || SquadComponent.SquadOf(pawn) != squad) continue;
                if (IsDirectReport(pawn, squad, map)) directs.Add(pawn);
            }
            return directs;
        }

        private static List<Pawn> FireteamMembers(Pawn leader, int squad, int team,
            List<Pawn> colonists)
        {
            var members = new List<Pawn>();
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (pawn == leader || SquadComponent.SquadOf(pawn) != squad) continue;
                if (SquadComponent.FireteamOf(pawn) == team) members.Add(pawn);
            }
            return members;
        }

        private static Dictionary<Pawn, Pawn> BuildOrderFirstHops(Pawn relayer,
            List<Pawn> recipients, bool relayerIsLeader, int squad, Map map)
        {
            var result = new Dictionary<Pawn, Pawn>();
            for (int i = 0; i < recipients.Count; i++)
                result[recipients[i]] = recipients[i];
            if (!relayerIsLeader) return result;

            // A group order normally compresses each fire team behind its lead. A
            // relay is considered only when both physical legs exist. If the team
            // lead's own reliable span is scarce, members without a direct line get
            // its slots first; direct-capable members then fall back to the squad
            // leader. A member with neither available route receives the truthful
            // physical/capacity failure.
            var byTeamLead = new Dictionary<Pawn, List<Pawn>>();
            for (int i = 0; i < recipients.Count; i++)
            {
                Pawn recipient = recipients[i];
                if (SquadComponent.SquadOf(recipient) != squad) continue;
                // The map-wide mental network is already a direct hop. Routing it
                // through a role intermediary would falsely reintroduce command
                // capacity into delivery.
                if (HasMentalLine(relayer, recipient)) continue;
                int team = SquadComponent.FireteamOf(recipient);
                if (team <= 0 || IsFireteamLead(recipient, squad, map)) continue;
                Pawn teamLead = SquadComponent.FireteamLeadPawn(squad, team, map);
                if (teamLead == null || teamLead == relayer
                    || !ConnectedWithin(relayer, teamLead, VoiceRange)
                    || !ConnectedWithin(teamLead, recipient, VoiceRange)) continue;
                List<Pawn> candidates;
                if (!byTeamLead.TryGetValue(teamLead, out candidates))
                {
                    candidates = new List<Pawn>();
                    byTeamLead[teamLead] = candidates;
                }
                candidates.Add(recipient);
            }

            foreach (KeyValuePair<Pawn, List<Pawn>> pair in byTeamLead)
            {
                Pawn teamLead = pair.Key;
                List<Pawn> candidates = pair.Value;
                candidates.Sort(delegate(Pawn a, Pawn b)
                {
                    bool aHasDirect = ConnectedWithin(relayer, a, VoiceRange);
                    bool bHasDirect = ConnectedWithin(relayer, b, VoiceRange);
                    if (aHasDirect != bHasDirect) return aHasDirect ? 1 : -1;
                    return CompareCommandPeers(teamLead, a, b);
                });
                int capacity = PsychCapacityOf(teamLead);
                int capacityUsed = 0;
                for (int i = 0; i < candidates.Count; i++)
                {
                    Pawn recipient = candidates[i];
                    bool mentalHop = HasMentalLine(teamLead, recipient);
                    bool directAvailable = ConnectedWithin(relayer, recipient,
                        VoiceRange);
                    if (mentalHop || capacityUsed < capacity || !directAvailable)
                    {
                        result[recipient] = teamLead;
                        if (!mentalHop) capacityUsed++;
                    }
                }
            }
            return result;
        }

        // The with-who order surface can originate from any selected pawn. The
        // actual squad leader may relay down the hierarchy; everyone else can issue
        // only across a direct voice/radio/mental relationship, with authority still
        // adjudicated separately by Authority.FilterObedient.
        public static bool CanRelayOrder(Pawn relayer, Pawn member)
        {
            return CanRelayOrder(relayer, member, null);
        }

        public static bool CanRelayOrder(Pawn relayer, Pawn member,
            List<Pawn> recipients)
        {
            CommandRouteFailure ignored;
            return CanRelayOrder(relayer, member, recipients, out ignored);
        }

        // One order gets one route plan. Voice and headset-radio first hops share
        // the current provisional span. Gesture and synchronized mental delivery
        // bypass that span without consuming slots that would displace other media.
        public static bool CanRelayOrder(Pawn relayer, Pawn member,
            List<Pawn> recipients, out CommandRouteFailure failure)
        {
            CommandRouteReceipt receipt;
            bool delivered = CanRelayOrder(relayer, member, recipients, out receipt);
            failure = receipt != null
                ? receipt.Failure : CommandRouteFailure.NoPhysicalRoute;
            return delivered;
        }

        public static bool CanRelayOrder(Pawn relayer, Pawn member,
            List<Pawn> recipients, out CommandRouteReceipt receipt)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || !settings.commsSystem)
            {
                receipt = new CommandRouteReceipt
                {
                    Delivered = true,
                    Failure = CommandRouteFailure.None,
                    Channel = CommunicationChannel.None,
                    From = relayer,
                    To = member,
                    Detail = "delivery gate bypassed because CA comms is disabled"
                };
                return true;
            }
            if (relayer == member)
            {
                receipt = new CommandRouteReceipt
                {
                    Delivered = true,
                    Failure = CommandRouteFailure.None,
                    Channel = CommunicationChannel.None,
                    From = relayer,
                    To = member,
                    Detail = "operator-direct selected actor; no relay is required"
                };
                return true;
            }
            if (!SameFactionMap(relayer, member))
            {
                receipt = ObserveCommandHop(relayer, member, VoiceRange);
                return false;
            }

            int squad = SquadComponent.SquadOf(relayer);
            Map map = relayer.Map;
            Pawn squadLeader = squad > 0
                ? SquadComponent.LeaderPawn(squad, map) : null;
            bool relayerIsLeader = squadLeader == relayer;
            var orderRecipients = new List<Pawn>();
            if (recipients == null)
                orderRecipients.Add(member);
            else
            {
                for (int i = 0; i < recipients.Count; i++)
                {
                    Pawn pawn = recipients[i];
                    if (pawn != null && pawn != relayer
                        && SameFactionMap(relayer, pawn)
                        && !orderRecipients.Contains(pawn))
                        orderRecipients.Add(pawn);
                }
            }
            if (!orderRecipients.Contains(member)) orderRecipients.Add(member);

            Dictionary<Pawn, Pawn> firstHopByRecipient = BuildOrderFirstHops(
                relayer, orderRecipients, relayerIsLeader, squad, map);

            var firstHops = new List<Pawn>();
            for (int i = 0; i < orderRecipients.Count; i++)
            {
                Pawn recipient = orderRecipients[i];
                Pawn firstHop = firstHopByRecipient[recipient];
                if (firstHop != relayer && !firstHops.Contains(firstHop))
                    firstHops.Add(firstHop);
            }

            Pawn memberFirstHop = firstHopByRecipient[member];

            CommandRouteReceipt firstReceipt = CommandWithinReceipt(relayer,
                memberFirstHop, firstHops, VoiceRange);
            if (!firstReceipt.Delivered)
            {
                if (memberFirstHop != member)
                {
                    firstReceipt.Relay = memberFirstHop;
                    firstReceipt.Detail = "first hop to relay "
                        + memberFirstHop.LabelShort + " failed: " + firstReceipt.Detail;
                }
                receipt = firstReceipt;
                return false;
            }
            if (memberFirstHop == member)
            {
                receipt = firstReceipt;
                return true;
            }

            var teamRecipients = new List<Pawn>();
            for (int i = 0; i < orderRecipients.Count; i++)
            {
                Pawn recipient = orderRecipients[i];
                if (recipient != memberFirstHop
                    && firstHopByRecipient[recipient] == memberFirstHop)
                    teamRecipients.Add(recipient);
            }
            CommandRouteReceipt finalReceipt = CommandWithinReceipt(memberFirstHop,
                member, teamRecipients, VoiceRange);
            finalReceipt.Relay = memberFirstHop;
            if (!finalReceipt.Delivered)
            {
                finalReceipt.Detail = "relay " + memberFirstHop.LabelShort
                    + " cannot complete the final hop: " + finalReceipt.Detail;
                receipt = finalReceipt;
                return false;
            }
            finalReceipt.Detail = "delivered via " + memberFirstHop.LabelShort
                + " (first hop: " + firstReceipt.Detail + "; final hop: "
                + finalReceipt.Detail + ")";
            receipt = finalReceipt;
            return true;
        }

        public static bool CanCommand(Pawn leader, Pawn member)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || !settings.commsSystem) return true;
            if (leader == null || member == null || leader.Map == null) return false;

            int squad = SquadComponent.SquadOf(leader);
            if (squad == 0 || SquadComponent.SquadOf(member) != squad) return false;

            Map map = leader.Map;
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            List<Pawn> directs = SquadDirectReports(leader, squad, map, colonists);
            int memberTeam = SquadComponent.FireteamOf(member);
            if (memberTeam == 0 || IsFireteamLead(member, squad, map))
                return CanCommandWithin(leader, member, directs, VoiceRange);

            // Fire-team structure is the normal headset/voice route, not a wall. A
            // leader who is physically near the member, has an allocated direct
            // headset line, or shares the synchronized mental network may still
            // address them personally. Only voice/headset delivery consumes the
            // leader's provisional span across the reachable squad.
            var squadMembers = new List<Pawn>();
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (pawn != leader && SquadComponent.SquadOf(pawn) == squad)
                    squadMembers.Add(pawn);
            }
            if (CanCommandWithin(leader, member, squadMembers, VoiceRange)) return true;

            // Fire-team members are directed through their lead. This is why a
            // capable fire-team lead lets a squad leader manage the higher-level
            // graph instead of personally carrying every subordinate.
            Pawn teamLead = SquadComponent.FireteamLeadPawn(squad, memberTeam, map);
            if (teamLead == null || teamLead == member)
                return CanCommandWithin(leader, member, directs, VoiceRange);
            if (!CanCommandWithin(leader, teamLead, directs, VoiceRange)) return false;
            return CanCommandWithin(teamLead, member,
                FireteamMembers(teamLead, squad, memberTeam, colonists), VoiceRange);
        }
    }
}
