using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 代码化布局（阶段 C 试点）：把「视图专属资源」并进视图包，其余资源回退 Meta 布局。
/// <para>
/// 试点只收编 <b>HelpPanel</b>：
/// </para>
/// <list type="bullet">
///   <item><c>Assets/Game/UI/View/HelpPanel/**</c>（prefab、根目录图片、Images 图集与源图）</item>
///   <item><c>Assets/Game/UI/Sprites/helppanel/**</c>（视图专属散图，原本每张一个包）</item>
/// </list>
/// <para>合并后统一进 <c>ui/view/helppanel_prefab</c>，让 prefab 与图片同包，依赖归零。</para>
/// <para>
/// 其余资源仍使用 <see cref="MetaLayout"/> 的结果；被收编的资源会从原包中移除，
/// 因此原 <c>ui/sprites/helppanel/*</c> 的 25 个包会消失。
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
        },
    };

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

        // 以 Meta 布局为底：保证未收编的资源一个都不丢。
        if (!MetaLayout.Instance.TryCreate(target, out List<AssetBundleBuild> baseBuilds, out error))
        {
            return false;
        }

        var movedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var viewBundles = new List<AssetBundleBuild>();
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

            if (!TryCreateBuild(view.BundleName, assets, out AssetBundleBuild build, out string buildError))
            {
                error = buildError;
                return false;
            }

            viewBundles.Add(build);
            messages.Add(string.Format("{0}: {1} 个资源 → {2}", view.ViewName, assets.Count, view.BundleName));
        }

        // 从 Meta 结果里移除被收编的资源；变空的包直接丢弃。
        var result = new List<AssetBundleBuild>(baseBuilds.Count + viewBundles.Count);
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

        result.AddRange(viewBundles);
        builds = result;

        Debug.Log(string.Format(
            "[CodeLayout] 试点收编完成：新增 {0} 个视图包，移除 {1} 个空包，其余 {2} 个包沿用 Meta 布局。\n  {3}",
            viewBundles.Count,
            removedBundles,
            result.Count - viewBundles.Count,
            string.Join("\n  ", messages)));
        return true;
    }

    private static List<string> CollectAssets(ViewPackage view, HashSet<string> movedPaths)
    {
        var collected = new List<string>();
        CollectFromFolder(view.ViewRoot, collected);
        CollectFromFolder(view.SpritesRoot, collected);

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

    private static void CollectFromFolder(string folder, List<string> output)
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

            output.Add(path);
        }
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
