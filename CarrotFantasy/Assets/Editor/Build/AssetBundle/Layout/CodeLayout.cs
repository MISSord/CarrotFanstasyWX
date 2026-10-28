using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 代码化布局（阶段 C 试点）：把「视图专属散图」并进视图包，其余资源回退 Meta 布局。
/// <para>
/// 试点只处理 <b>HelpPanel</b>：
/// </para>
/// <list type="bullet">
///   <item><c>Assets/Game/UI/Sprites/helppanel/**</c>（31 张视图散图，原本每张一个包）</item>
///   <item>并入 <c>ui/view/helppanel_prefab</c>（prefab 已在该包）</item>
/// </list>
/// <para>
/// <b>刻意保留</b> <c>Assets/Game/UI/View/HelpPanel/Images/**</c> 在自己的
/// <c>ui/view/helppanel/images_atlas</c> 包里：prefab 上的 18 个 UIImageLoader 记录的
/// 是图集包名，AtlasLoader 只认名字含 images_atlas 的包；把它并进视图包会让
/// 运行时按旧包名加载失败。
/// </para>
/// <para>
/// 其余资源仍使用 <see cref="MetaLayout"/> 的结果；被收编的资源会从原包中移除，
/// 因此原 <c>ui/sprites/helppanel/*</c> 的 31 个包会消失。
/// </para>
/// </summary>
public sealed class CodeLayout : IAssetBundleLayout
{
    private sealed class ViewPackage
    {
        public string ViewName;
        public string ViewRoot;
        public string SpritesRoot;
        public string BundleName;

        /// <summary>这些目录下的资源不并入视图包（保持原图集包，供 AtlasLoader 使用）。</summary>
        public string[] ExcludeRoots;
    }

    // 阶段 C 试点：只收编 HelpPanel；验证收益后再逐视图推广。
    private static readonly ViewPackage[] PilotViews =
    {
        new ViewPackage
        {
            ViewName = "HelpPanel",
            ViewRoot = "Assets/Game/UI/View/HelpPanel",
            SpritesRoot = "Assets/Game/UI/Sprites/helppanel",
            BundleName = "ui/view/helppanel_prefab",
            ExcludeRoots = new[] { "Assets/Game/UI/View/HelpPanel/Images" },
        },
    };

    /// <summary>
    /// 最近一次 Code 布局实际产出的包名（供清单阶段校验/过滤依赖）。
    /// 只在编辑器构建流程内使用，单线程。
    /// </summary>
    public static IReadOnlyCollection<string> LastBundleNames { get; private set; }

    public string Name
    {
        get { return "Code"; }
    }

