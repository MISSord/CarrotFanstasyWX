using System.IO;

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
        public const string HotUpdateFolder = "hybridclr/hotupdate";
        public const string AotFolder = "hybridclr/aot";

        public static readonly string[] DefaultAotMetaAssemblies =
        {
            "mscorlib",
            "System",
            "System.Core",
            "UnityEngine.CoreModule",
        };

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
