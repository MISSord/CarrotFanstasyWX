using System;
using System.IO;
using System.Linq;
using CarrotFantasy;
using CarrotFantasy.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// PC Player 一键出包：通道准备 →（可选）HybridCLR Generate/同步 → BuildPlayer。
/// 输出到 CarrotFantasy/Build/PC/{env}/{时间戳}/。
/// </summary>
public static class PcPlayerBuildPipeline
{
    public const string DefaultRelativeOutputRoot = "Build/PC";

    public enum Channel
    {
        Dev,
        Prod,
    }

    public struct BuildRequest
    {
        public Channel Channel;
        /// <summary>false 时不弹 Dialog（Batch CLI）。</summary>
        public bool Interactive;
        /// <summary>默认 true：Generate/All + 同步 DLL 到 StreamingAssets/AB。</summary>
        public bool RunHybridClrGenerateAndSync;
        /// <summary>相对工程根，默认 Build/PC。</summary>
        public string RelativeOutputRoot;
    }

    public struct BuildResult
    {
        public bool Success;
        /// <summary>宏刚切换，需等编译后再跑一次。</summary>
        public bool NeedsRecompileRetry;
        public string Message;
        public string OutputPath;
        public string ChannelEnv;
    }

    [MenuItem("Tools/Build Channel/一键打开发 PC 包", priority = 20)]
    public static void BuildDevFromMenu()
    {
        Build(new BuildRequest
        {
            Channel = Channel.Dev,
            Interactive = true,
            RunHybridClrGenerateAndSync = true,
            RelativeOutputRoot = DefaultRelativeOutputRoot,
        });
    }

    [MenuItem("Tools/Build Channel/一键打正式 PC 包", priority = 21)]
    public static void BuildProdFromMenu()
    {
        Build(new BuildRequest
        {
            Channel = Channel.Prod,
            Interactive = true,
            RunHybridClrGenerateAndSync = true,
            RelativeOutputRoot = DefaultRelativeOutputRoot,
        });
    }

    public static BuildResult Build(BuildRequest request)
    {
        var result = new BuildResult();
        bool interactive = request.Interactive && !Application.isBatchMode;
        string env = request.Channel == Channel.Dev
            ? BuildChannelDefines.EnvDev
            : BuildChannelDefines.EnvProd;
        result.ChannelEnv = env;

        bool enableDevTools = request.Channel == Channel.Dev;
        bool developmentBuild = request.Channel == Channel.Dev;

        try
        {
            BuildTarget target = BuildTarget.StandaloneWindows64;
            if (EditorUserBuildSettings.activeBuildTarget != target
                && EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows)
            {
                result.Message = string.Format(
                    "当前激活平台为 {0}，PC 出包需要 StandaloneWindows64。请先在 Build Settings 切换。",
                    EditorUserBuildSettings.activeBuildTarget);
                Fail(result, interactive);
                return result;
            }

            bool defineChanged = PcBuildChannel.ApplyChannelCore(enableDevTools, env);
            if (defineChanged)
            {
                result.NeedsRecompileRetry = true;
                result.Message =
                    "已切换 CF_DEV_TOOLS / 写入 ab_runtime_config.env=" + env + "。\n"
                    + "脚本正在重新编译，请等待编译完成后再次执行本命令（菜单或脚本）。";
                Debug.LogWarning("[PcPlayerBuild] " + result.Message.Replace('\n', ' '));
                if (interactive)
                {
                    EditorUtility.DisplayDialog("需等待编译", result.Message, "确定");
                }

                return result;
            }

            if (!TryValidateChannel(request.Channel, out string validateError))
            {
                result.Message = validateError;
                Fail(result, interactive);
                return result;
            }

            if (request.RunHybridClrGenerateAndSync)
            {
                if (interactive)
                {
                    EditorUtility.DisplayProgressBar("PC 出包", "HybridCLR Generate/All…", 0.15f);
                }

                if (!EditorApplication.ExecuteMenuItem("HybridCLR/Generate/All"))
                {
                    Debug.LogWarning("[PcPlayerBuild] ExecuteMenuItem HybridCLR/Generate/All 返回 false，继续同步 DLL。");
                }

                if (interactive)
                {
                    EditorUtility.DisplayProgressBar("PC 出包", "同步 HybridCLR DLL…", 0.4f);
                }

                int copied = HybridCLRProjectSetup.SyncDllsForHotUpdate(target);
                if (copied <= 0)
                {
                    result.Message =
                        "未同步到 HybridCLR DLL。请确认 IL2CPP 工具链与 Generate/All 已成功。";
                    Fail(result, interactive);
                    return result;
                }
            }

            string[] scenes = GetEnabledScenePaths();
            if (scenes.Length == 0)
            {
                result.Message = "EditorBuildSettings 中没有启用的场景。";
                Fail(result, interactive);
                return result;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string relativeRoot = string.IsNullOrEmpty(request.RelativeOutputRoot)
                ? DefaultRelativeOutputRoot
                : request.RelativeOutputRoot.Replace('\\', '/').Trim('/');
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string outDir = Path.Combine(projectRoot, relativeRoot, env, stamp);
            Directory.CreateDirectory(outDir);

            string exeName = ResolveExeFileName();
            string locationPath = Path.Combine(outDir, exeName);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = locationPath,
                target = target,
                options = developmentBuild
                    ? BuildOptions.Development
                    : BuildOptions.None,
            };

            if (interactive)
            {
                EditorUtility.DisplayProgressBar("PC 出包", "BuildPipeline.BuildPlayer…", 0.7f);
            }

            Debug.Log(string.Format(
                "[PcPlayerBuild] BuildPlayer channel={0} env={1} development={2} scenes={3} out={4}",
                request.Channel,
                env,
                developmentBuild,
                scenes.Length,
                locationPath));

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report == null
                || report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                result.Message = report == null
                    ? "BuildPlayer 返回空报告。"
                    : "BuildPlayer 失败: " + report.summary.result;
                Fail(result, interactive);
                return result;
            }

            result.Success = true;
            result.OutputPath = locationPath;
            result.Message = string.Format(
                "PC 包完成\n通道: {0}\nenv: {1}\nDevelopment: {2}\n输出: {3}",
                request.Channel,
                env,
                developmentBuild,
                locationPath);

            Debug.Log("[PcPlayerBuild] " + result.Message.Replace('\n', ' '));
            if (interactive)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("PC 出包完成", result.Message, "确定");
            }

