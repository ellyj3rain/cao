using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Module 4: the colony handles its own dead and its enemies' dead. Humanlike corpses
    // are unforbidden once the map is calm, and when unburied corpses outnumber open
    // graves, new grave blueprints are placed beside the existing graveyard.
    public class CorpseDisciplineMapComponent : MapComponent
    {
        private const int IntervalTicks = 1800;

        public CorpseDisciplineMapComponent(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % IntervalTicks != 0) return;
            var s = AwarenessMod.Settings;
            if (s == null) return;
            try
            {
                if (map.dangerWatcher.DangerRating != StoryDanger.None) return;

                var corpses = ListUnburiedHumanlikeCorpses();
                if (corpses.Count == 0) return;

                if (s.autoUnforbidCorpses)
                {
                    for (int i = 0; i < corpses.Count; i++)
                    {
                        if (corpses[i].IsForbidden(Faction.OfPlayer))
                            corpses[i].SetForbidden(false, false);
                    }
                }

                if (s.autoDigGraves)
                {
                    int capacity = CountOpenGravesAndPlans();
                    int deficit = corpses.Count - capacity;
                    if (deficit > 0) PlaceGraves(deficit);
                }
            }
            catch { }
        }

        private List<Corpse> ListUnburiedHumanlikeCorpses()
        {
            var result = new List<Corpse>();
            var things = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
            for (int i = 0; i < things.Count; i++)
            {
                var c = things[i] as Corpse;
                if (c == null || !c.Spawned) continue;
                var p = c.InnerPawn;
                if (p == null || !p.RaceProps.Humanlike) continue;
                result.Add(c);
            }
            return result;
        }

        private int CountOpenGravesAndPlans()
        {
            int n = 0;
            var graves = map.listerThings.ThingsOfDef(ThingDefOf.Grave);
            for (int i = 0; i < graves.Count; i++)
            {
                var g = graves[i] as Building_Grave;
                if (g != null && !g.HasCorpse) n++;
            }
            var blueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint);
            for (int i = 0; i < blueprints.Count; i++)
            {
                var b = blueprints[i] as Blueprint_Build;
                if (b != null && b.def.entityDefToBuild == ThingDefOf.Grave) n++;
            }
            var frames = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame);
            for (int i = 0; i < frames.Count; i++)
            {
                var f = frames[i] as Frame;
                if (f != null && f.def.entityDefToBuild == ThingDefOf.Grave) n++;
            }
            return n;
        }

        private void PlaceGraves(int wanted)
        {
            // Seed from the existing graveyard: no graves yet means the player picks the
            // site first; we only ever extend.
            var graves = map.listerThings.ThingsOfDef(ThingDefOf.Grave);
            if (graves.Count == 0) return;

            IntVec3 centroid = IntVec3.Zero;
            for (int i = 0; i < graves.Count; i++) centroid += graves[i].Position;
            centroid = new IntVec3(centroid.x / graves.Count, 0, centroid.z / graves.Count);
            Rot4 rot = graves[0].Rotation;

            int placed = 0;
            int limit = GenRadial.NumCellsInRadius(14f);
            for (int i = 0; i < limit && placed < wanted && placed < 10; i++)
            {
                IntVec3 c = centroid + GenRadial.RadialPattern[i];
                if (!c.InBounds(map)) continue;
                if (!GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Grave, c, rot, map).Accepted) continue;
                GenConstruct.PlaceBlueprintForBuild(ThingDefOf.Grave, c, map, rot, Faction.OfPlayer, null);
                placed++;
            }
            if (placed > 0)
            {
                Messages.Message(
                    "Colonist Awareness: " + placed + " grave" + (placed == 1 ? "" : "s") + " planned beside the graveyard.",
                    new TargetInfo(centroid, map), MessageTypeDefOf.SilentInput, historical: false);
            }
        }
    }
}
