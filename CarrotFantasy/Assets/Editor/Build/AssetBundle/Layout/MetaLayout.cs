using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

/// <summary>
/// 现状布局：包名与资源归属来自资源 .meta（由 PrefabABPathPostprocessor 维护）。
/// <para>
/// 关键细节：每个资源都填 <c>addressableNames</c> = 文件名（不含扩展名）。
/// SBP 在 addressableNames 为空时会登记完整资产路径，而运行时用的是
/// <c>LoadAsset("Tower_4_3")</c> / <c>sprite.name</c> 这种短名，会加载不到。
/// </para>
/// </summary>
public sealed class MetaLayout : IAssetBundleLayout
{
    /// <summary>无状态，可直接复用。</summary>
    public static readonly MetaLayout Instance = new MetaLayout();

    public string Name
    {
        get { return "Meta"; }
    }

    public bool TryCreate(
        BuildTarget target,
        out List<AssetBundleBuild> builds,
        out string error)
    {
        builds = new List<AssetBundleBuild>();
        error = null;
        var conflicts = new List<string>();
        string[] names = AssetDatabase.GetAllAssetBundleNames();

        for (int i = 0; i < names.Length; i++)
        {
            string bundleName = names[i];
            string[] assets = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);

            // 没有资源的包名（目录残留 / 历史脏数据）不传给构建后端，
            // 否则会产出 0 字节包或直接报错。
            if (assets == null || assets.Length == 0)
            {
                continue;
            }

            var addresses = new string[assets.Length];
            var usedAddresses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
            {
                string assetPath = assets[assetIndex];
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
                addresses[assetIndex] = address;
            }

            if (conflicts.Count > 0)
            {
                continue;
            }

            builds.Add(new AssetBundleBuild
            {
                assetBundleName = bundleName,
                assetNames = assets,
                addressableNames = addresses,
            });
        }

        if (conflicts.Count == 0)
        {
            return true;
        }

        builds = null;
        error = "Meta 布局的加载名与运行时不一致或互相冲突，已中止：\n" + string.Join("\n", conflicts);
        return false;
    }
}
