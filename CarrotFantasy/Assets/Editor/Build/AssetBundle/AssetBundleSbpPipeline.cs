using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEngine;

// SBP 的 BundleBuildParameters.BundleCompression 在 2018.3+ 用的是运行时类型
// UnityEngine.BuildCompression（见 SBP 源码 BuildParameters.cs 的 using 别名）；
// 同时 UnityEditor.Build.Content 里也有一个同名类型，不做别名会 CS0104 歧义。
using BuildCompression = UnityEngine.BuildCompression;

/// <summary>
/// Scriptable Build Pipeline（SBP）构建后端。
///
/// 与 built-in <c>BuildPipeline.BuildAssetBundles</c> 的差别：
/// <list type="bullet">
///   <item>输入是 <see cref="AssetBundleBuild"/> 列表，包内容由**代码**决定，
///         不再依赖资源 .meta 里的 assetBundleName；</item>
///   <item>可以传入自定义 <see cref="IBuildTask"/>，在依赖计算 / 写包之间插入逻辑
///         （依赖审计、资源裁剪、压缩策略、体积报告）；</item>
///   <item>配合 <c>BuildCache</c> 做增量构建（默认开启；强制重建会关闭）。</item>
/// </list>
///
/// <para>
/// 内容仍来自 meta 里已经登记的整包。打之前按资源类型分组，并记录直接依赖里
/// 哪些已经有包名、哪些还没有。
/// </para>
/// </summary>
public static class AssetBundleSbpPipeline
{
    private const string LogTag = "[AB Build][SBP]";

    /// <summary>
    /// 用 SBP 构建 AB 到 <paramref name="outputPath"/>。
    /// 包内容来自 meta 已登记的整包。
    /// </summary>
    public static bool Build(
        string outputPath,
        BuildTarget buildTarget,
        BuildAssetBundleOptions options,
        out string error,
        out IBundleBuildResults results)
    {
        error = null;
        results = null;

        if (!MetaLayout.Instance.TryCreate(buildTarget, out List<AssetBundleBuild> builds, out error))
        {
            return false;
        }

        MetaBundleDependencyReport.Log(builds);
        Debug.Log(LogTag + " 内容: Meta 整包");

        if (builds == null || builds.Count == 0)
        {
            error = "AssetDatabase 里没有任何已登记的 AB 包（检查 PrefabABPathPostprocessor 规则是否生效）";
            return false;
        }

        try
        {
            var content = new BundleBuildContent(builds);
            var parameters = new BundleBuildParameters(
                buildTarget,
                BuildPipeline.GetBuildTargetGroup(buildTarget),
                outputPath);

            // 与 built-in 的压缩选项一一对应：
            //   UncompressedAssetBundle → 不压缩
            //   ChunkBasedCompression    → LZ4（运行时随机读友好）
            //   None                     → LZMA（体积最小，首包用；运行时会转换）
            parameters.BundleCompression = ToBuildCompression(options);

            // built-in 的 ForceRebuild 不会自动作用到 SBP。缓存开着时，勾了强制重建仍会交回旧产物。
            if ((options & BuildAssetBundleOptions.ForceRebuildAssetBundle) != 0)
            {
                parameters.UseCache = false;
                Debug.Log(LogTag + " 强制重建：已关闭 SBP BuildCache");
            }
            else
            {
                // 默认 UseCache = true（SBP 构造函数的默认值）。
                // 命中缓存时构建会非常快，且还原出来的文件保留缓存时间戳 —— 属正常现象。
                Debug.Log(LogTag + " 使用 SBP BuildCache（增量）；需要全量请勾选「强制重新构建」");
            }

            IList<IBuildTask> tasks = DefaultBuildTasks.Create(DefaultBuildTasks.Preset.AssetBundleCompatible);
            tasks.Insert(FindStripInsertIndex(tasks), new StripExplicitBundleDependencies());

            ReturnCode code = ContentPipeline.BuildAssetBundles(parameters, content, out IBundleBuildResults buildResults, tasks);
            if (code != ReturnCode.Success)
            {
                error = "ContentPipeline.BuildAssetBundles 返回 " + code;
                return false;
            }

            LogBuildSummary(outputPath, builds.Count);
            results = buildResults;
            return true;
        }
        catch (Exception e)
        {
            // SBP 的异常信息通常带完整调用栈，直接透出便于定位。
            error = e.ToString();
            return false;
        }
    }

