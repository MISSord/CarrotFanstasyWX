using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CarrotFantasy
{
    /// <summary>
    /// 关卡与 MapConfig JSON 加载：Editor 开发模式读 Assets/Game/Json，其余走 AB（含 PC 热更缓存）。
    /// </summary>
    public static class GameJsonLoader
    {
        static AssetBundle mapConfigBundle;
        static AssetBundle levelBundle;

        public static string LoadLevelJsonText(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            string assetName = Path.GetFileNameWithoutExtension(fileName);

#if UNITY_EDITOR
            if (ShouldUseEditorGameJsonFiles())
            {
                return TryReadEditorLevelFile(fileName);
            }

            TextAsset editorAsset = EditorAssetLoader.LoadAssetAtPath(
                GameJsonAbPaths.LevelBundle,
                assetName,
                typeof(TextAsset)) as TextAsset;
            if (editorAsset != null)
            {
                return editorAsset.text;
            }
#endif

            return TryLoadTextFromBundle(ref levelBundle, GameJsonAbPaths.LevelBundle, assetName);
        }

        public static string LoadMapConfigJsonText()
        {
#if UNITY_EDITOR
            if (ShouldUseEditorGameJsonFiles())
            {
                return TryReadEditorMapConfigFile();
            }

            TextAsset editorAsset = EditorAssetLoader.LoadAssetAtPath(
                GameJsonAbPaths.MapConfigBundle,
                GameJsonAbPaths.MapConfigAsset,
                typeof(TextAsset)) as TextAsset;
            if (editorAsset != null)
            {
                return editorAsset.text;
            }
#endif

            return TryLoadTextFromBundle(
                ref mapConfigBundle,
                GameJsonAbPaths.MapConfigBundle,
                GameJsonAbPaths.MapConfigAsset);
        }

        public static void Reload()
        {
            UnloadBundle(ref mapConfigBundle);
            UnloadBundle(ref levelBundle);
        }

#if UNITY_EDITOR
        static bool ShouldUseEditorGameJsonFiles()
        {
            var loadMode = (LoadMode)EditorPrefs.GetInt("GameLoadMode", 0);
            return loadMode == LoadMode.Development || loadMode == LoadMode.DebugMode;
        }

        static string TryReadEditorLevelFile(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "Game", "Json", "Level", fileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        static string TryReadEditorMapConfigFile()
        {
            string path = Path.Combine(Application.dataPath, "Game", "Json", "MapConfig.json");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
#endif

        static string TryLoadTextFromBundle(ref AssetBundle bundle, string bundleName, string assetName)
        {
            EnsureBundleLoaded(ref bundle, bundleName);
            if (bundle == null)
            {
                return null;
            }

            TextAsset asset = bundle.LoadAsset<TextAsset>(assetName);
            return asset != null ? asset.text : null;
        }

        static void EnsureBundleLoaded(ref AssetBundle bundle, string bundleName)
        {
            if (bundle != null)
            {
                return;
            }

            string path = AssetBundlePathHelper.GetRuntimeLoadPath(bundleName);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[GameJsonLoader] AB path empty: " + bundleName);
                return;
            }

#if !UNITY_ANDROID || UNITY_EDITOR
            if (!path.Contains("://") && !File.Exists(path))
            {
                Debug.LogWarning("[GameJsonLoader] AB file missing: " + path);
                return;
            }
#endif

            bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                Debug.LogWarning("[GameJsonLoader] LoadFromFile failed: " + path);
            }
        }

        static void UnloadBundle(ref AssetBundle bundle)
        {
            if (bundle == null)
            {
                return;
            }

            bundle.Unload(true);
            bundle = null;
        }
    }
}
