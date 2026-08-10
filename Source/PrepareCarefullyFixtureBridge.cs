using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Dev-only bridge for reconstructing the fixture's missing second starting pawn
    // from the operator's exact Prepare Carefully character preset. Reflection keeps
    // Prepare Carefully optional for normal Colonist Awareness play.
    internal static class CAPrepareCarefullyFixtureBridge
    {
        private const string PresetName = "Dolly";
        private const string LoaderTypeName =
            "EdB.PrepareCarefully.PawnLoaderV5";
        private const string HealthProviderTypeName =
            "EdB.PrepareCarefully.ProviderHealthOptions";
        private const string PassionProviderTypeName =
            "EdB.PrepareCarefully.ProviderPassions";
        private const string CustomizerTypeName =
            "EdB.PrepareCarefully.PawnCustomizer";
        private const string ReflectionCacheTypeName =
            "EdB.PrepareCarefully.ReflectionCache";

        public static bool TryRestoreDolly(Map map, out Pawn dolly,
            out string outcome)
        {
            dolly = null;
            string stage = "preflight";
            if (map == null)
            {
                outcome = "no current map";
                return false;
            }

            Pawn existing = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead
                .FirstOrDefault(IsExactDollyIdentity);
            if (existing != null)
            {
                outcome = "Kira \"Dolly\" Cole already exists as "
                    + existing.GetUniqueLoadID();
                return false;
            }

            Pawn luc = map.mapPawns.FreeColonistsSpawned
                .FirstOrDefault(IsExactLucIdentity);
            if (luc == null)
            {
                outcome = "Lucien \"Luc\" Frank is not a spawned colonist";
                return false;
            }
            Pawn currentSpouse = luc.GetFirstSpouse();
            if (currentSpouse != null)
            {
                outcome = "Luc already has a live spouse relation with "
                    + currentSpouse.LabelShort;
                return false;
            }

            try
            {
                stage = "resolving Prepare Carefully services";
                Type loaderType = ResolvePrepareCarefullyType(LoaderTypeName);
                Type healthType = ResolvePrepareCarefullyType(
                    HealthProviderTypeName);
                Type passionType = ResolvePrepareCarefullyType(
                    PassionProviderTypeName);
                Type customizerType = ResolvePrepareCarefullyType(
                    CustomizerTypeName);
                Type reflectionCacheType = ResolvePrepareCarefullyType(
                    ReflectionCacheTypeName);
                if (loaderType == null || healthType == null
                    || passionType == null || customizerType == null
                    || reflectionCacheType == null)
                {
                    outcome = "Prepare Carefully 1.6 is not active";
                    return false;
                }

                stage = "initializing Prepare Carefully reflection";
                object reflectionCache = reflectionCacheType
                    .GetProperty("Instance", BindingFlags.Public
                        | BindingFlags.Static)?.GetValue(null, null);
                reflectionCacheType.GetMethod("Initialize",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(reflectionCache, null);

                stage = "constructing Prepare Carefully loader";
                object loader = Activator.CreateInstance(loaderType);
                object healthProvider = Activator.CreateInstance(healthType);
                object passionProvider = Activator.CreateInstance(passionType);
                passionType.GetMethod("PostConstruct",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(passionProvider, null);
                loaderType.GetProperty("ProviderHealthOptions")
                    ?.SetValue(loader, healthProvider, null);
                loaderType.GetProperty("ProviderPassions")
                    ?.SetValue(loader, passionProvider, null);

                stage = "loading Dolly.pcc";
                MethodInfo load = loaderType.GetMethod("Load",
                    BindingFlags.Public | BindingFlags.Instance);
                object result = load?.Invoke(loader,
                    new object[] { PresetName });
                if (result == null)
                {
                    outcome = "Prepare Carefully returned no Dolly preset";
                    return false;
                }

                string problem;
                if (TryFirstProblem(result, out problem))
                {
                    outcome = "Prepare Carefully rejected an exact restore: "
                        + problem;
                    return false;
                }

                stage = "reading Dolly customizations";
                object customized = result.GetType().GetProperty("Pawn")
                    ?.GetValue(result, null);
                object customizations = customized?.GetType()
                    .GetProperty("Customizations")?.GetValue(customized, null);
                if (customizations == null)
                {
                    outcome = "Dolly preset contained no pawn customizations";
                    return false;
                }

                stage = "generating Dolly pawn";
                object customizer = Activator.CreateInstance(customizerType);
                MethodInfo create = customizerType.GetMethod(
                    "CreatePawnFromCustomizations",
                    BindingFlags.Public | BindingFlags.Instance);
                GameInitData originalInitData = Current.Game.InitData;
                try
                {
                    if (originalInitData == null)
                        Current.Game.InitData = new GameInitData();
                    dolly = create?.Invoke(customizer,
                        new[] { customizations }) as Pawn;
                }
                finally
                {
                    Current.Game.InitData = originalInitData;
                }
                if (dolly == null || !IsExactDollyIdentity(dolly))
                {
                    DiscardUnspawnedPawn(dolly);
                    dolly = null;
                    outcome = "Prepare Carefully did not produce the exact Dolly identity";
                    return false;
                }

                stage = "spawning Dolly pawn";
                if (dolly.Faction != Faction.OfPlayer)
                    dolly.SetFaction(Faction.OfPlayer);
                IntVec3 cell = CellFinder.RandomSpawnCellForPawnNear(
                    luc.Position, map);
                GenSpawn.Spawn(dolly, cell, map);
                dolly.relations.AddDirectRelation(PawnRelationDefOf.Spouse, luc);

                outcome = "restored " + dolly.Name.ToStringFull
                    + " from Prepare Carefully preset " + PresetName
                    + " at " + cell + "; spouse Luc";
                return true;
            }
            catch (Exception exception)
            {
                DiscardUnspawnedPawn(dolly);
                dolly = null;
                Exception cause = exception is TargetInvocationException
                    && exception.InnerException != null
                    ? exception.InnerException
                    : exception;
                Log.Error("[CA] exact Dolly fixture failed while " + stage
                    + ":\n" + cause);
                outcome = stage + ": " + cause.GetType().Name + ": "
                    + cause.Message;
                return false;
            }
        }

        private static Type ResolvePrepareCarefullyType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName,
                    throwOnError: false))
                .FirstOrDefault(type => type != null);
        }

        private static bool TryFirstProblem(object loaderResult,
            out string problem)
        {
            problem = null;
            IEnumerable problems = loaderResult.GetType()
                .GetProperty("Problems")?.GetValue(loaderResult, null)
                as IEnumerable;
            if (problems == null) return false;
            foreach (object entry in problems)
            {
                if (entry == null) continue;
                problem = entry.GetType().GetProperty("Message")
                    ?.GetValue(entry, null) as string;
                if (string.IsNullOrEmpty(problem))
                    problem = "unspecified preset problem";
                return true;
            }
            return false;
        }

        internal static bool IsExactDollyIdentity(Pawn pawn)
        {
            NameTriple name = pawn?.Name as NameTriple;
            return name != null && name.First == "Kira" && name.Nick == "Dolly"
                && name.Last == "Cole";
        }

        internal static bool IsExactLucIdentity(Pawn pawn)
        {
            NameTriple name = pawn?.Name as NameTriple;
            return name != null && name.First == "Lucien" && name.Nick == "Luc"
                && name.Last == "Frank";
        }

        private static void DiscardUnspawnedPawn(Pawn pawn)
        {
            if (pawn != null && !pawn.Spawned && !pawn.Destroyed)
                pawn.Destroy();
        }
    }
}
