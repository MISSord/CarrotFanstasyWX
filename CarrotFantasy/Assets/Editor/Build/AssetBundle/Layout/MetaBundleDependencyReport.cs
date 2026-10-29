using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把 meta 整包里的资源按类型分组，并查看它们的直接依赖有没有包名。
/// 只出报告，不改变要打进 SBP 的包内容。
/// </summary>
public static class MetaBundleDependencyReport
{
    private const int SampleLimit = 12;

    public static void Log(IList<AssetBundleBuild> builds)
    {
        if (builds == null || builds.Count == 0)
        {
            Debug.Log("[AB Build][依赖] 没有已入包资源。");
            return;
        }

        var assetsByType = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var bundlesByType = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        int assetCount = 0;

        for (int i = 0; i < builds.Count; i++)
        {
            AssetBundleBuild build = builds[i];
            if (build.assetNames == null)
            {
                continue;
            }

            for (int assetIndex = 0; assetIndex < build.assetNames.Length; assetIndex++)
            {
                string assetPath = build.assetNames[assetIndex];
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                string typeName = Classify(assetPath);
                if (!assetsByType.TryGetValue(typeName, out List<string> assets))
                {
                    assets = new List<string>();
                    assetsByType.Add(typeName, assets);
                    bundlesByType.Add(typeName, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                }

                assets.Add(assetPath);
                bundlesByType[typeName].Add(build.assetBundleName);
                assetCount++;
            }
        }

        var withBundle = new Dictionary<string, int>(StringComparer.Ordinal);
        var withoutBundleCount = new Dictionary<string, int>(StringComparer.Ordinal);
        var withoutBundleSamples = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var seenDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int dependencyCount = 0;
        int namedDependencyCount = 0;

        foreach (KeyValuePair<string, List<string>> pair in assetsByType)
        {
            for (int i = 0; i < pair.Value.Count; i++)
            {
                string assetPath = pair.Value[i].Replace('\\', '/');
                string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
                for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                {
                    string dependency = dependencies[dependencyIndex].Replace('\\', '/');
                    if (string.IsNullOrEmpty(dependency)
                        || string.Equals(dependency, assetPath, StringComparison.OrdinalIgnoreCase)
                        || !seenDependencies.Add(dependency))
                    {
                        continue;
                    }

                    dependencyCount++;
                    string dependencyType = Classify(dependency);
                    if (HasBundleName(dependency))
                    {
                        namedDependencyCount++;
                        AddCount(withBundle, dependencyType);
                        continue;
                    }

                    AddCount(withoutBundleCount, dependencyType);
                    if (!withoutBundleSamples.TryGetValue(dependencyType, out List<string> samples))
                    {
                        samples = new List<string>();
                        withoutBundleSamples.Add(dependencyType, samples);
                    }

                    if (samples.Count < SampleLimit)
                    {
                        samples.Add(dependency);
                    }
                }
            }
        }

        var report = new StringBuilder();
        report.Append("[AB Build][依赖] meta 整包 ").Append(builds.Count)
            .Append(" 个，资源 ").Append(assetCount).AppendLine(" 个。按类型：");
        foreach (KeyValuePair<string, List<string>> pair in assetsByType)
        {
            report.Append("  ").Append(pair.Key)
                .Append("：资源 ").Append(pair.Value.Count)
                .Append("，包 ").Append(bundlesByType[pair.Key].Count)
                .AppendLine();
        }

        report.Append("直接依赖去重后 ").Append(dependencyCount)
            .Append(" 个：已有包名 ").Append(namedDependencyCount)
            .Append("，没有包名 ").Append(dependencyCount - namedDependencyCount)
            .AppendLine("。");

        if (withBundle.Count > 0)
        {
            report.AppendLine("已有包名的依赖：");
            foreach (KeyValuePair<string, int> pair in withBundle)
            {
                report.Append("  ").Append(pair.Key).Append(" ").Append(pair.Value).AppendLine();
            }
        }

        if (withoutBundleCount.Count > 0)
        {
            report.AppendLine("没有包名的依赖：");
            foreach (KeyValuePair<string, int> pair in withoutBundleCount)
            {
                report.Append("  ").Append(pair.Key).Append(" ").Append(pair.Value).AppendLine(" 个");
                if (!withoutBundleSamples.TryGetValue(pair.Key, out List<string> samples))
                {
                    continue;
                }

                for (int i = 0; i < samples.Count; i++)
                {
                    report.Append("    ").AppendLine(samples[i]);
                }

                if (pair.Value > samples.Count)
                {
                    report.Append("    … 其余 ").Append(pair.Value - samples.Count).AppendLine(" 个未列出");
                }
            }
        }

        Debug.Log(report.ToString());
    }

    private static void AddCount(Dictionary<string, int> counts, string typeName)
    {
        counts.TryGetValue(typeName, out int count);
        counts[typeName] = count + 1;
    }

    private static bool HasBundleName(string assetPath)
    {
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        return importer != null && !string.IsNullOrEmpty(importer.assetBundleName);
    }

    private static string Classify(string assetPath)
    {
        string extension = Path.GetExtension(assetPath);
        if (string.IsNullOrEmpty(extension))
        {
            return "其他";
        }

        switch (extension.ToLowerInvariant())
        {
            case ".prefab":
                return "预制体";
            case ".unity":
                return "场景";
            case ".png":
            case ".jpg":
            case ".jpeg":
            case ".tga":
            case ".psd":
            case ".tif":
            case ".tiff":
            case ".bmp":
            case ".exr":
                return "图片";
            case ".spriteatlas":
                return "图集";
            case ".anim":
            case ".controller":
            case ".overridecontroller":
            case ".playable":
                return "动画";
            case ".mat":
                return "材质";
            case ".shader":
            case ".shadergraph":
            case ".shadersubgraph":
            case ".compute":
                return "着色器";
            case ".fbx":
            case ".obj":
            case ".dae":
                return "模型";
            case ".wav":
            case ".mp3":
            case ".ogg":
                return "音频";
            case ".json":
            case ".txt":
            case ".xml":
            case ".bytes":
            case ".csv":
                return "文本";
            case ".cs":
            case ".dll":
                return "脚本";
            case ".asset":
                return "序列化资产";
            default:
                return "其他";
        }
    }
}
