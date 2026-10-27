using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
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
///   <item>配合 <c>BuildCache</c> 可做增量构建（第二阶段再接）。</item>
/// </list>
///
/// <para><b>阶段 1（当前）</b>：从 AssetDatabase 收集包名，等价替换 built-in，
/// 产物、清单、PackMerger 全部不变；用菜单
/// <c>Tools/AssetBundle/构建后端</c> 可随时切回 built-in 对比。</para>
///
/// <para><b>阶段 2（下一步）</b>：把 <see cref="CollectFromAssetDatabase"/> 换成
/// “代码定义布局” —— 用 <c>AssetDatabase.GetDependencies</c> 做依赖闭包，
/// 把视图专属资源与 prefab 放进同一个包（视图包），依赖自然归零，
/// 且不需要改 prefab、不需要重新导入。</para>
///
/// <para><b>阶段 3</b>：自定义 <see cref="IBuildTask"/> 做依赖审计与体积报告。</para>
///
/// <para>
/// 注意：SBP 只是“打包布局可编程”，它**不能**在不改动资源内容的前提下
/// 让 prefab 的序列化引用消失。真想断引用仍然要改资产（mutation / PrepareAssets）。
/// </para>
/// </summary>
public static class AssetBundleSbpPipeline
{
    private const string LogTag = "[AB Build][SBP]";

    /// <summary>
    /// 用 SBP 构建 AB 到 <paramref name="outputPath"/>。
    /// 包内容来自 AssetDatabase 已登记的 assetBundleName（阶段 1）。
    /// </summary>
    public static bool Build(
        string outputPath,
        BuildTarget buildTarget,
        BuildAssetBundleOptions options,
        out string error)
    {
        error = null;

        List<AssetBundleBuild> builds = CollectFromAssetDatabase();
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

            ReturnCode code = ContentPipeline.BuildAssetBundles(parameters, content, out _);
            if (code != ReturnCode.Success)
            {
                error = "ContentPipeline.BuildAssetBundles 返回 " + code;
                return false;
            }

            LogBuildSummary(outputPath, builds.Count);
            return true;
        }
        catch (Exception e)
        {
            // SBP 的异常信息通常带完整调用栈，直接透出便于定位。
            error = e.ToString();
            return false;
        }
    }

    /// <summary>
    /// 阶段 1：把 AssetDatabase 里已登记的包名翻译成 SBP 的输入。
    ///
    /// 注意：这里不修改任何 .meta；包名仍由 PrefabABPathPostprocessor 维护。
    /// 阶段 2 会换成“代码定义布局”，届时此方法只作为兼容兜底保留。
    /// </summary>
    private static List<AssetBundleBuild> CollectFromAssetDatabase()
    {
        var builds = new List<AssetBundleBuild>();
        string[] names = AssetDatabase.GetAllAssetBundleNames();

        for (int i = 0; i < names.Length; i++)
        {
            string bundleName = names[i];
            string[] assets = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);

            // 没有资源的包名（目录残留 / 历史脏数据）不传给 SBP，
            // 否则会产出 0 字节包或直接报错。
            if (assets == null || assets.Length == 0)
            {
                continue;
            }

            builds.Add(new AssetBundleBuild
            {
                assetBundleName = bundleName,
                assetNames = assets,
            });
        }

        return builds;
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
                foreach (string file in Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase)
                        || file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                        || file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                        || file.EndsWith(".sig", StringComparison.OrdinalIgnoreCase))
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
