using System;
using UnityEditor;
using UnityEngine;

namespace CarrotFantasy.Editor.Batch
{
    /// <summary>
    /// Batch / -executeMethod 入口：代码热更、完整 AB、PC Player 出包。
    /// 用法见 docs/BuildAndHotUpdateSOP.md「命令行（Batch）」。
    /// </summary>
    public static class BuildCli
    {
        const string ArgPrefix = "-cf";

        /// <summary>Unity: -executeMethod CarrotFantasy.Editor.Batch.BuildCli.Run</summary>
        public static void Run()
        {
            bool batchMode = Application.isBatchMode;
            bool success = false;
            string message = string.Empty;
            int exitCode = 1;

            try
            {
                CliOptions options = ParseArgs(Environment.GetCommandLineArgs(), batchMode);
                if (string.IsNullOrEmpty(options.Command))
                {
                    message =
                        "缺少 -cfCommand=codeHotUpdate|abBuild|pcBuild。"
                        + " 示例: -cfCommand=pcBuild -cfChannel=dev";
                    Debug.LogError("[BuildCli] " + message);
                }
                else if (!TryResolveTarget(options.TargetName, batchMode, out BuildTarget target, out string targetError))
                {
                    message = targetError;
                    Debug.LogError("[BuildCli] " + message);
                }
                else if (EditorUserBuildSettings.activeBuildTarget != target
                         && !IsWindowsStandalonePair(EditorUserBuildSettings.activeBuildTarget, target))
                {
                    message = string.Format(
                        "激活平台 {0} 与 -cfTarget={1} 不一致。请先切换 Build Settings，或保证 batch 工程已切到目标平台。",
                        EditorUserBuildSettings.activeBuildTarget,
                        target);
                    Debug.LogError("[BuildCli] " + message);
                }
                else
                {
                    Debug.Log(string.Format(
                        "[BuildCli] command={0} target={1} upload={2} env={3} channel={4} forceRebuild={5} copyStreaming={6} skipGenerate={7} batch={8}",
                        options.Command,
                        target,
                        options.Upload,
                        options.Env ?? "(default)",
                        options.Channel ?? "(n/a)",
                        options.ForceRebuild,
                        options.CopyStreaming,
                        options.SkipGenerate,
                        batchMode));

                    success = Execute(options, target, out message, out exitCode);
                    if (!success)
                    {
                        Debug.LogError("[BuildCli] 失败: " + message);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(options.Env)
                            && !string.Equals(options.Command, "pcBuild", StringComparison.OrdinalIgnoreCase))
                        {
                            AssetBundleBuildSettings.WriteRuntimeConfig(target, options.Env);
                        }

                        Debug.Log("[BuildCli] 成功: " + message);
                        exitCode = 0;
                    }
                }
            }
            catch (Exception e)
            {
                message = e.Message;
                Debug.LogException(e);
                success = false;
                exitCode = 1;
            }

