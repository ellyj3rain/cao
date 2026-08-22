using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Immutable-at-capture evidence for one concern returned by LogEntry.GetConcerns.
    // The ordinal in the containing list is the only meaning assigned to its order.
    internal sealed class CACombatSpatialConcernSnapshot : IExposable
    {
        internal string thingId;
        internal string label;
        internal int ordinal = -1;
        internal int mapId = -1;
        internal IntVec3 cell = IntVec3.Invalid;

        public void ExposeData()
        {
            Scribe_Values.Look(ref thingId, "thingId");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref ordinal, "ordinal", -1);
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
        }
    }

    internal sealed class CACombatSpatialLogRecord : IExposable
    {
        internal int logId;
        internal int eventTick = -1;
        internal int mapId = -1;
        internal List<CACombatSpatialConcernSnapshot> concerns =
            new List<CACombatSpatialConcernSnapshot>();
        internal bool hasBulletImpact;
        internal int impactMapId = -1;
        internal IntVec3 impactCell = IntVec3.Invalid;
        internal string topologyIncidentId;

        public void ExposeData()
        {
            Scribe_Values.Look(ref logId, "logId", 0);
            Scribe_Values.Look(ref eventTick, "eventTick", -1);
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Collections.Look(ref concerns, "concerns", LookMode.Deep);
            Scribe_Values.Look(ref hasBulletImpact, "hasBulletImpact", false);
            Scribe_Values.Look(ref impactMapId, "impactMapId", -1);
            Scribe_Values.Look(ref impactCell, "impactCell", IntVec3.Invalid);
            Scribe_Values.Look(ref topologyIncidentId,
                "topologyIncidentId");
            if (concerns == null)
                concerns = new List<CACombatSpatialConcernSnapshot>();
        }

        internal static CACombatSpatialLogRecord Capture(LogEntry entry)
        {
            if (entry == null) return null;

            var record = new CACombatSpatialLogRecord
            {
                logId = entry.LogID,
                eventTick = entry.Tick
            };
            var concernSequence = new List<Thing>();
            int ordinal = 0;

            foreach (Thing thing in entry.GetConcerns())
            {
                concernSequence.Add(thing);
                int currentOrdinal = ordinal++;
                if (thing == null || !thing.Spawned || thing.Map == null)
                    continue;

                Map map = thing.Map;
                record.concerns.Add(new CACombatSpatialConcernSnapshot
                {
                    thingId = thing.ThingID,
                    label = thing.Label,
                    ordinal = currentOrdinal,
                    mapId = map.uniqueID,
                    cell = thing.Position
                });
                if (record.mapId < 0)
                    record.mapId = map.uniqueID;
            }

            int impactMap;
            IntVec3 impact;
            if (CABulletImpactSpatialContext.TryClaim(entry, concernSequence,
                out impactMap, out impact))
            {
                record.hasBulletImpact = true;
                record.impactMapId = impactMap;
                record.impactCell = impact;
                record.mapId = impactMap;
            }

            // With no live map there is no coordinate snapshot to expose. Leaving the
            // entry untouched is more truthful than manufacturing a map sentinel.
            return record.mapId >= 0 ? record : null;
        }

        internal string StableSuffix()
        {
            var sb = new StringBuilder(96 + concerns.Count * 64);
            sb.Append(" [CA-space:v2 log=");
            AppendInt(sb, logId);
            sb.Append(" tickAbs=");
            AppendInt(sb, eventTick);
            sb.Append(" map=");
            AppendInt(sb, mapId);

            for (int i = 0; i < concerns.Count; i++)
            {
                CACombatSpatialConcernSnapshot concern = concerns[i];
                if (concern == null) continue;
                sb.Append(" c");
                AppendInt(sb, concern.ordinal >= 0 ? concern.ordinal : i);
                sb.Append("{id=");
                AppendQuoted(sb, concern.thingId);
                sb.Append(",label=");
                AppendQuoted(sb, concern.label);
                sb.Append(",map=");
                AppendInt(sb, concern.mapId);
                sb.Append(",x=");
                AppendInt(sb, concern.cell.x);
                sb.Append(",z=");
                AppendInt(sb, concern.cell.z);
                sb.Append('}');
            }

            if (hasBulletImpact && impactMapId >= 0 && impactCell.IsValid)
            {
                sb.Append(" impact{map=");
                AppendInt(sb, impactMapId);
                sb.Append(",x=");
                AppendInt(sb, impactCell.x);
                sb.Append(",z=");
                AppendInt(sb, impactCell.z);
                sb.Append('}');
            }

            if (!topologyIncidentId.NullOrEmpty())
            {
                sb.Append(" topology=");
                AppendQuoted(sb, topologyIncidentId);
            }

            sb.Append(']');
            return sb.ToString();
        }

        private static void AppendInt(StringBuilder sb, int value)
        {
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendQuoted(StringBuilder sb, string value)
        {
            sb.Append('"');
            if (value != null)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    switch (c)
                    {
                        case '\\': sb.Append("\\\\"); break;
                        case '"': sb.Append("\\\""); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < ' ')
                            {
                                sb.Append("\\u");
                                sb.Append(((int)c).ToString("X4",
                                    CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                sb.Append(c);
                            }
                            break;
                    }
                }
            }
            sb.Append('"');
        }
    }

    // Save-owned evidence index for combat coordinates and map topology. It has no
    // tick polling; native map events feed bounded deltas, while presentation and
    // explicit read-only exports consume the retained snapshots.
    //
    // Save policy: a record lives only while its native battle-log entry
    // lives, and at most the newest RetainedRecordCap records serialize.
    // The native battle log keeps battles for roughly seven in-game days
    // after their last entry, so chronic day-long combat would otherwise
    // accumulate past the campaign preflight's streaming element limit and
    // fail every save; beyond the recency horizon the native battle-log
    // text remains while the CA spatial snapshot expires.
    internal sealed partial class CACombatSpatialLogComponent : GameComponent
    {
        private const int StoresBetweenPrunes = 128;
        private const int RetainedRecordCap = 2000;

        private readonly Game game;
        private Dictionary<int, CACombatSpatialLogRecord> records =
            new Dictionary<int, CACombatSpatialLogRecord>();
        private int storesSincePrune;

        public CACombatSpatialLogComponent(Game game)
        {
            this.game = game;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref records, "CA_combatSpatialLogRecords",
                LookMode.Value, LookMode.Deep);
            ExposeTopologyData();
            if (records == null)
                records = new Dictionary<int, CACombatSpatialLogRecord>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                storesSincePrune = 0;
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            PruneToRetainedBattleLog();
            FinalizeTopologyData();
        }

        internal void Store(LogEntry entry, CACombatSpatialLogRecord record,
            Battle battle)
        {
            if (record == null || records.ContainsKey(record.logId)) return;
            records.Add(record.logId, record);
            try
            {
                AttachTopology(entry, record, battle);
            }
            catch
            {
                RecordTopologyObservationFailure(record.mapId,
                    "attach-topology");
            }
            storesSincePrune++;
            if (storesSincePrune >= StoresBetweenPrunes)
            {
                storesSincePrune = 0;
                PruneToRetainedBattleLog();
            }
        }

        internal void Remove(int logId)
        {
            records.Remove(logId);
            PruneTopologyToRecords();
        }

        internal bool TryGet(int logId, out CACombatSpatialLogRecord record)
        {
            return records.TryGetValue(logId, out record) && record != null;
        }

        internal void PruneToRetainedBattleLog()
        {
            BattleLog battleLog = game != null ? game.battleLog : null;
            if (battleLog == null || battleLog.Battles == null) return;

            var retained = new HashSet<int>();
            List<Battle> battles = battleLog.Battles;
            for (int i = 0; i < battles.Count; i++)
            {
                Battle battle = battles[i];
                if (battle == null || battle.Entries == null) continue;
                List<LogEntry> entries = battle.Entries;
                for (int j = 0; j < entries.Count; j++)
                {
                    LogEntry entry = entries[j];
                    if (entry != null) retained.Add(entry.LogID);
                }
            }

            List<int> stale = null;
            foreach (KeyValuePair<int, CACombatSpatialLogRecord> pair in records)
            {
                if (pair.Value != null && retained.Contains(pair.Key)) continue;
                if (stale == null) stale = new List<int>();
                stale.Add(pair.Key);
            }
            if (stale != null)
                for (int i = 0; i < stale.Count; i++)
                    records.Remove(stale[i]);
            bool pruned = stale != null;
            if (records.Count > RetainedRecordCap)
            {
                // Native log ids increase monotonically, so the smallest
                // ids are the oldest evidence beyond the recency horizon.
                var ordered = new List<int>(records.Keys);
                ordered.Sort();
                int surplus = records.Count - RetainedRecordCap;
                for (int i = 0; i < surplus; i++)
                    records.Remove(ordered[i]);
                pruned = true;
            }
            if (pruned) PruneTopologyToRecords();
        }

        internal static CACombatSpatialLogComponent CurrentComponent()
        {
            Game current = Current.Game;
            return current != null
                ? current.GetComponent<CACombatSpatialLogComponent>()
                : null;
        }
    }

    // This context exactly brackets RimWorld.Bullet.Impact. Only the one ranged-impact
    // entry it creates may consume the cell; other log entries get concern snapshots only.
    internal static class CABulletImpactSpatialContext
    {
        [ThreadStatic] private static bool active;
        [ThreadStatic] private static bool consumed;
        [ThreadStatic] private static int mapId;
        [ThreadStatic] private static IntVec3 cell;
        [ThreadStatic] private static Thing expectedLauncher;
        [ThreadStatic] private static Thing expectedRecipient;
        [ThreadStatic] private static Thing expectedOriginalTarget;

        internal static void Begin(Bullet bullet, Thing hitThing)
        {
            Clear();
            if (bullet == null || bullet.Map == null) return;
            active = true;
            mapId = bullet.Map.uniqueID;
            cell = bullet.Position;
            expectedLauncher = bullet.Launcher;
            expectedRecipient = hitThing;
            expectedOriginalTarget = bullet.intendedTarget.Thing;
        }

        internal static bool TryClaim(LogEntry entry,
            List<Thing> concernSequence, out int impactMapId,
            out IntVec3 impactCell)
        {
            impactMapId = -1;
            impactCell = IntVec3.Invalid;
            if (!active || consumed || !(entry is BattleLogEntry_RangedImpact)
                || mapId < 0 || !cell.IsValid || concernSequence == null
                || !MatchesExpectedConcerns(concernSequence))
                return false;

            consumed = true;
            impactMapId = mapId;
            impactCell = cell;
            return true;
        }

        private static bool MatchesExpectedConcerns(List<Thing> concerns)
        {
            int index = 0;
            if (!MatchesExpectedPawn(expectedLauncher, concerns, ref index)
                || !MatchesExpectedPawn(expectedRecipient, concerns, ref index)
                || !MatchesExpectedPawn(expectedOriginalTarget, concerns,
                    ref index))
                return false;
            return index == concerns.Count;
        }

        private static bool MatchesExpectedPawn(Thing expected,
            List<Thing> concerns, ref int index)
        {
            if (!(expected is Pawn)) return true;
            if (index >= concerns.Count
                || !ReferenceEquals(concerns[index], expected))
                return false;
            index++;
            return true;
        }

        internal static void Clear()
        {
            active = false;
            consumed = false;
            mapId = -1;
            cell = IntVec3.Invalid;
            expectedLauncher = null;
            expectedRecipient = null;
            expectedOriginalTarget = null;
        }
    }

    [HarmonyPatch(typeof(Bullet), "Impact")]
    internal static class Patch_CABulletImpactSpatialContext
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        internal static void Prefix(Bullet __instance, Thing hitThing)
        {
            try
            {
                CABulletImpactSpatialContext.Begin(__instance, hitThing);
            }
            catch
            {
                CABulletImpactSpatialContext.Clear();
            }
        }

        [HarmonyFinalizer]
        internal static Exception Finalizer(Exception __exception)
        {
            CABulletImpactSpatialContext.Clear();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(BattleLog), nameof(BattleLog.Add))]
    internal static class Patch_CABattleLogSpatialCapture
    {
        [ThreadStatic] private static bool capturing;

        // Verse.Battle.Add inserts at index zero. Count - 1 is the oldest entry,
        // so it cannot identify the Battle that just accepted this log entry.
        internal static bool HasNewlyInsertedEntry<T>(IList<T> entries,
            T entry) where T : class
        {
            return entry != null && entries != null && entries.Count > 0
                && ReferenceEquals(entries[0], entry);
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(BattleLog __instance, LogEntry entry,
            out CACombatSpatialLogRecord __state)
        {
            __state = null;
            if (capturing) return;
            CACombatSpatialLogComponent component = null;
            try
            {
                Game game = Current.Game;
                if (game == null || game.battleLog != __instance)
                    return;
                component = game.GetComponent<CACombatSpatialLogComponent>();
                if (component == null) return;
                capturing = true;
                __state = CACombatSpatialLogRecord.Capture(entry);
            }
            catch
            {
                __state = null;
                component?.RecordTopologyObservationFailure(-1,
                    "battle-log-spatial-prefix");
            }
            finally
            {
                capturing = false;
            }
        }

        [HarmonyPostfix]
        internal static void Postfix(BattleLog __instance, LogEntry entry,
            CACombatSpatialLogRecord __state)
        {
            if (__state == null) return;
            CACombatSpatialLogComponent component = null;
            try
            {
                Game game = Current.Game;
                if (game == null || game.battleLog != __instance) return;
                component = game.GetComponent<CACombatSpatialLogComponent>();
                Battle battle = null;
                foreach (Pawn concern in entry.GetConcerns())
                {
                    Battle active = concern?.records?.BattleActive;
                    if (active == null || !HasNewlyInsertedEntry(
                        active.Entries, entry)) continue;
                    battle = active;
                    break;
                }
                if (battle == null)
                {
                    List<Battle> battles = __instance.Battles;
                    for (int i = 0; i < battles.Count; i++)
                    {
                        Battle candidate = battles[i];
                        if (!HasNewlyInsertedEntry(candidate?.Entries, entry))
                            continue;
                        battle = candidate;
                        break;
                    }
                }
                component?.Store(entry, __state, battle);
            }
            catch
            {
                // Observation must never make BattleLog.Add fail.
                component?.RecordTopologyObservationFailure(__state.mapId,
                    "battle-log-topology-postfix");
            }
        }
    }

    [HarmonyPatch(typeof(BattleLog), nameof(BattleLog.RemoveEntry))]
    internal static class Patch_CABattleLogSpatialRemove
    {
        [HarmonyPostfix]
        internal static void Postfix(BattleLog __instance, LogEntry log)
        {
            if (log == null) return;
            try
            {
                Game game = Current.Game;
                if (game == null || game.battleLog != __instance) return;
                game.GetComponent<CACombatSpatialLogComponent>()
                    ?.Remove(log.LogID);
            }
            catch
            {
                // Retention bookkeeping is strictly best-effort observation.
                CACombatSpatialLogComponent.ReportTopologyObservationFailure(
                    -1, "battle-log-remove-entry");
            }
        }
    }

    [HarmonyPatch(typeof(BattleLog), nameof(BattleLog.Notify_PawnDiscarded))]
    internal static class Patch_CABattleLogSpatialPawnDiscarded
    {
        [HarmonyPostfix]
        internal static void Postfix(BattleLog __instance)
        {
            try
            {
                Game game = Current.Game;
                if (game == null || game.battleLog != __instance) return;
                game.GetComponent<CACombatSpatialLogComponent>()
                    ?.PruneToRetainedBattleLog();
            }
            catch
            {
                // Native retention remains authoritative if observation fails.
                CACombatSpatialLogComponent.ReportTopologyObservationFailure(
                    -1, "battle-log-pawn-discarded");
            }
        }
    }

    [HarmonyPatch(typeof(LogEntry), nameof(LogEntry.ToGameStringFromPOV))]
    internal static class Patch_CALogEntrySpatialString
    {
        [HarmonyPostfix]
        internal static void Postfix(LogEntry __instance, ref string __result)
        {
            if (__instance == null || __result == null) return;
            try
            {
                AwarenessSettings settings = AwarenessMod.Settings;
                if (settings == null || !settings.traceBehavior) return;
                CACombatSpatialLogComponent component =
                    CACombatSpatialLogComponent.CurrentComponent();
                CACombatSpatialLogRecord record;
                if (component != null
                    && component.TryGet(__instance.LogID, out record))
                    __result += record.StableSuffix();
            }
            catch
            {
                // An unavailable snapshot leaves the vanilla string byte-for-byte intact.
            }
        }
    }
}
