using System;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine.Build.Pipeline;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>
    /// 所有打包步骤的实现（静态方法）。
    /// <para>
    /// 顺序与"是否关键"不在这里，而在 <see cref="AbBuildStepCatalog"/>：
    /// 这里只负责"做什么"，目录表负责"按什么顺序、失败要不要停"。
    /// </para>
    /// <para>步骤之间只通过 <see cref="BuildContext"/> 交换数据，不互相直接调用。</para>
    /// </summary>
    public static class AbBuildSteps
    {
        // ---------------------------------------------------------------- 1) 校验与设置

        /// <summary>参数与平台校验：输出路径、目标平台是否支持同步 File IO 加载。</summary>
        public static BuildStepResult ValidateRequest(BuildContext ctx)
        {
            if (string.IsNullOrEmpty(ctx.Request.OutputRoot))
            {
                return BuildStepResult.Failed("输出根目录为空（检查构建配置 / BuildSettings）");
            }

            if (!AssetBundlePackager.IsSupportedRuntimePlatform(ctx.Request.BuildTarget))
            {
                return BuildStepResult.Failed(
                    "当前 AB 运行时只支持 64 位 Windows / macOS 的 File IO 加载，" + ctx.Request.BuildTarget
                    + " 需要先补 UnityWebRequestAssetBundle 分支");
            }

            if (string.IsNullOrEmpty(ctx.PlatformPath))
            {
                return BuildStepResult.Failed("平台输出目录未计算（检查 BuildAndManifestWithSteps）");
            }

            return BuildStepResult.Success(ctx.PlatformPath);
        }

        /// <summary>把本次请求参数写回 EditorPrefs（构建窗口下次打开时沿用）。</summary>
        public static BuildStepResult ApplyBuildSettings(BuildContext ctx)
        {
            AssetBundleBuildSettings.SetOutputRoot(ctx.Request.OutputRoot);
            AssetBundleBuildSettings.SetBuildTarget(ctx.Request.BuildTarget);
            AssetBundleBuildSettings.SetCompressionType(ctx.Request.Compression);
            return BuildStepResult.Success(ctx.Request.BuildTarget.ToString());
        }

        // ---------------------------------------------------------------- 2) 资源预处理

        /// <summary>打 AB 前先刷新图集（可选，失败只告警）。</summary>
        public static BuildStepResult PackAtlas(BuildContext ctx)
        {
            if (!ctx.Request.PackAtlasesBeforeBuild)
            {
                return BuildStepResult.Skipped("PackAtlasesBeforeBuild=false");
            }

            AtlasPackager.PackForAbBuild();
            return BuildStepResult.Success();
        }

        /// <summary>
        /// 打包 Lua 脚本 bundle。
        /// <para>
        /// 必须早于 AB 打包：<c>lua_bundle.bytes</c> 是 AB 的一个资产，漏打的话包里是旧脚本，
        /// 症状是「改了 Lua 没生效」，比打包失败难查，所以失败即中止。
        /// </para>
        /// </summary>
        public static BuildStepResult BuildLuaBundle(BuildContext ctx)
        {
            if (!CarrotFantasy.Editor.LuaBundleBuilder.Build(out string summary))
            {
                return BuildStepResult.Failed("Lua Bundle 打包失败：\n" + summary);
            }

            return BuildStepResult.Success(summary.Replace('\n', ' '));
        }

        // ---------------------------------------------------------------- 3) 打 AB

        /// <summary>清空平台输出目录（受 <c>Request.ClearOutputFolder</c> 控制）。</summary>
        public static BuildStepResult CleanOutput(BuildContext ctx)
        {
            if (!ctx.Request.ClearOutputFolder)
            {
                return BuildStepResult.Skipped("ClearOutputFolder=false");
            }

            if (!AssetBundlePackager.TryCleanOutputDirectory(ctx.PlatformPath, out string error))
            {
                return BuildStepResult.Failed(error);
            }

            return BuildStepResult.Success(ctx.PlatformPath);
        }

        /// <summary>
        /// 构建 AB（内部按 <see cref="AssetBundlePackager.UseScriptableBuildPipeline"/> 选 SBP / built-in）。
        /// <para>清目录由 <see cref="CleanOutput"/> 负责，拷 StreamingAssets 由后续步骤负责。</para>
        /// </summary>
        public static BuildStepResult BuildBundles(BuildContext ctx)
        {
            // ForceRebuild 只能附加，不能替换压缩选项。
            BuildAssetBundleOptions options = AssetBundlePackager.GetCompressionOption(ctx.Request.Compression);
            if (ctx.Request.ForceRebuild)
            {
                options |= BuildAssetBundleOptions.ForceRebuildAssetBundle;
            }

            bool built = AssetBundlePackager.BuildAssetBundles(
                ctx.PlatformPath,
                ctx.Request.BuildTarget,
                options,
                clearFolders: false,
                copyToStreamingAssets: false,
                backend: ctx.Request.Backend,
                sbpResults: out IBundleBuildResults sbpResults);

            if (!built)
            {
                return BuildStepResult.Failed("AB 构建失败（详见上方日志）");
            }

            if (sbpResults != null)
            {
                // 阶段 A：把 SBP 结果带进上下文，清单阶段用它做依赖双算对比。
                ctx.Set(sbpResults);
            }

            return BuildStepResult.Success(
                ctx.Request.Backend == AbBuildBackend.ScriptableBuildPipeline ? "SBP" : "built-in");
        }

        // ---------------------------------------------------------------- 4) 清单与分发包

        /// <summary>扫描落盘 AB，构建清单数据（Size/MD5/扁平依赖），不写文件、不合并 Pack、不签名。</summary>
        public static BuildStepResult GenerateManifestData(BuildContext ctx)
        {
            if (!Directory.Exists(ctx.PlatformPath))
            {
                return BuildStepResult.Failed("AB 输出目录不存在：" + ctx.PlatformPath);
            }

            // 阶段 A：SBP 结果可用时，把 BundleInfos 的直接依赖传给清单阶段做双算对比。
            IBundleBuildResults sbpResults = ctx.Get<IBundleBuildResults>();
            Func<string, string[]> dependencyResolver = null;
            if (sbpResults != null && sbpResults.BundleInfos != null)
            {
                dependencyResolver = bundle =>
                    sbpResults.BundleInfos.TryGetValue(bundle, out BundleDetails details)
                        ? details.Dependencies
                        : null;
            }

            AssetBundlePackager.ManifestBuildData data = AssetBundlePackager.BuildManifestData(
                ctx.PlatformPath,
                ctx.Request.BuildTarget,
                ctx.Request.ManifestVersion,
                (int)ctx.Request.Compression,
                dependencyResolver);

            if (data == null || data.Manifest == null)
            {
                return BuildStepResult.Failed("清单数据构建失败");
            }

            int missingCount = data.MissingOnDisk != null ? data.MissingOnDisk.Count : 0;
            if (missingCount > 0)
            {
                return BuildStepResult.Failed(
                    missingCount + " 个已登记资源没有打出 AB 文件，已中止。详见 Console。");
            }

            ctx.Set(data);
            ctx.Result.Manifest = data.Manifest;

            return BuildStepResult.Success(data.Manifest.AssetBundles.Count + " 个包");
        }

        /// <summary>把清单里的 AB 按规则合并成下载用 ZIP Pack，填充 DownloadPacks。</summary>
        public static BuildStepResult MergePacks(BuildContext ctx)
        {
            AssetBundlePackager.ManifestBuildData data = ctx.Get<AssetBundlePackager.ManifestBuildData>();
            if (data == null || data.Manifest == null)
            {
                return BuildStepResult.Failed("缺少清单数据（检查 GenerateManifestData 是否在本步骤之前）");
            }

            AssetBundlePackMerger.BuildPacks(data.Manifest, ctx.PlatformPath);

            int packCount = data.Manifest.DownloadPacks != null ? data.Manifest.DownloadPacks.Count : 0;
            return BuildStepResult.Success(packCount + " 个 Pack");
        }

        /// <summary>写 custom_manifest.json（不做签名，签名是独立步骤）。</summary>
        public static BuildStepResult WriteManifestJson(BuildContext ctx)
        {
            AssetBundlePackager.ManifestBuildData data = ctx.Get<AssetBundlePackager.ManifestBuildData>();
            if (data == null || data.Manifest == null)
            {
                return BuildStepResult.Failed("缺少清单数据");
            }

            return AssetBundlePackager.WriteManifestJson(data.Manifest, ctx.PlatformPath, out string error)
                ? BuildStepResult.Success("custom_manifest.json")
                : BuildStepResult.Failed(error);
        }

        /// <summary>
        /// 清单签名。开发宏 <c>CF_DEV_SKIP_MANIFEST_SIGN</c> 生效时只告警不签名，
        /// 此时 <see cref="ManifestSigner.TrySign"/> 仍返回 true（与改造前一致）。
        /// </summary>
        public static BuildStepResult SignManifest(BuildContext ctx)
        {
            return AssetBundlePackager.SignManifestFile(ctx.PlatformPath, out string error)
                ? BuildStepResult.Success()
                : BuildStepResult.Failed(error);
        }

        /// <summary>写 version.txt（内容为清单里的 BuildTime，与服务端 version.txt 对应）。</summary>
        public static BuildStepResult WriteVersion(BuildContext ctx)
        {
            AssetBundlePackager.ManifestBuildData data = ctx.Get<AssetBundlePackager.ManifestBuildData>();
            if (data == null || data.Manifest == null)
            {
                return BuildStepResult.Failed("缺少清单数据");
            }

            AssetBundlePackager.WriteVersionFile(data.Manifest, ctx.PlatformPath);
            return BuildStepResult.Success(data.Manifest.BuildTime);
        }

        // ---------------------------------------------------------------- 5) 交付

        /// <summary>把平台 AB 目录拷进 StreamingAssets（受 <c>Request.CopyToStreamingAssets</c> 控制）。</summary>
        public static BuildStepResult CopyToStreamingAssets(BuildContext ctx)
        {
            if (!ctx.Request.CopyToStreamingAssets)
            {
                return BuildStepResult.Skipped("CopyToStreamingAssets=false");
            }

            if (!AssetBundlePackager.CopyToStreamingAssets(ctx.PlatformPath, ctx.Request.BuildTarget))
            {
                return BuildStepResult.Failed("拷贝到 StreamingAssets 失败");
            }

            return BuildStepResult.Success();
        }

        /// <summary>
        /// 写入运行时 CDN 配置（StreamingAssets/ab_runtime_config.json）。
        /// 未指定 RuntimeEnv 时保留文件里已有的 env，避免打 AB 把正式环境盖成 dev。
        /// </summary>
        public static BuildStepResult WriteRuntimeConfig(BuildContext ctx)
        {
            if (!AssetBundleBuildSettings.WriteRuntimeConfig(ctx.Request.BuildTarget, ctx.Request.RuntimeEnv))
            {
                return BuildStepResult.Failed(
                    "写入 ab_runtime_config.json 失败。prod 需要 https:// CDN 模板，详见 Console。");
            }

            return BuildStepResult.Success("env=" + AssetBundleBuildSettings.ReadRuntimeConfigEnv());
        }

        // ---------------------------------------------------------------- 6) 收尾

        /// <summary>
        /// 通过 SFTP 把平台目录上传到云服务器（可选）。
        /// <para>
        /// 配置由 Profile 通过 <see cref="BuildContext"/> 注入（<see cref="AbUploadSettings"/>）；
        /// 未注入或未启用时跳过，不影响命令行/旧窗口的现有行为。
        /// 密码仍沿用本机 EditorPrefs（不进 Git）。
        /// </para>
        /// </summary>
        public static BuildStepResult UploadToServer(BuildContext ctx)
        {
            AbUploadSettings settings = ctx.Get<AbUploadSettings>();
            if (settings == null)
            {
                return BuildStepResult.Skipped("未配置上传（Profile 未提供 AbUploadSettings）");
            }

            if (!settings.Enabled)
            {
                return BuildStepResult.Skipped("EnableUpload=false");
            }

            // 把 Profile 里的连接信息写进本机 DeploySettings，再复用现有上传器。
            settings.ApplyToDeploySettings();

            if (!AssetBundleDeploySettings.TryValidate(out string validationError))
            {
                return BuildStepResult.Failed(validationError);
            }

            AssetBundleCloudUploadResult upload = AssetBundleCloudUploader.UploadDirectory(ctx.PlatformPath);
            if (upload.Cancelled)
            {
                return BuildStepResult.Skipped("上传已取消");
            }

            if (!upload.Success)
            {
                return BuildStepResult.Failed(upload.Message);
            }

            return BuildStepResult.Success(string.Format(
                "{0} 个文件 / {1:F1} MB → {2}:{3}{4}",
                upload.UploadedFileCount,
                upload.UploadedBytes / 1024f / 1024f,
                settings.Host,
                settings.Port,
                settings.RemotePath));
        }

        /// <summary>弹"成功"对话框（受 <c>Request.ShowManifestDialog</c> 控制，BatchMode 自动跳过）。</summary>
        public static BuildStepResult ShowManifestDialog(BuildContext ctx)
        {
            if (!ctx.Request.ShowManifestDialog)
            {
                return BuildStepResult.Skipped("ShowManifestDialog=false");
            }

            if (Application.isBatchMode)
            {
                return BuildStepResult.Skipped("BatchMode");
            }

            AssetBundlePackager.ManifestBuildData data = ctx.Get<AssetBundlePackager.ManifestBuildData>();
            int bundleCount = data != null && data.Manifest != null ? data.Manifest.AssetBundles.Count : 0;
            int missingCount = data != null && data.MissingOnDisk != null ? data.MissingOnDisk.Count : 0;

            EditorUtility.DisplayDialog(
                "成功",
                string.Format(
                    "清单已生成：{0} 个 AB 包（以输出目录实际文件为准）。\n" +
                    "有资源登记但未构建出文件：{1} 个（详见 Console，多为空文件夹包名或需重新打 AB）。",
                    bundleCount,
                    missingCount),
                "确定");

            return BuildStepResult.Success();
        }

        /// <summary>收尾：把上下文里的产物落到最终结果，供调用方（窗口 / 批处理 / Profile）读取。</summary>
        public static BuildStepResult FinalizeResult(BuildContext ctx)
        {
            if (ctx.Result.Manifest == null)
            {
                return BuildStepResult.Failed("清单为空，不能标记为成功");
            }

            ctx.Result.Success = true;
            ctx.Result.PlatformBundlePath = ctx.PlatformPath;
            return BuildStepResult.Success(
                ctx.Result.Manifest.AssetBundles.Count + " 个包 → " + ctx.PlatformPath);
        }
    }
}
