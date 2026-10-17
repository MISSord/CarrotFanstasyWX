using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// HybridCLR 热更 DLL / AOT 补充元数据在 AB 管线中的路径约定。
    /// 这些文件是原始 .dll.bytes，不是真正的 AssetBundle。
    /// </summary>
    public static class HybridCLRPaths
    {
        public const string HotUpdateAssemblyName = "CarrotFantasy.HotUpdate";

        /// <summary>清单 / 下载用的 BundleName（小写路径）。</summary>
        public const string HotUpdateBundleName =
            "hybridclr/hotupdate/carrotfantasy.hotupdate.dll.bytes";

        public const string ManifestFolder = "hybridclr";
    /// <summary>构建时按 HybridCLR 设置写出的补充元数据清单（每行一个程序集名）。</summary>
    public const string AotMetaListFileName = "aot_meta_list.txt";
        public const string HotUpdateFolder = "hybridclr/hotupdate";
        public const string AotFolder = "hybridclr/aot";

        public static readonly string[] DefaultAotMetaAssemblies =
        {
            "mscorlib",
            "System",
            "System.Core",
            "UnityEngine.CoreModule",
        };

        /// <summary>
        /// 优先读构建时生成的 aot_meta_list.txt（来自 HybridCLR 设置），没有时退回内置默认值。
        /// </summary>
        public static string[] LoadAotMetaAssemblyNames()
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "HybridCLR", "AOT", AotMetaListFileName);
                if (File.Exists(path))
                {
                    var names = new List<string>();
                    foreach (string line in File.ReadAllLines(path))
                    {
                        string trimmed = line.Trim();
                        if (trimmed.Length > 0 && !trimmed.StartsWith("#", StringComparison.Ordinal))
                        {
                            names.Add(trimmed);
                        }
                    }

                    if (names.Count > 0)
                    {
                        return names.ToArray();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HybridCLRPaths] 读取 aot_meta_list.txt 失败，使用内置默认值: " + e.Message);
            }

            return DefaultAotMetaAssemblies;
        }

        public static string GetAotMetaBundleName(string assemblyName)
        {
            return $"{AotFolder}/{assemblyName.ToLowerInvariant()}.dll.bytes";
        }

        public static bool IsHybridClrRawFile(string bundleName)
        {
            if (string.IsNullOrEmpty(bundleName))
            {
                return false;
            }

            string normalized = bundleName.Replace('\\', '/').ToLowerInvariant();
            return normalized.StartsWith(ManifestFolder + "/")
                   && normalized.EndsWith(".dll.bytes");
        }

        /// <summary>StreamingAssets 基线相对路径（与历史 P1 布局兼容）。</summary>
        public static string GetStreamingBaselineRelativePath(bool isHotUpdate, string assemblyName)
        {
            string sub = isHotUpdate ? "HotUpdate" : "AOT";
            return Path.Combine("HybridCLR", sub, assemblyName + ".dll.bytes");
        }
    }
}