            return result;
        }
        catch (Exception e)
        {
            result.Message = e.Message;
            Debug.LogException(e);
            Fail(result, interactive);
            return result;
        }
        finally
        {
            if (interactive)
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }

    public static bool TryValidateChannel(Channel channel, out string error)
    {
        error = null;
        bool wantDevTools = channel == Channel.Dev;
        bool hasDevTools = PcBuildChannel.HasDevToolsDefine();
        if (hasDevTools != wantDevTools)
        {
            error = string.Format(
                "CF_DEV_TOOLS 状态不符：期望 {0}，当前 {1}。",
                wantDevTools ? "启用" : "禁用",
                hasDevTools ? "启用" : "禁用");
            return false;
        }

        string wantEnv = channel == Channel.Dev
            ? BuildChannelDefines.EnvDev
            : BuildChannelDefines.EnvProd;
        string actualEnv = ReadRuntimeConfigEnv();
        if (!string.Equals(actualEnv, wantEnv, StringComparison.OrdinalIgnoreCase))
        {
            error = string.Format(
                "ab_runtime_config.env 不符：期望 {0}，当前 {1}。",
                wantEnv,
                string.IsNullOrEmpty(actualEnv) ? "(缺失)" : actualEnv);
            return false;
        }

        return true;
    }

    public static Channel? ParseChannel(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (string.Equals(name, "dev", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "development", StringComparison.OrdinalIgnoreCase))
        {
            return Channel.Dev;
        }

        if (string.Equals(name, "prod", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "release", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "production", StringComparison.OrdinalIgnoreCase))
        {
            return Channel.Prod;
        }

        return null;
    }

    static string[] GetEnabledScenePaths()
    {
        return EditorBuildSettings.scenes
            .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
            .Select(s => s.path)
            .ToArray();
    }

    static string ResolveExeFileName()
    {
        string product = PlayerSettings.productName;
        if (string.IsNullOrWhiteSpace(product)
            || string.Equals(product, "Unity", StringComparison.OrdinalIgnoreCase))
        {
            product = "CarrotFantasy";
        }

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            product = product.Replace(c, '_');
        }

        return product + ".exe";
    }

    static string ReadRuntimeConfigEnv()
    {
        string path = Path.Combine(Application.streamingAssetsPath, AssetBundleRuntimeConfig.FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var config = JsonUtility.FromJson<AssetBundleRuntimeConfig>(File.ReadAllText(path));
            return config != null ? config.env : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PcPlayerBuild] 读取 ab_runtime_config.json 失败: " + e.Message);
            return null;
        }
    }

    static void Fail(BuildResult result, bool interactive)
    {
        Debug.LogError("[PcPlayerBuild] " + result.Message);
        if (interactive)
        {
            EditorUtility.DisplayDialog("PC 出包失败", result.Message, "确定");
        }
    }
}