    public bool TryCreate(
        BuildTarget target,
        out List<AssetBundleBuild> builds,
        out string error)
    {
        builds = null;
        error = null;
        LastBundleNames = null;

        // 以 Meta 布局为底：保证未收编的资源一个都不丢。
        if (!MetaLayout.Instance.TryCreate(target, out List<AssetBundleBuild> baseBuilds, out error))
        {
            return false;
        }

        var movedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var viewAssets = new List<KeyValuePair<string, List<string>>>();
        var messages = new List<string>();

        for (int i = 0; i < PilotViews.Length; i++)
        {
            ViewPackage view = PilotViews[i];
            List<string> assets = CollectAssets(view, movedPaths);
            if (assets.Count == 0)
            {
                messages.Add(view.ViewName + ": 未找到任何资源，已跳过");
                continue;
            }

            viewAssets.Add(new KeyValuePair<string, List<string>>(view.BundleName, assets));
            messages.Add(string.Format("{0}: {1} 个资源 → {2}", view.ViewName, assets.Count, view.BundleName));
        }

        // 从 Meta 结果里移除被收编的资源；变空的包直接丢弃。
        var result = new List<AssetBundleBuild>(baseBuilds.Count);
        int removedBundles = 0;
        for (int i = 0; i < baseBuilds.Count; i++)
        {
            AssetBundleBuild build = baseBuilds[i];
            var keptAssets = new List<string>(build.assetNames.Length);
            var keptAddresses = new List<string>(build.assetNames.Length);

            for (int assetIndex = 0; assetIndex < build.assetNames.Length; assetIndex++)
            {
                string assetPath = build.assetNames[assetIndex];
                if (movedPaths.Contains(assetPath))
                {
                    continue;
                }

                keptAssets.Add(assetPath);
                keptAddresses.Add(
                    build.addressableNames != null && assetIndex < build.addressableNames.Length
                        ? build.addressableNames[assetIndex]
                        : Path.GetFileNameWithoutExtension(assetPath));
            }

            if (keptAssets.Count == 0)
            {
                removedBundles++;
                continue;
            }

            build.assetNames = keptAssets.ToArray();
            build.addressableNames = keptAddresses.ToArray();
            result.Add(build);
        }

        // 把收编的资源合并进目标包：目标包已存在（如视图 prefab 包）则追加，否则新建。
        for (int i = 0; i < viewAssets.Count; i++)
        {
            string bundleName = viewAssets[i].Key;
            List<string> assets = viewAssets[i].Value;

            int existingIndex = FindBundle(result, bundleName);
            if (existingIndex >= 0)
            {
                var combined = new List<string>(result[existingIndex].assetNames);
                for (int assetIndex = 0; assetIndex < assets.Count; assetIndex++)
                {
                    if (!combined.Contains(assets[assetIndex]))
                    {
                        combined.Add(assets[assetIndex]);
                    }
                }

                if (!TryCreateBuild(bundleName, combined, out AssetBundleBuild merged, out string mergeError))
                {
                    error = mergeError;
                    return false;
                }

                result[existingIndex] = merged;
            }
            else
            {
                if (!TryCreateBuild(bundleName, assets, out AssetBundleBuild build, out string buildError))
                {
                    error = buildError;
                    return false;
                }

                result.Add(build);
            }
        }

        builds = result;

        var resultNames = new List<string>(result.Count);
        for (int i = 0; i < result.Count; i++)
        {
            resultNames.Add(result[i].assetBundleName);
        }

        LastBundleNames = resultNames;

        Debug.Log(string.Format(
            "[CodeLayout] 试点收编完成：合并 {0} 个视图包，移除 {1} 个空包，最终 {2} 个包。\n  {3}",
            viewAssets.Count,
            removedBundles,
            result.Count,
            string.Join("\n  ", messages)));
        return true;
    }

    private static int FindBundle(List<AssetBundleBuild> builds, string bundleName)
    {
        for (int i = 0; i < builds.Count; i++)
        {
            if (string.Equals(builds[i].assetBundleName, bundleName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static List<string> CollectAssets(ViewPackage view, HashSet<string> movedPaths)
    {
        var collected = new List<string>();
        CollectFromFolder(view.ViewRoot, view.ExcludeRoots, collected);
        CollectFromFolder(view.SpritesRoot, null, collected);

        var unique = new List<string>(collected.Count);
        for (int i = 0; i < collected.Count; i++)
        {
            // 同一资源可能被两个根目录覆盖（例如 Sprites 被软链到 View 下），去重。
            if (movedPaths.Add(collected[i]))
            {
                unique.Add(collected[i]);
            }
        }

        return unique;
    }

    private static void CollectFromFolder(string folder, string[] excludeRoots, List<string> output)
    {
        if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path)
                || AssetDatabase.IsValidFolder(path)
                || path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsUnderAny(path, excludeRoots))
            {
                continue;
            }

            output.Add(path);
        }
    }

    private static bool IsUnderAny(string assetPath, string[] roots)
    {
        if (roots == null || roots.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < roots.Length; i++)
        {
            string root = roots[i];
            if (string.IsNullOrEmpty(root))
            {
                continue;
            }

            if (string.Equals(assetPath, root, StringComparison.OrdinalIgnoreCase)
                || assetPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCreateBuild(
        string bundleName,
        List<string> assets,
        out AssetBundleBuild build,
        out string error)
    {
        build = default;
        error = null;

        var addresses = new string[assets.Count];
        var usedAddresses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var conflicts = new List<string>();

        for (int i = 0; i < assets.Count; i++)
        {
            string assetPath = assets[i];
            string address = Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(address))
            {
                conflicts.Add(bundleName + " 含无法命名的资源: " + assetPath);
                continue;
            }

            if (usedAddresses.TryGetValue(address, out string existingPath))
            {
                conflicts.Add(
                    bundleName + " 内加载名冲突 \"" + address + "\": " + existingPath + " 与 " + assetPath);
                continue;
            }

            usedAddresses.Add(address, assetPath);
            addresses[i] = address;
        }

        if (conflicts.Count > 0)
        {
            error = "Code 布局的加载名冲突，已中止：\n" + string.Join("\n", conflicts);
            return false;
        }

        build = new AssetBundleBuild
        {
            assetBundleName = bundleName,
            assetNames = assets.ToArray(),
            addressableNames = addresses,
        };
        return true;
    }
}