            if (batchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        static bool IsWindowsStandalonePair(BuildTarget active, BuildTarget requested)
        {
            bool activeWin = active == BuildTarget.StandaloneWindows
                             || active == BuildTarget.StandaloneWindows64;
            bool reqWin = requested == BuildTarget.StandaloneWindows
                          || requested == BuildTarget.StandaloneWindows64;
            return activeWin && reqWin;
        }

        static bool Execute(CliOptions options, BuildTarget target, out string message, out int exitCode)
        {
            exitCode = 1;
            string command = options.Command.Trim();
            if (string.Equals(command, "codeHotUpdate", StringComparison.OrdinalIgnoreCase))
            {
                HybridCLRCodeHotUpdatePipeline.Result result = HybridCLRCodeHotUpdatePipeline.Run(
                    target,
                    promptUpload: options.Upload,
                    showManifestDialog: false,
                    interactive: false);
                message = result.Message;
                if (result.Success)
                {
                    exitCode = 0;
                }

                return result.Success;
            }

            if (string.Equals(command, "abBuild", StringComparison.OrdinalIgnoreCase))
            {
                var request = AssetBundleBuildPipeline.CreateDefaultRequest();
                request.BuildTarget = target;
                request.ShowManifestDialog = false;
                request.ForceRebuild = options.ForceRebuild;
                request.CopyToStreamingAssets = options.CopyStreaming;
                request.ManifestVersion = AssetBundleBuildSettings.SuggestNextManifestVersion(target);

                AssetBundleBuildPipeline.BuildResult buildResult =
                    AssetBundleBuildPipeline.BuildAndManifest(request);
                if (!buildResult.Success)
                {
                    message = "AB 打包失败，请查看日志。";
                    return false;
                }

                message = string.Format(
                    "AB 完成 platform={0} version={1} path={2}",
                    target,
                    buildResult.Manifest != null ? buildResult.Manifest.ManifestVersion : 0,
                    buildResult.PlatformBundlePath);

                if (options.Upload)
                {
                    if (!AssetBundleDeploySettings.TryValidate(out string validationError))
                    {
                        message += "\n上传失败: " + validationError;
                        return false;
                    }

                    AssetBundleCloudUploadResult uploadResult =
                        AssetBundleCloudUploader.UploadDirectory(buildResult.PlatformBundlePath);
                    if (!uploadResult.Success)
                    {
                        message += "\n上传失败: " + uploadResult.Message;
                        return false;
                    }

                    message += "\n上传: " + uploadResult.Message;
                }

                exitCode = 0;
                return true;
            }

            if (string.Equals(command, "pcBuild", StringComparison.OrdinalIgnoreCase))
            {
                PcPlayerBuildPipeline.Channel? channel = PcPlayerBuildPipeline.ParseChannel(options.Channel);
                if (channel == null)
                {
                    message = "pcBuild 需要 -cfChannel=dev|prod";
                    return false;
                }

                PcPlayerBuildPipeline.BuildResult pcResult = PcPlayerBuildPipeline.Build(
                    new PcPlayerBuildPipeline.BuildRequest
                    {
                        Channel = channel.Value,
                        Interactive = false,
                        RunHybridClrGenerateAndSync = !options.SkipGenerate,
                        RelativeOutputRoot = PcPlayerBuildPipeline.DefaultRelativeOutputRoot,
                    });

                message = pcResult.Message;
                if (pcResult.NeedsRecompileRetry)
                {
                    // 2 = 请重跑（宏刚切换）
                    exitCode = 2;
                    return false;
                }

                if (pcResult.Success)
                {
                    exitCode = 0;
                }

                return pcResult.Success;
            }

            message = "未知 -cfCommand: " + command + "（支持 codeHotUpdate / abBuild / pcBuild）";
            return false;
        }

        static CliOptions ParseArgs(string[] args, bool batchMode)
        {
            var options = new CliOptions
            {
                // batch 下未指定时默认 Win64；非 batch 用当前激活平台（由 TargetName 空表示）。
                TargetName = batchMode ? "StandaloneWindows64" : null,
            };

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.IsNullOrEmpty(arg) || !arg.StartsWith(ArgPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string key;
                string value;
                int eq = arg.IndexOf('=');
                if (eq > 0)
                {
                    key = arg.Substring(0, eq);
                    value = arg.Substring(eq + 1);
                }
                else
                {
                    key = arg;
                    value = i + 1 < args.Length ? args[i + 1] : string.Empty;
                }

                if (string.Equals(key, "-cfCommand", StringComparison.OrdinalIgnoreCase))
                {
                    options.Command = value;
                }
                else if (string.Equals(key, "-cfTarget", StringComparison.OrdinalIgnoreCase))
                {
                    options.TargetName = value;
                }
                else if (string.Equals(key, "-cfUpload", StringComparison.OrdinalIgnoreCase))
                {
                    options.Upload = ParseBool(value, defaultValue: false);
                }
                else if (string.Equals(key, "-cfEnv", StringComparison.OrdinalIgnoreCase))
                {
                    options.Env = value;
                }
                else if (string.Equals(key, "-cfChannel", StringComparison.OrdinalIgnoreCase))
                {
                    options.Channel = value;
                }
                else if (string.Equals(key, "-cfForceRebuild", StringComparison.OrdinalIgnoreCase))
                {
                    options.ForceRebuild = ParseBool(value, defaultValue: false);
                }
                else if (string.Equals(key, "-cfCopyStreaming", StringComparison.OrdinalIgnoreCase))
                {
                    options.CopyStreaming = ParseBool(value, defaultValue: false);
                }
                else if (string.Equals(key, "-cfSkipGenerate", StringComparison.OrdinalIgnoreCase))
                {
                    options.SkipGenerate = ParseBool(value, defaultValue: false);
                }
            }

            return options;
        }

        static bool TryResolveTarget(
            string targetName,
            bool batchMode,
            out BuildTarget target,
            out string error)
        {
            target = BuildTarget.NoTarget;
            error = null;

            if (string.IsNullOrEmpty(targetName))
            {
                if (batchMode)
                {
                    target = BuildTarget.StandaloneWindows64;
                    return true;
                }

                target = EditorUserBuildSettings.activeBuildTarget;
                return true;
            }

            if (Enum.TryParse(targetName, ignoreCase: true, out target)
                && target != BuildTarget.NoTarget)
            {
                return true;
            }

            error = "无法解析 -cfTarget=" + targetName + "（需为 UnityEngine.BuildTarget 枚举名，如 StandaloneWindows64）";
            return false;
        }

        static bool ParseBool(string value, bool defaultValue)
        {
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            if (bool.TryParse(value, out bool b))
            {
                return b;
            }

            if (value == "1" || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (value == "0" || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return defaultValue;
        }

        sealed class CliOptions
        {
            public string Command;
            public string TargetName;
            public bool Upload;
            public string Env;
            public string Channel;
            public bool ForceRebuild;
            public bool CopyStreaming;
            public bool SkipGenerate;
        }
    }
}
