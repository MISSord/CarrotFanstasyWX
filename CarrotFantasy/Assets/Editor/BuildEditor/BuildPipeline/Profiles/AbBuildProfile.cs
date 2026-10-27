using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarrotFantasy.Editor.BuildPipeline.Profiles
{
    /// <summary>
    /// 一份"打包配置"资产：把平台、路径、压缩、开关、流程选项全部配置化。
    /// <para>
    /// 用法：右键 <c>Create → CarrotFantasy → Build → AB 打包配置</c>，
    /// 每个平台建一份（Windows / macOS / Android…），需要打包时选中对应资产点"开始打包"。
    /// </para>
    /// <para>
    /// 为什么用 ScriptableObject：
    /// </para>
    /// <list type="bullet">
    ///   <item>配置跟着工程走、可 review、可 diff，不用改代码；</item>
    ///   <item>不同平台 = 不同资产，而不是在不同 if 分支里猜当前该用哪套参数；</item>
    ///   <item>批处理可以直接按资产路径启动（见 <see cref="BuildFromPath"/>）。</item>
    /// </list>
    /// </summary>
    [CreateAssetMenu(menuName = "CarrotFantasy/Build/AB 打包配置", fileName = "AbBuildProfile")]
    public sealed class AbBuildProfile : ScriptableObject
    {
        [Header("平台")]
        public BuildTarget BuildTarget = BuildTarget.StandaloneWindows64;

        [Header("输出")]
        [Tooltip("相对工程根目录；运行时 GetServerLoadUrl 会拼成 {OutputRoot}/{平台目录}")]
        public string OutputRoot = "Build/AssetBundles";

        [Header("压缩")]
        public CompressionType Compression = CompressionType.ChunkBasedCompression;

        [Header("清单版本")]
        [Tooltip("<= 0 时自动取 SuggestNextManifestVersion（推荐保持 0，避免手工改错）")]
        public int ManifestVersion = 0;

        [Header("开关")]
        public bool ClearOutputFolder = true;
        public bool CopyToStreamingAssets = false;
        public bool ForceRebuild = false;
        public bool PackAtlasesBeforeBuild = true;
        public bool ShowManifestDialog = true;

        [Header("流程")]
        [Tooltip("true = 走步骤执行器；false = 走旧的整体流程（A/B 对比用）")]
        public bool UseStepPipeline = true;

        [Tooltip("只打印将执行的步骤，不真正打包")]
        public bool DryRun = false;

        [Tooltip("按名字跳过步骤（调试用），例如 CopyToStreamingAssets")]
        public string[] SkipSteps = new string[0];

        public AssetBundleBuildPipeline.BuildRequest CreateRequest()
        {
            int version = this.ManifestVersion > 0
                ? this.ManifestVersion
                : AssetBundleBuildSettings.SuggestNextManifestVersion(this.BuildTarget);

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

        /// <summary>按本配置执行一次完整打包。</summary>
        public AssetBundleBuildPipeline.BuildResult Build()
        {
            AssetBundleBuildPipeline.BuildRequest request = CreateRequest();

            if (!this.UseStepPipeline)
            {
                Debug.Log("[AB Build] Profile=" + this.name + " 使用旧整体流程执行");
                return AssetBundleBuildPipeline.RunLegacyFlow(request);
            }

            Debug.Log("[AB Build] Profile=" + this.name
                      + " 使用步骤执行器（" + AbBuildStepCatalog.ForTarget(this.BuildTarget).Count + " 步）");
            return AssetBundleBuildPipeline.BuildAndManifestWithSteps(request, CreateOptions());
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
            AssetBundleBuildPipeline.BuildAndManifestWithSteps(CreateRequest(), options);
        }

        /// <summary>Inspector / 日志用的摘要。</summary>
        public string Describe()
        {
            string platformFolder = AssetBundlePackager.GetPlatformFolder(this.BuildTarget);
            string outputPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", this.OutputRoot, platformFolder));

            return string.Format(
                "平台: {0}（{1}）\n流程: {2} / 后端: {3} / 步骤: {4}\n输出: {5}",
                this.BuildTarget,
                platformFolder,
                this.UseStepPipeline ? "步骤执行器" : "旧整体流程",
                AssetBundlePackager.UseScriptableBuildPipeline ? "SBP" : "built-in",
                AbBuildStepCatalog.ForTarget(this.BuildTarget).Count,
                outputPath);
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
