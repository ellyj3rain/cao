using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Vehicle Framework's vendored bundles retain the upstream mod identity in
    // every internal asset name. RimWorld correctly asks those bundles under
    // CA's current folder/package identity. Alias that one bundle boundary
    // after normal lookup fails; do not patch the shared ContentFinder<T>
    // resolver used by textures, shaders, and official AudioClip bundles.
    [HarmonyPatch]
    internal static class CAVehicleFrameworkBundleIdentityPatch
    {
        private const string UpstreamRoot =
            "Assets/Data/SmashPhil.VehicleFramework/";

        private static readonly HashSet<string> LoggedAssets =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(AssetBundle),
                nameof(AssetBundle.LoadAsset),
                new[] { typeof(string), typeof(Type) });
        }

        private static void Postfix(AssetBundle __instance, string name,
            Type type, ref UnityEngine.Object __result)
        {
            if (__result != null || __instance == null
                || string.IsNullOrEmpty(name))
                return;

            ModContentPack content = AwarenessMod.Instance?.Content;
            List<AssetBundle> bundles = content?.assetBundles
                ?.loadedAssetBundles;
            if (bundles == null || !bundles.Contains(__instance)) return;

            string normalized = name.Replace('\\', '/');
            string folderRoot = "Assets/Data/" + content.FolderName + "/";
            string packageRoot = "Assets/Data/"
                + content.PackageIdPlayerFacing + "/";
            string relative = RemoveRoot(normalized, folderRoot)
                ?? RemoveRoot(normalized, packageRoot);
            if (relative == null) return;

            UnityEngine.Object asset = __instance.LoadAsset(
                UpstreamRoot + relative, type);
            if (asset == null) return;

            __result = asset;
            string key = type.FullName + ":" + relative;
            if (LoggedAssets.Add(key))
            {
                Log.Message("[CA][VehicleFramework] resolved bundled "
                    + type.Name + " " + relative
                    + " through its retained upstream identity.");
            }
        }

        private static string RemoveRoot(string path, string root)
        {
            return path.StartsWith(root,
                    StringComparison.OrdinalIgnoreCase)
                ? path.Substring(root.Length)
                : null;
        }
    }
}
