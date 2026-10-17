using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Editor 与运行时共用的 AB 构建/发布配置（路径、CDN 模板、版本号）。
/// WriteRuntimeConfig 写入 StreamingAssets/ab_runtime_config.json，供 AssetBundlePathHelper 读取 CDN 模板。
/// </summary>
public static class AssetBundleBuildSettings
{
    public const string DefaultOutputRoot = "Build/AssetBundles";
    public const string PrefsOutputRoot = "AB_OutputRoot";
    public const string PrefsBuildTarget = "BuildTarget";
    public const string PrefsCompression = "CompressionType";
    public const string PrefsCdnUrlTemplate = "AB_CdnUrlTemplate";

    public static string GetOutputRoot()
    {
        string saved = EditorPrefs.GetString(PrefsOutputRoot, string.Empty);
        return string.IsNullOrEmpty(saved) ? DefaultOutputRoot : saved;
    }

    public static void SetOutputRoot(string path)
    {
        EditorPrefs.SetString(PrefsOutputRoot, path ?? string.Empty);
    }

    public static BuildTarget GetBuildTarget()
    {
        int target = EditorPrefs.GetInt(PrefsBuildTarget, -1);
        if (target == -1)
        {
            return EditorUserBuildSettings.activeBuildTarget;
        }

        return (BuildTarget)target;
    }

    public static void SetBuildTarget(BuildTarget target)
    {
        EditorPrefs.SetInt(PrefsBuildTarget, (int)target);
    }

    public static CompressionType GetCompressionType()
    {
        int type = EditorPrefs.GetInt(PrefsCompression, -1);
        if (type == -1)
        {
            return CompressionType.ChunkBasedCompression;
        }

        return (CompressionType)type;
    }

    public static void SetCompressionType(CompressionType compression)
    {
        EditorPrefs.SetInt(PrefsCompression, (int)compression);
    }

    public static string GetFullOutputRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", GetOutputRoot()));
    }

    public static string GetFullOutputRoot(string outputRoot)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputRoot));
    }

    public static string GetPlatformBundlePath(BuildTarget target)
    {
        return GetPlatformBundlePath(GetOutputRoot(), target);
    }

    public static string GetPlatformBundlePath(string outputRoot, BuildTarget target)
    {
        return Path.Combine(
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputRoot)),
            AssetBundlePackager.GetPlatformFolder(target));
    }

    public static string GetManifestPath(BuildTarget target)
    {
        return Path.Combine(GetPlatformBundlePath(target), "custom_manifest.json");
    }

    public static string GetManifestPath(string outputRoot, BuildTarget target)
    {
        return Path.Combine(GetPlatformBundlePath(outputRoot, target), "custom_manifest.json");
    }

    public static int ReadLastManifestVersion(BuildTarget target)
    {
        return ReadLastManifestVersion(GetOutputRoot(), target);
    }

    public static int ReadLastManifestVersion(string outputRoot, BuildTarget target)
    {
        string manifestPath = Path.Combine(
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputRoot)),
            AssetBundlePackager.GetPlatformFolder(target),
            "custom_manifest.json");
        if (!File.Exists(manifestPath))
        {
            return 0;
        }

        try
        {
            string text = File.ReadAllText(manifestPath);
            CustomManifest old = JsonUtility.FromJson<CustomManifest>(text);
            return old != null ? old.ManifestVersion : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static int SuggestNextManifestVersion(BuildTarget target)
    {
        return ReadLastManifestVersion(target) + 1;
    }

    public static int SuggestNextManifestVersion(string outputRoot, BuildTarget target)
    {
        return ReadLastManifestVersion(outputRoot, target) + 1;
    }

    public static string GetCdnUrlTemplate()
    {
        string saved = EditorPrefs.GetString(PrefsCdnUrlTemplate, string.Empty);
        if (!string.IsNullOrEmpty(saved))
        {
            return saved;
        }

        return BuildDefaultCdnUrlTemplate(GetFullOutputRoot());
    }

    /// <summary>按环境取模板：优先 AB_CdnUrlTemplate_{env}，没有配置时回退到全局模板。</summary>
    public static string GetCdnUrlTemplate(string env)
    {
        if (!string.IsNullOrEmpty(env))
        {
            string perEnv = EditorPrefs.GetString(PrefsCdnUrlTemplate + "_" + env.ToLowerInvariant(), string.Empty);
            if (!string.IsNullOrEmpty(perEnv))
            {
                return perEnv;
            }
        }

        return GetCdnUrlTemplate();
    }

    public static void SetCdnUrlTemplate(string env, string template)
    {
        if (string.IsNullOrEmpty(env))
        {
            SetCdnUrlTemplate(template);
            return;
        }

        EditorPrefs.SetString(PrefsCdnUrlTemplate + "_" + env.ToLowerInvariant(), template ?? string.Empty);
    }

    public static void SetCdnUrlTemplate(string template)
    {
        EditorPrefs.SetString(PrefsCdnUrlTemplate, template ?? string.Empty);
    }

    public static string BuildDefaultCdnUrlTemplate(string fullOutputRoot)
    {
        string normalized = Path.GetFullPath(fullOutputRoot).Replace('\\', '/');
        if (!normalized.StartsWith("/"))
        {
            normalized = "/" + normalized;
        }

        return "file://" + normalized + "/{0}";
    }

    /// <summary>写入 StreamingAssets，供运行时 AssetBundlePathHelper 读取。</summary>
    public static void WriteRuntimeConfig(BuildTarget target)
    {
        WriteRuntimeConfig(target, CarrotFantasy.BuildChannelDefines.EnvDev);
    }

    /// <summary>写入 StreamingAssets；env 为 dev/staging/prod。</summary>
    public static void WriteRuntimeConfig(BuildTarget target, string env)
    {
        if (string.IsNullOrEmpty(env))
        {
            env = CarrotFantasy.BuildChannelDefines.EnvDev;
        }

        string template = GetCdnUrlTemplate(env);
        if (string.IsNullOrEmpty(template))
        {
            template = BuildDefaultCdnUrlTemplate(GetFullOutputRoot());
        }

        bool isProd = string.Equals(env, CarrotFantasy.BuildChannelDefines.EnvProd, StringComparison.OrdinalIgnoreCase);
        bool isHttps = template.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        bool isInsecureHttp = template.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

        // 正式包必须走 https：下载的清单与热更 DLL 会被执行，明文链路等于把代码执行权交出去。
        if (isProd && !isHttps)
        {
            Debug.LogError(
                "[AB Build] prod 环境必须使用 https:// 的 CDN 模板，已拒绝写入运行配置。当前: " + template);
            return;
        }

        if (isInsecureHttp)
        {
            Debug.LogWarning("[AB Build] 正在写入明文 HTTP 的 CDN 模板，仅限调试: " + template);
        }

        var config = new AssetBundleRuntimeConfig
        {
            env = env,
            serverDownloadUrlTemplate = template,
            localSavePath = AssetBundlePathHelper.localSavePath,
            allowInsecureHttp = isInsecureHttp,
        };

        string json = JsonUtility.ToJson(config, true);
        string destPath = Path.Combine(Application.streamingAssetsPath, AssetBundleRuntimeConfig.FileName);
        string destDir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        File.WriteAllText(destPath, json);
        AssetDatabase.Refresh();
        Debug.Log("[AB Build] 已写入运行时配置 env=" + env + " -> " + destPath);
    }
}
