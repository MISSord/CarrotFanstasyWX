using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarrotFantasy.Editor.BuildPipeline.Profiles
{
    /// <summary>
    /// 一份"打包配置"资产：把平台、路径、压缩、开关、上传、流程选项全部配置化。
    /// <para>
    /// 用法：右键 <c>Create → CarrotFantasy → Build → AB 打包配置</c>，
    /// 每个平台建一份（Windows / macOS / Android…），需要打包时选中对应资产点"开始打包"。
    /// </para>
    /// <para>
    /// 敏感信息约定：SFTP 的**密码**只保存在本机 EditorPrefs（<c>AB_DeployPassword</c>），
    /// 不写进本资产，避免随 Git 提交；其余连接信息（主机/端口/用户/远程目录/私钥路径）可入库。
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "CarrotFantasy/Build/AB 打包配置", fileName = "AbBuildProfile")]
    public sealed class AbBuildProfile : ScriptableObject
    {
        [Tooltip("目标平台；目前运行时只支持 PC / macOS")]
        public BuildTarget BuildTarget = BuildTarget.StandaloneWindows64;

        [Tooltip("相对工程根目录；运行时 GetServerLoadUrl 会拼成 {OutputRoot}/{平台目录}")]
        public string OutputRoot = "Build/AssetBundles";

        [Tooltip("标准压缩=LZMA（最小，运行时需转换）；分块压缩=LZ4（推荐）；不压缩=体积最大")]
        public CompressionType Compression = CompressionType.ChunkBasedCompression;

        [Tooltip("勾选后每次打包自动取「输出目录里当前清单版本 + 1」")]
        public bool AutoManifestVersion = true;

        [Tooltip("自动模式下这里显示下一版本号；取消自动后可手工指定")]
        public int ManifestVersion = 0;

        public bool ClearOutputFolder = true;
        public bool CopyToStreamingAssets = false;
        public bool ForceRebuild = false;
        public bool PackAtlasesBeforeBuild = true;
        public bool ShowManifestDialog = true;

        [Tooltip("勾选后打包结束自动上传平台目录；未配置连接信息时上传步骤自动跳过")]
        public bool EnableUpload = false;
        public string UploadHost = string.Empty;
        public int UploadPort = AssetBundleDeploySettings.DefaultPort;
        public string UploadUser = string.Empty;
        [Tooltip("服务器上的目标目录，例如 /var/www/ab")]
        public string UploadRemotePath = string.Empty;
        public bool UploadUsePrivateKey = false;
        [Tooltip("私钥文件绝对路径（仅勾选私钥登录时使用）")]
        public string UploadPrivateKeyPath = string.Empty;

        [Tooltip("true = 走步骤执行器；false = 走旧的整体流程（A/B 对比用）")]
        public bool UseStepPipeline = true;

        [Tooltip("只打印将执行的步骤，不真正打包")]
        public bool DryRun = false;

        [Tooltip("按名字跳过步骤（调试用），例如 CopyToStreamingAssets")]
        public string[] SkipSteps = new string[0];

        private void OnEnable()
        {
            this.RefreshManifestVersion();
        }

        private void OnValidate()
        {
            this.RefreshManifestVersion();
        }

        /// <summary>
        /// 自动模式下刷新 <see cref="ManifestVersion"/>：
        /// 新建资产（默认 0）或改了平台/输出目录后，Inspector 里能直接看到下一版本号。
        /// </summary>
        public void RefreshManifestVersion()
        {
            if (!this.AutoManifestVersion)
            {
                return;
            }

            int next = AssetBundleBuildSettings.SuggestNextManifestVersion(this.OutputRoot, this.BuildTarget);
            if (next != this.ManifestVersion)
            {
                this.ManifestVersion = next;
            }
        }

        /// <summary>
        /// 一键刷新所有「读本地数据/配置」得到的结果（Inspector 的「刷新」按钮）。
        /// <para>
        /// 目前包含：清单版本（读输出目录里现有清单）。
        /// 以后新增的本地数据/配置读取都挂到这里，按钮不用改。
        /// </para>
        /// </summary>
        public void RefreshLocalData()
        {
            this.RefreshManifestVersion();
        }

        public AssetBundleBuildPipeline.BuildRequest CreateRequest()
        {
            // 打包前也走一次统一刷新，保证用的是最新的本地数据（清单版本等）。
            this.RefreshLocalData();

            int version = this.ManifestVersion > 0
                ? this.ManifestVersion
                : AssetBundleBuildSettings.SuggestNextManifestVersion(this.OutputRoot, this.BuildTarget);

            return new AssetBundleBuildPipeline.BuildRequest
            {
                OutputRoot = this.OutputRoot,
                BuildTarget = this.BuildTarget,
                Compression = this.Compression,
                ManifestVersion = version,
                ClearOutputFolder = this.ClearOutputFolder,
                CopyToStreamingAssets = this.CopyToStreamingAssets,
                ForceRebuild = this.ForceRebuild,
                ShowManifestDialog = this.ShowManifestDialog,
                PackAtlasesBeforeBuild = this.PackAtlasesBeforeBuild,
            };
        }

        public BuildExecutionOptions CreateOptions()
        {
            return new BuildExecutionOptions
            {
                DryRun = this.DryRun,
                Skip = this.SkipSteps != null && this.SkipSteps.Length > 0 ? this.SkipSteps : null,
            };
        }

        public AbUploadSettings CreateUploadSettings()
        {
            return new AbUploadSettings
            {
                Enabled = this.EnableUpload,
                Host = this.UploadHost,
                Port = this.UploadPort,
                User = this.UploadUser,
                RemotePath = this.UploadRemotePath,
                UsePrivateKey = this.UploadUsePrivateKey,
                PrivateKeyPath = this.UploadPrivateKeyPath,
            };
        }

        /// <summary>
        /// 单独测试上传：把当前平台目录传到服务器，不需要完整重打包。
        /// <para>密码来自本机 EditorPrefs（AB_DeployPassword），不进资产。</para>
        /// </summary>
        public void TestUpload()
        {
            if (!this.EnableUpload)
            {
                Debug.LogWarning("[AB Build] 未勾选「启用云上传」，测试上传已跳过");
                return;
            }

            AbUploadSettings settings = CreateUploadSettings();
            settings.ApplyToDeploySettings();

            if (!AssetBundleDeploySettings.TryValidate(out string validationError))
            {
                Debug.LogError("[AB Build] 上传配置不完整：" + validationError);
                return;
            }

            string platformPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                this.OutputRoot,
                AssetBundlePackager.GetPlatformFolder(this.BuildTarget)));

            if (!Directory.Exists(platformPath))
            {
                Debug.LogError("[AB Build] 平台目录不存在，请先打一次包：" + platformPath);
                return;
            }

            AssetBundleCloudUploadResult result = AssetBundleCloudUploader.UploadDirectory(platformPath);
            if (result.Success)
            {
                Debug.Log(string.Format(
                    "[AB Build] 测试上传成功：{0} 个文件 / {1:F1} MB → {2}:{3}{4}",
                    result.UploadedFileCount,
                    result.UploadedBytes / 1024f / 1024f,
                    settings.Host,
                    settings.Port,
                    settings.RemotePath));
            }
            else
            {
                Debug.LogError("[AB Build] 测试上传失败：" + result.Message);
            }
        }

        /// <summary>按本配置执行一次完整打包（含可选的上传步骤）。</summary>
        public AssetBundleBuildPipeline.BuildResult Build()
        {
            AssetBundleBuildPipeline.BuildRequest request = CreateRequest();

            if (!this.UseStepPipeline)
            {
                if (this.EnableUpload)
                {
                    Debug.LogWarning("[AB Build] 已勾选云上传，但当前 UseStepPipeline=false（旧流程），上传步骤不会执行");
                }

                Debug.Log("[AB Build] Profile=" + this.name + " 使用旧整体流程执行");
                return AssetBundleBuildPipeline.RunLegacyFlow(request);
            }

            Debug.Log("[AB Build] Profile=" + this.name
                      + " 使用步骤执行器（" + AbBuildStepCatalog.ForTarget(this.BuildTarget).Count + " 步）");

            return AssetBundleBuildPipeline.BuildAndManifestWithSteps(
                request,
                CreateOptions(),
                ctx => ctx.Set(this.CreateUploadSettings()));
        }

        /// <summary>只打印将执行的步骤（不落盘、不改任何配置）。</summary>
        public void DryRunOnce()
        {
            if (!this.UseStepPipeline)
            {
                Debug.LogWarning("[AB Build] Profile=" + this.name
                                 + " 当前 UseStepPipeline=false，DryRun 不起作用（旧流程不可拆步预览）");
                return;
            }

            BuildExecutionOptions options = CreateOptions();
            options.DryRun = true;
            AssetBundleBuildPipeline.BuildAndManifestWithSteps(
                CreateRequest(),
                options,
                ctx => ctx.Set(this.CreateUploadSettings()));
        }

        /// <summary>Inspector / 日志用的摘要。</summary>
        public string Describe()
        {
            string platformFolder = AssetBundlePackager.GetPlatformFolder(this.BuildTarget);
            string outputPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", this.OutputRoot, platformFolder));

            string upload = this.EnableUpload
                ? string.Format("{0}:{1}{2}", this.UploadHost, this.UploadPort, this.UploadRemotePath)
                : "未启用";

            return string.Format(
                "平台：{0}（{1}）\n清单版本：{2}{3}\n流程：{4} / 后端：{5} / 步骤：{6}\n输出：{7}\n上传：{8}",
                this.BuildTarget,
                platformFolder,
                this.ManifestVersion,
                this.AutoManifestVersion ? "（自动）" : "（手工）",
                this.UseStepPipeline ? "步骤执行器" : "旧整体流程",
                AssetBundlePackager.UseScriptableBuildPipeline ? "SBP" : "built-in",
                AbBuildStepCatalog.ForTarget(this.BuildTarget).Count,
                outputPath,
                upload);
        }

        /// <summary>
        /// 批处理入口：按资产路径执行打包。
        /// <example>
        /// Unity.exe -batchmode -projectPath &lt;proj&gt; -executeMethod
        ///   CarrotFantasy.Editor.BuildPipeline.Profiles.AbBuildProfile.BuildFromPath
        ///   -profilePath Assets/Editor/BuildEditor/BuildPipeline/Profiles/Windows.asset
        /// </example>
        /// </summary>
        public static void BuildFromPath()
        {
            string path = GetCommandLineArg("-profilePath");
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[AB Build] 缺少参数 -profilePath（指向 AbBuildProfile 资产）");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(2);
                }

                return;
            }

            AbBuildProfile profile = AssetDatabase.LoadAssetAtPath<AbBuildProfile>(path);
            if (profile == null)
            {
                Debug.LogError("[AB Build] 加载 Profile 失败: " + path);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(3);
                }

                return;
            }

            AssetBundleBuildPipeline.BuildResult result = profile.Build();
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(result.Success ? 0 : 1);
            }
        }

        private static string GetCommandLineArg(string key)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, System.StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