    static int FindStripInsertIndex(IList<IBuildTask> tasks)
    {
        int afterDependency = tasks.Count;
        for (int i = 0; i < tasks.Count; i++)
        {
            if (tasks[i] is StripUnusedSpriteSources)
            {
                return i + 1;
            }

            if (tasks[i] is CalculateAssetDependencyData)
            {
                afterDependency = i + 1;
            }
        }

        return afterDependency;
    }

    private static BuildCompression ToBuildCompression(BuildAssetBundleOptions options)
    {
        if ((options & BuildAssetBundleOptions.UncompressedAssetBundle) != 0)
        {
            return BuildCompression.Uncompressed;
        }

        if ((options & BuildAssetBundleOptions.ChunkBasedCompression) != 0)
        {
            return BuildCompression.LZ4;
        }

        return BuildCompression.LZMA;
    }

    private static void LogBuildSummary(string outputPath, int requestedBundleCount)
    {
        int fileCount = 0;
        long totalBytes = 0;
        try
        {
            if (Directory.Exists(outputPath))
            {
                string outputFolderName = Path.GetFileName(outputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                string packsPrefix = PackGroupingDefaults.PacksFolderName + "/";
                foreach (string file in Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories))
                {
                    string relative = file.Substring(outputPath.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace('\\', '/');
                    if (relative.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase)
                        || relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                        || relative.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                        || relative.EndsWith(".sig", StringComparison.OrdinalIgnoreCase)
                        || relative.StartsWith(packsPrefix, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(relative, outputFolderName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    fileCount++;
                    totalBytes += new FileInfo(file).Length;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(LogTag + " 统计产物失败: " + e.Message);
        }

        Debug.Log(string.Format(
            "{0} 构建完成：请求 {1} 个包，产物 {2} 个文件，合计 {3:F1} MB → {4}",
            LogTag,
            requestedBundleCount,
            fileCount,
            totalBytes / 1024f / 1024f,
            outputPath));
    }
}

/// <summary>
/// 构建后端切换（EditorPrefs 持久化，重启编辑器后保持）。
/// 用于分阶段迁移时做 A/B 对比：同一份资源、同一套清单，只换构建实现。
/// </summary>
public static class AssetBundleBuildBackendMenu
{
    private const string MenuRoot = "Tools/AssetBundle/构建后端/";

    [MenuItem(MenuRoot + "使用 SBP（Scriptable Build Pipeline）", true)]
    private static bool ValidateUseSbp()
    {
        return !AssetBundlePackager.UseScriptableBuildPipeline;
    }

    [MenuItem(MenuRoot + "使用 SBP（Scriptable Build Pipeline）")]
    private static void UseSbp()
    {
        AssetBundlePackager.UseScriptableBuildPipeline = true;
        Debug.Log("[AB Build] 构建后端已切换为 SBP（Scriptable Build Pipeline）");
    }

    [MenuItem(MenuRoot + "使用 built-in（BuildPipeline）", true)]
    private static bool ValidateUseBuiltin()
    {
        return AssetBundlePackager.UseScriptableBuildPipeline;
    }

    [MenuItem(MenuRoot + "使用 built-in（BuildPipeline）")]
    private static void UseBuiltin()
    {
        AssetBundlePackager.UseScriptableBuildPipeline = false;
        Debug.Log("[AB Build] 构建后端已切换为 built-in（BuildPipeline）");
    }
}