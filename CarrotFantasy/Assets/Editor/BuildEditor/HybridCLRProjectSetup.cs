using System.IO;
using HybridCLR.Editor;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace CarrotFantasy.Editor
{
    /// <summary>
    /// HybridCLR 工程配置：热更程序集、补充元数据、DLL 同步到 StreamingAssets 与 AB 输出目录。
    /// </summary>
    public static class HybridCLRProjectSetup
    {
        private const string HotUpdateAssemblyName = HybridCLRPaths.HotUpdateAssemblyName;

        [MenuItem("Tools/HybridCLR/应用项目热更配置", priority = 100)]
        public static void ApplyProjectSettings()
        {
            HybridCLRSettings settings = HybridCLRSettings.Instance;
            settings.enable = true;
            settings.hotUpdateAssemblies = new[] { HotUpdateAssemblyName };
            settings.hotUpdateAssemblyDefinitions = System.Array.Empty<UnityEditorInternal.AssemblyDefinitionAsset>();

            if (settings.patchAOTAssemblies == null || settings.patchAOTAssemblies.Length == 0)
            {
                settings.patchAOTAssemblies = HybridCLRPaths.DefaultAotMetaAssemblies;
            }

            HybridCLRSettings.Save();
            Debug.Log(
                $"[HybridCLRProjectSetup] 已写入配置：hotUpdate={HotUpdateAssemblyName}，" +
                $"patchAOT=[{string.Join(", ", settings.patchAOTAssemblies)}]");
        }

        /// <summary>代码热更当前是否启用（即 HybridCLR 的 enable 开关）。</summary>
        public static bool IsCodeHotUpdateEnabled()
        {
            HybridCLRSettings settings = HybridCLRSettings.Instance;
            return settings != null && settings.enable;
        }

        [MenuItem("Tools/HybridCLR/关闭代码热更（改用 AOT 程序集）", priority = 104)]
        public static void DisableCodeHotUpdate()
        {
            HybridCLRSettings settings = HybridCLRSettings.Instance;
            settings.enable = false;
            HybridCLRSettings.Save();

            Debug.LogWarning(
                "[HybridCLRProjectSetup] 已关闭代码热更（enable=false）：\n" +
                "  - 构建改用 Unity 原生 il2cpp（不再使用 HybridCLR 那份补丁源码）\n" +
                "  - CarrotFantasy.HotUpdate 会随主包正常编译\n" +
                "  - 运行时找不到热更 DLL 时自动退回 AOT 里已编译的同名程序集\n" +
                "  - 资源热更（AB 打包 / 下载 / 清单）不受任何影响");
        }

        [MenuItem("Tools/HybridCLR/启用代码热更", priority = 105)]
        public static void EnableCodeHotUpdate()
        {
            HybridCLRSettings settings = HybridCLRSettings.Instance;
            settings.enable = true;
            HybridCLRSettings.Save();

            Debug.Log(
                "[HybridCLRProjectSetup] 已启用代码热更（enable=true）。\n" +
                "  注意：HybridCLR 的 2023 分支要求 Unity ≥ 2023.2，" +
                "当前 2023.1 会导致 il2cpp 编译失败。");
        }

        // 原「同步到 StreamingAssets」「同步到 AB 输出目录」两个菜单已被这一项完全覆盖，合并为一个入口。
        [MenuItem("Tools/HybridCLR/同步 DLL（StreamingAssets + AB 输出）", priority = 101)]
        public static void SyncDllsAll()
        {
            int copied = SyncDlls(EditorUserBuildSettings.activeBuildTarget, toStreamingAssets: true, toBuildOutput: true);
            Debug.Log($"[HybridCLRProjectSetup] 全部同步完成，成功 {copied} 个文件");
        }

        /// <summary>代码热更流水线用：同步到 StreamingAssets 与当前 AB 平台输出目录。</summary>
        public static int SyncDllsForHotUpdate(BuildTarget target)
        {
            return SyncDlls(target, toStreamingAssets: true, toBuildOutput: true);
        }

        /// <summary>
        /// AB 打包清空输出目录后调用：把 HybridCLR DLL 拷进平台 AB 目录，供 GenerateManifest 扫描入库。
        /// </summary>
        public static int EnsureDllsInAbOutput(BuildTarget target, string platformOutputPath = null)
        {
            int copied = SyncDlls(
                target,
                toStreamingAssets: false,
                toBuildOutput: true,
                platformOutputPath: platformOutputPath);
            Debug.Log($"[HybridCLRProjectSetup] 已确保 HybridCLR DLL 位于 AB 输出目录，复制 {copied} 个文件（平台 {target}）");
            return copied;
        }

        [MenuItem("Tools/HybridCLR/打开官方 Installer", priority = 200)]
        public static void OpenInstaller()
        {
            EditorApplication.ExecuteMenuItem("HybridCLR/Installer...");
        }

        [MenuItem("Tools/HybridCLR/Generate All（官方）", priority = 201)]
        public static void GenerateAll()
        {
            EditorApplication.ExecuteMenuItem("HybridCLR/Generate/All");
        }

        private static int SyncDlls(
            BuildTarget target,
            bool toStreamingAssets,
            bool toBuildOutput,
            string platformOutputPath = null)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                Debug.LogError("[HybridCLRProjectSetup] 无法解析工程根目录");
                return 0;
            }

            // HybridCLR 产物目录按「当前 Editor 激活平台」生成；AB 输出目录按传入 target 的平台文件夹写入。
            BuildTarget hybridClrTarget = EditorUserBuildSettings.activeBuildTarget;
            string hotUpdateDllDir = Path.Combine(
                projectRoot,
                SettingsUtil.GetHotUpdateDllsOutputDirByTarget(hybridClrTarget));
            string strippedAotDir = Path.Combine(
                projectRoot,
                SettingsUtil.GetAssembliesPostIl2CppStripDir(hybridClrTarget));

            HybridCLRSettings settings = HybridCLRSettings.Instance;
            string[] patchList = settings.patchAOTAssemblies ?? HybridCLRPaths.DefaultAotMetaAssemblies;

            int copied = 0;

            if (toStreamingAssets)
            {
                string streamingRoot = Path.Combine(Application.streamingAssetsPath, "HybridCLR");
                string hotUpdateOut = Path.Combine(streamingRoot, "HotUpdate");
                string aotOut = Path.Combine(streamingRoot, "AOT");
                Directory.CreateDirectory(hotUpdateOut);
                Directory.CreateDirectory(aotOut);

                copied += CopyDllAsBytes(
                    Path.Combine(hotUpdateDllDir, HotUpdateAssemblyName + ".dll"),
                    Path.Combine(hotUpdateOut, HotUpdateAssemblyName + ".dll.bytes"));

                foreach (string name in patchList)
                {
                    copied += CopyDllAsBytes(
                        Path.Combine(strippedAotDir, name + ".dll"),
                        Path.Combine(aotOut, name + ".dll.bytes"));
                }
            }

            if (toBuildOutput)
            {
                // 优先用调用方传入的平台输出目录（支持自定义 AB_OutputRoot）；
                // 未传时才回落到默认布局，避免把 DLL 拷到与实际构建无关的目录。
                string buildRoot;
                if (!string.IsNullOrEmpty(platformOutputPath))
                {
                    buildRoot = platformOutputPath;
                }
                else
                {
                    string platformFolder = AssetBundlePackager.GetPlatformFolder(target);
                    buildRoot = Path.Combine(projectRoot, "Build", "AssetBundles", platformFolder);
                }

                Directory.CreateDirectory(buildRoot);

                string hotUpdateOut = Path.Combine(buildRoot, "hybridclr", "hotupdate");
                string aotOut = Path.Combine(buildRoot, "hybridclr", "aot");
                Directory.CreateDirectory(hotUpdateOut);
                Directory.CreateDirectory(aotOut);

                copied += CopyDllAsBytes(
                    Path.Combine(hotUpdateDllDir, HotUpdateAssemblyName + ".dll"),
                    Path.Combine(hotUpdateOut, "carrotfantasy.hotupdate.dll.bytes"));

                foreach (string name in patchList)
                {
                    copied += CopyDllAsBytes(
                        Path.Combine(strippedAotDir, name + ".dll"),
                        Path.Combine(aotOut, name.ToLowerInvariant() + ".dll.bytes"));
                }
            }

            AssetDatabase.Refresh();
            return copied;
        }

        private static int CopyDllAsBytes(string srcDll, string dstBytes)
        {
            if (!File.Exists(srcDll))
            {
                Debug.LogWarning($"[HybridCLRProjectSetup] 源文件不存在，跳过: {srcDll}");
                return 0;
            }

            string dir = Path.GetDirectoryName(dstBytes);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.Copy(srcDll, dstBytes, true);
            Debug.Log($"[HybridCLRProjectSetup] 已复制: {dstBytes}");
            return 1;
        }

        [InitializeOnLoadMethod]
        private static void EnsureSettingsOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                HybridCLRSettings settings = HybridCLRSettings.Instance;
                bool dirty = false;
                if (settings.hotUpdateAssemblies == null
                    || settings.hotUpdateAssemblies.Length == 0
                    || System.Array.IndexOf(settings.hotUpdateAssemblies, HotUpdateAssemblyName) < 0)
                {
                    settings.hotUpdateAssemblies = new[] { HotUpdateAssemblyName };
                    dirty = true;
                }

                if (settings.patchAOTAssemblies == null || settings.patchAOTAssemblies.Length == 0)
                {
                    settings.patchAOTAssemblies = HybridCLRPaths.DefaultAotMetaAssemblies;
                    dirty = true;
                }

                if (dirty)
                {
                    settings.enable = true;
                    HybridCLRSettings.Save();
                    Debug.Log("[HybridCLRProjectSetup] 已自动补齐 HybridCLR 热更程序集配置");
                }
            };
        }
    }
}
