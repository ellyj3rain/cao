using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Read-only animal-infrastructure evidence. Containment, sleeping,
    // companionship, culture, and defensive posture remain separate axes.
    // This evaluator neither paints a program nor installs/builds anything.
    internal static class CAAnimalInfrastructureModule
    {
        internal static string Evaluate(Map map)
        {
            if (map == null || !map.IsPlayerHome)
                return "[CA] animal infrastructure evaluation\nrefused; no "
                    + "authorized player-home map is loaded";

            List<Pawn> animals = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn != null && !pawn.Dead
                    && pawn.Faction == Faction.OfPlayer
                    && pawn.RaceProps?.Animal == true)
                .OrderBy(pawn => pawn.thingIDNumber).ToList();
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned
                .Where(pawn => pawn != null && !pawn.Dead)
                .OrderBy(pawn => pawn.thingIDNumber).ToList();
            PlannedUseMapComponent programs = PlannedUseMapComponent.For(map);
            IReadOnlyList<CASpaceProgram> observedPrograms =
                programs?.ProgramsForObservation
                ?? Array.Empty<CASpaceProgram>();
            List<CASpaceProgram> defenses = observedPrograms
                .Where(program => program != null
                    && program.author == CASpaceAuthor.Player
                    && program.purpose == CASpacePurpose.Defense)
                .OrderBy(program => program.id).ToList();
            List<Building_MechCharger> chargers = map.listerBuildings
                .allBuildingsColonist.OfType<Building_MechCharger>()
                .Where(charger => charger != null && charger.Spawned)
                .OrderBy(charger => charger.thingIDNumber).ToList();
            List<Building_Bed> installedBeds = map.listerBuildings
                .allBuildingsColonist.OfType<Building_Bed>()
                .Where(IsAnimalBed).OrderBy(bed => bed.thingIDNumber)
                .ToList();
            List<MinifiedThing> minifiedBeds = map.listerThings.AllThings
                .OfType<MinifiedThing>().Where(minified =>
                    IsAnimalBed(minified.InnerThing as Building_Bed))
                .OrderBy(minified => minified.thingIDNumber).ToList();
            int installedSlots = installedBeds.Sum(bed =>
                bed.SleepingSlotsCount);
            int assignedInstalledBeds = installedBeds.Count(bed =>
                bed.OwnersForReading.Count > 0);
            int penMarkers = map.listerBuildings
                .allBuildingsAnimalPenMarkers.Count;
            int enclosedPens = map.listerBuildings
                .allBuildingsAnimalPenMarkers.Count(building =>
                    building?.TryGetComp<CompAnimalPenMarker>()?.PenState?
                        .Enclosed == true);
            int hitchingPosts = map.listerBuildings
                .allBuildingsHitchingPosts.Count;

            var builder = new StringBuilder();
            builder.AppendLine("[CA] animal infrastructure evaluation")
                .Append("read-only; player animals ").Append(animals.Count)
                .Append("; rope-managed now ")
                .Append(animals.Count(AnimalPenUtility
                    .NeedsToBeManagedByRope))
                .Append("; installed animal beds ")
                .Append(installedBeds.Count).Append(" / slots ")
                .Append(installedSlots).Append(" / assigned ")
                .Append(assignedInstalledBeds)
                .Append("; minified installable animal-bed assets ")
                .Append(minifiedBeds.Count).AppendLine()
                .Append("pen markers ").Append(penMarkers)
                .Append("; enclosed ").Append(enclosedPens)
                .Append("; hitching posts ").Append(hitchingPosts)
                .Append("; authored Animal programs ")
                .Append(observedPrograms.Count(program => program != null
                    && program.author == CASpaceAuthor.Player
                    && program.purpose == CASpacePurpose.Animal))
                .Append("; authored Defense programs ")
                .Append(defenses.Count).Append("; mech chargers ")
                .Append(chargers.Count).AppendLine();

            AppendFactionEvidence(builder, animals, colonists);

            if (minifiedBeds.Count == 0)
                builder.AppendLine("existing installable bed assets: none");
            else
            {
                builder.Append("existing installable bed assets: ")
                    .AppendLine(string.Join("; ", minifiedBeds.Select(
                        minified => minified.ThingID + " "
                            + minified.InnerThing.def.label + " at "
                            + minified.Position + " (installed function none)")
                        .ToArray()));
            }

            if (animals.Count == 0)
                builder.AppendLine("animals: none");
            for (int i = 0; i < animals.Count; i++)
                AppendAnimal(builder, animals[i], colonists, programs,
                    defenses, chargers);

            builder.Append("sentinel contract: native animal call and angry-call "
                + "sounds are audio only; they do not create RimWorld clamor "
                + "or Colonist Awareness threat knowledge. A deliberate "
                + "sentinel bed therefore remains future site potential until "
                + "an animal can perceive a real hostile, emit a grounded "
                + "alarm/report, and that report can reach actual residents. ")
                .Append(defenses.Count == 0
                    ? "No authored Defense program exists, so an approach, "
                        + "beachhead, or defended-side relation is explicitly absent."
                    : "Defense-program proximity is evidence only; a defended "
                        + "approach still requires an actual topology relation.")
                .Append(" No bed, pen, program, assignment, or construction "
                    + "was changed.");
            return builder.ToString();
        }

        private static void AppendAnimal(StringBuilder builder, Pawn animal,
            List<Pawn> colonists, PlannedUseMapComponent programs,
            List<CASpaceProgram> defenses,
            List<Building_MechCharger> chargers)
        {
            bool ropeManaged = AnimalPenUtility.NeedsToBeManagedByRope(animal);
            CompAnimalPenMarker currentPen = ropeManaged
                ? AnimalPenUtility.GetCurrentPenOf(animal,
                    allowUnenclosedPens: true) : null;
            string containment;
            if (!ropeManaged)
                containment = "native rope/pen management not required";
            else if (currentPen != null)
                containment = "current pen " + currentPen.InspectLabel
                    + " at " + currentPen.parent.Position + ", enclosed "
                    + currentPen.PenState.Enclosed;
            else
                containment = "rope-managed and currently unpenned; suitable "
                    + "enclosed pen "
                    + AnimalPenUtility.AnySuitablePens(animal,
                        allowUnenclosedPens: false)
                    + ", suitable unenclosed pen "
                    + AnimalPenUtility.AnySuitablePens(animal,
                        allowUnenclosedPens: true)
                    + ", suitable hitch "
                    + AnimalPenUtility.AnySuitableHitch(animal);

            List<Pawn> bonds = animal.relations?.DirectRelations
                .Where(relation => relation != null
                    && relation.def == PawnRelationDefOf.Bond
                    && relation.otherPawn != null && !relation.otherPawn.Dead)
                .Select(relation => relation.otherPawn)
                .OrderBy(pawn => pawn.thingIDNumber).ToList()
                ?? new List<Pawn>();
            List<Pawn> venerators = colonists.Where(colonist =>
                    colonist.Ideo?.IsVeneratedAnimal(animal) == true)
                .ToList();
            Building_Bed animalBed = animal.ownership?.OwnedBed;
            CASpaceProgram animalProgram = animalBed != null
                ? programs?.ProgramAt(animalBed.Position) : null;
            CASpaceProgram currentProgram = programs?.ProgramAt(
                animal.Position);
            string sleeping = animalBed == null
                ? "no assigned installed animal bed"
                : animalBed.def.label + " " + animalBed.ThingID + " at "
                    + animalBed.Position + ", program "
                    + ProgramLabel(animalProgram);
            string bondEvidence = bonds.Count == 0 ? "none" : string.Join(
                "; ", bonds.Select(bond => BondEvidence(bond, programs,
                    animalBed)).ToArray());
            string veneration = venerators.Count == 0 ? "none observed"
                : string.Join(", ", venerators.Select(pawn => pawn.LabelShort
                    + " [" + IdeoligionLabel(pawn.Ideo) + "]").ToArray());
            string culturalFeasibility = venerators.Count == 0
                ? "no spawned free-colonist veneration-specific constraint "
                    + "observed; "
                    + "care, containment, sleeping, bonds, masters, training, "
                    + "and topology remain independently applicable"
                : "veneration constrains hunting, slaughter, food use, and "
                    + "loss for the named adherents through native "
                    + "unwillingness and thoughts; it supplies no pen "
                    + "exemption or bed-location mandate";
            Pawn assignedMaster = animal.playerSettings?.Master;
            Pawn effectiveMaster = animal.playerSettings?.RespectedMaster;

            bool obedience = animal.training?.HasLearned(
                TrainableDefOf.Obedience) == true;
            bool release = animal.training?.HasLearned(
                TrainableDefOf.Release) == true;
            bool attackTarget = TrainableDefOf.AttackTarget != null
                && animal.training?.HasLearned(TrainableDefOf.AttackTarget)
                    == true;
            LifeStageAge lifeStage = animal.ageTracker?.CurLifeStageRace;
            bool nativeCall = lifeStage?.soundCall != null;
            bool nativeAngryCall = lifeStage?.soundAngry != null;
            float hearing = animal.health?.capacities != null
                ? animal.health.capacities.GetLevel(PawnCapacityDefOf.Hearing)
                : 0f;
            int defenseDistance = NearestProgramDistance(animal.Position,
                defenses);
            int chargerDistance = NearestBuildingDistance(animal.Position,
                chargers.Cast<Building>());

            builder.Append("animal ").Append(animal.ThingID).Append(" ")
                .Append(animal.LabelShort).Append(" [")
                .Append(animal.def.defName).Append(", type ")
                .Append(animal.RaceProps.animalType).Append("] at ")
                .Append(animal.Position).AppendLine()
                .Append("  containment: ").Append(containment).AppendLine()
                .Append("  sleeping: ").Append(sleeping)
                .Append("; current authored program ")
                .Append(ProgramLabel(currentProgram)).AppendLine()
                .Append("  culture: venerated by ").Append(veneration)
                .AppendLine()
                .Append("  cultural feasibility: ")
                .Append(culturalFeasibility).AppendLine()
                .Append("  bonds: ").Append(bondEvidence).AppendLine()
                .Append("  master: assigned ")
                .Append(PawnLabel(assignedMaster)).Append("; effective ")
                .Append(PawnLabel(effectiveMaster))
                .Append("; follow drafted ")
                .Append(animal.playerSettings?.followDrafted == true)
                .Append("; follow fieldwork ")
                .Append(animal.playerSettings?.followFieldwork == true)
                .AppendLine()
                .Append("  defensive evidence: combat power ")
                .Append(animal.kindDef?.combatPower.ToString("0.##") ?? "n/a")
                .Append("; hearing ").Append(hearing.ToString("0.00"))
                .Append("; obedience ").Append(obedience)
                .Append("; release ").Append(release)
                .Append("; attack-target training ").Append(attackTarget)
                .Append("; native call sound ").Append(nativeCall)
                .Append("; native angry-call sound ").Append(nativeAngryCall)
                .Append("; semantic alarm/report channel absent")
                .Append("; nearest authored Defense footprint ")
                .Append(Distance(defenseDistance))
                .Append("; nearest mech charger ")
                .Append(Distance(chargerDistance)).AppendLine();
        }

        private static void AppendFactionEvidence(StringBuilder builder,
            List<Pawn> animals, List<Pawn> colonists)
        {
            Faction playerFaction = Faction.OfPlayer;
            Ideo primary = playerFaction?.ideos?.PrimaryIdeo;
            builder.Append("faction: player ")
                .Append(playerFaction?.Name ?? "absent")
                .Append("; primary ideoligion ").Append(IdeoligionLabel(primary))
                .Append("; free colonists on map ").Append(colonists.Count)
                .AppendLine();

            var groups = colonists.GroupBy(pawn => pawn.Ideo)
                .OrderBy(group => group.Key?.id ?? int.MaxValue)
                .ThenBy(group => group.Key?.name ?? string.Empty,
                    StringComparer.Ordinal).ToList();
            if (groups.Count == 0)
            {
                builder.AppendLine("spawned free-colonist ideoligion evidence: "
                    + "none");
            }
            else
            {
                builder.AppendLine("spawned free-colonist ideoligion evidence:");
                for (int i = 0; i < groups.Count; i++)
                {
                    Ideo ideoligion = groups[i].Key;
                    List<Pawn> followers = groups[i]
                        .OrderBy(pawn => pawn.thingIDNumber).ToList();
                    List<ThingDef> venerated = ideoligion?.VeneratedAnimals?
                        .Where(def => def != null)
                        .OrderBy(def => def.defName, StringComparer.Ordinal)
                        .ToList() ?? new List<ThingDef>();
                    List<Pawn> present = ideoligion == null
                        ? new List<Pawn>()
                        : animals.Where(ideoligion.IsVeneratedAnimal)
                            .OrderBy(pawn => pawn.thingIDNumber).ToList();
                    string standing = ideoligion == null ? "no ideoligion"
                        : ideoligion == primary ? "player-faction primary"
                        : playerFaction?.ideos?.IsMinor(ideoligion) == true
                            ? "player-faction minor" : "resident";
                    builder.Append("  ").Append(IdeoligionLabel(ideoligion))
                        .Append(" [").Append(standing).Append("]: followers ")
                        .Append(followers.Count).Append(" [")
                        .Append(string.Join(", ", followers.Select(pawn =>
                            pawn.LabelShort).ToArray())).Append("]")
                        .Append("; venerated species ")
                        .Append(venerated.Count == 0 ? "none" : string.Join(
                            ", ", venerated.Select(def => def.label + " ["
                                + def.defName + "]").ToArray()))
                        .Append("; present matching animals ")
                        .Append(present.Count == 0 ? "none" : string.Join(", ",
                            present.Select(PawnLabel).ToArray()))
                        .AppendLine();
                }
            }

            builder.AppendLine("native veneration contract: where an ideoligion "
                + "has the loaded AnimalVenerated precept, its adherents are "
                + "unwilling to hunt or slaughter the species and receive "
                + "death, meat, and living-presence thoughts. The precept "
                + "supplies no native exemption from rope/pen management and "
                + "no native sleeping-location rule.");
        }

        private static string BondEvidence(Pawn bonded,
            PlannedUseMapComponent programs, Building_Bed animalBed)
        {
            Building_Bed residentBed = bonded.ownership?.OwnedBed;
            CASpaceProgram residentProgram = programs?.ResidentProgramFor(
                bonded);
            string proximity = animalBed == null || residentBed == null
                ? "installed bed proximity unavailable"
                : "animal-to-resident bed distance "
                    + animalBed.Position.DistanceTo(residentBed.Position)
                        .ToString("0.0");
            return bonded.LabelShort + " in " + ProgramLabel(residentProgram)
                + ", resident bed "
                + (residentBed == null ? "none" : residentBed.ThingID + " at "
                    + residentBed.Position) + ", " + proximity;
        }

        private static string ProgramLabel(CASpaceProgram program)
        {
            return program == null ? "none" : "#" + program.id + " "
                + CASpacePurposeInfo.Label(program.purpose) + " "
                + program.label;
        }

        private static string IdeoligionLabel(Ideo ideoligion)
        {
            return ideoligion == null ? "none" : ideoligion.name + " [Ideo_"
                + ideoligion.id + "]";
        }

        private static string PawnLabel(Pawn pawn)
        {
            return pawn == null ? "none" : pawn.LabelShort + " "
                + pawn.ThingID;
        }

        private static bool IsAnimalBed(Building_Bed bed)
        {
            return bed != null && bed.def?.IsBed == true
                && bed.def.building?.bed_humanlike == false;
        }

        private static int NearestProgramDistance(IntVec3 cell,
            List<CASpaceProgram> programs)
        {
            int nearest = int.MaxValue;
            for (int i = 0; i < programs.Count; i++)
                for (int c = 0; c < programs[i].cells.Count; c++)
                    nearest = Math.Min(nearest,
                        cell.DistanceToSquared(programs[i].cells[c]));
            return nearest == int.MaxValue ? int.MaxValue
                : (int)Math.Round(Math.Sqrt(nearest));
        }

        private static int NearestBuildingDistance(IntVec3 cell,
            IEnumerable<Building> buildings)
        {
            int nearest = int.MaxValue;
            foreach (Building building in buildings)
                nearest = Math.Min(nearest,
                    cell.DistanceToSquared(building.Position));
            return nearest == int.MaxValue ? int.MaxValue
                : (int)Math.Round(Math.Sqrt(nearest));
        }

        private static string Distance(int distance)
        {
            return distance == int.MaxValue ? "absent"
                : distance + " cells";
        }
    }
}
