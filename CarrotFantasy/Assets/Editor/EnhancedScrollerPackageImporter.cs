#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CarrotFantasy.EditorTools
{
    /// <summary>
    /// 从 Packages/EnhancedScroller 目录导入 EnhancedScroller .unitypackage，
    /// 并统一放到 Assets/ThirdParty/EnhancedScroller。
    /// </summary>
    public static class EnhancedScrollerPackageImporter
    {
        private const string PackageDir = "Packages/EnhancedScroller";
        private const string TargetDir = "Assets/ThirdParty/EnhancedScroller";
        private const string LegacyDir = "Assets/EnhancedScroller v2";
        private const string LegacyDirAlt = "Assets/EnhancedScroller";

        [MenuItem("CarrotFantasy/第三方插件/导入 EnhancedScroller")]
        public static void ImportFromPackageDirectory()
        {
            if (IsInstalled())
            {
                if (!EditorUtility.DisplayDialog(
                        "EnhancedScroller",
                        "检测到项目中已存在 EnhancedScroller。\n是否仍要重新导入（可能覆盖现有文件）？",
                        "重新导入",
                        "取消"))
                {
                    return;
                }
            }

            var packagePath = FindUnityPackage();
            if (string.IsNullOrEmpty(packagePath))
            {
                EditorUtility.DisplayDialog(
                    "EnhancedScroller",
                    "未找到 .unitypackage 文件。\n\n" +
                    "请将官方下载的包放到：\n" +
                    PackageDir + "\n\n" +
                    "或通过 Asset Store 的 Package Manager 导入后，使用菜单：\n" +
                    "CarrotFantasy → 第三方插件 → 迁移 EnhancedScroller 到 ThirdParty",
                    "确定");
                return;
            }

            AssetDatabase.ImportPackage(packagePath, false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RelocateToThirdParty();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "EnhancedScroller",
                "导入完成。\n路径：" + TargetDir,
                "确定");
        }

        [MenuItem("CarrotFantasy/第三方插件/迁移 EnhancedScroller 到 ThirdParty")]
        public static void RelocateOnly()
        {
            if (!RelocateToThirdParty())
            {
                EditorUtility.DisplayDialog(
                    "EnhancedScroller",
                    "未找到可迁移的 EnhancedScroller 资源。\n" +
                    "请确认存在 " + LegacyDir + " 或已正确导入插件。",
                    "确定");
                return;
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("EnhancedScroller", "已迁移到：\n" + TargetDir, "确定");
        }

        [MenuItem("CarrotFantasy/第三方插件/导入 EnhancedScroller", true)]
        private static bool ValidateImportMenu()
        {
            return !Application.isPlaying;
        }

        [MenuItem("CarrotFantasy/第三方插件/迁移 EnhancedScroller 到 ThirdParty", true)]
        private static bool ValidateRelocateMenu()
        {
            return !Application.isPlaying;
        }

        public static bool IsInstalled()
        {
            return FindEnhancedScrollerScript(TargetDir) != null
                   || FindEnhancedScrollerScript(LegacyDir) != null
                   || FindEnhancedScrollerScript(LegacyDirAlt) != null;
        }

        private static string FindUnityPackage()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return null;
            }

            var searchDir = Path.Combine(projectRoot, PackageDir);
            if (!Directory.Exists(searchDir))
            {
                return null;
            }

            return Directory
                .GetFiles(searchDir, "*.unitypackage", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static bool RelocateToThirdParty()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ThirdParty"))
            {
                AssetDatabase.CreateFolder("Assets", "ThirdParty");
            }

            if (AssetDatabase.IsValidFolder(TargetDir))
            {
                // 已在目标目录
                if (FindEnhancedScrollerScript(TargetDir) != null)
                {
                    return true;
                }
            }

            if (FindEnhancedScrollerScript(TargetDir) != null)
            {
                return true;
            }

            foreach (var legacyDir in new[] { LegacyDir, LegacyDirAlt })
            {
                if (!AssetDatabase.IsValidFolder(legacyDir))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(TargetDir))
                {
                    MoveChildren(legacyDir, TargetDir);
                    if (AssetDatabase.IsValidFolder(legacyDir))
                    {
                        AssetDatabase.DeleteAsset(legacyDir);
                    }
                }
                else
                {
                    var error = AssetDatabase.MoveAsset(legacyDir, TargetDir);
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogError("[EnhancedScroller] 迁移失败: " + error);
                        return false;
                    }
                }

                if (FindEnhancedScrollerScript(TargetDir) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void MoveChildren(string sourceFolder, string destFolder)
        {
            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { sourceFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == sourceFolder || !path.StartsWith(sourceFolder + "/"))
                {
                    continue;
                }

                var relative = path.Substring(sourceFolder.Length + 1);
                var destPath = destFolder + "/" + relative;
                var destParent = Path.GetDirectoryName(destPath)?.Replace('\\', '/');
                EnsureFolderPath(destParent);

                var moveError = AssetDatabase.MoveAsset(path, destPath);
                if (!string.IsNullOrEmpty(moveError))
                {
                    Debug.LogWarning("[EnhancedScroller] 跳过: " + path + " → " + moveError);
                }
            }
        }

        private static void EnsureFolderPath(string assetFolderPath)
        {
            if (string.IsNullOrEmpty(assetFolderPath) || AssetDatabase.IsValidFolder(assetFolderPath))
            {
                return;
            }

            var parts = assetFolderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string FindEnhancedScrollerScript(string rootAssetPath)
        {
            if (!AssetDatabase.IsValidFolder(rootAssetPath))
            {
                return null;
            }

            foreach (var guid in AssetDatabase.FindAssets("EnhancedScroller t:Script", new[] { rootAssetPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/EnhancedScroller.cs"))
                {
                    return path;
                }
            }

            return null;
        }
    }
}
#endif
