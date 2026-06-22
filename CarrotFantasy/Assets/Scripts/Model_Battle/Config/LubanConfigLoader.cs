using System.Collections.Generic;
using System.IO;
using cfg;
using SimpleJSON;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CarrotFantasy
{
    /// <summary>
    /// Luban 配置表加载：优先从热更 AB（config/luban）读取，Resources 作兜底。
    /// </summary>
    public static class LubanConfigLoader
    {
        const string ResourceDir = "Config/Luban/";

        static AssetBundle configBundle;
        static Tables tables;
        static readonly Dictionary<string, string> jsonCache = new Dictionary<string, string>();

        public static Tables Tables
        {
            get
            {
                if (tables == null)
                {
                    tables = new Tables(LoadJson);
                }

                return tables;
            }
        }

        /// <summary>热更下载完成后调用，使下次访问重新从磁盘/AB 读取。</summary>
        public static void Reload()
        {
            tables = null;
            jsonCache.Clear();
            UnloadConfigBundle();
        }

        static JSONNode LoadJson(string file)
        {
            string text = LoadJsonText(file);
            if (string.IsNullOrEmpty(text))
            {
                Debug.LogError("Luban config not found: " + file);
                return JSONNull.Instance;
            }

            return JSONNode.Parse(text);
        }

        static string LoadJsonText(string file)
        {
            if (jsonCache.TryGetValue(file, out string cached))
            {
                return cached;
            }

            string text = TryLoadFromAssetBundle(file);
            if (string.IsNullOrEmpty(text))
            {
                text = TryLoadFromResources(file);
            }

            jsonCache[file] = text ?? string.Empty;
            return jsonCache[file];
        }

        static string TryLoadFromResources(string file)
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourceDir + file);
            return asset != null ? asset.text : null;
        }

        static string TryLoadFromAssetBundle(string file)
        {
#if UNITY_EDITOR
            if (ShouldUseEditorAssetLoader())
            {
                TextAsset asset = EditorAssetLoader.LoadAssetAtPath(
                    LubanConfigAbPaths.BundleName,
                    file,
                    typeof(TextAsset)) as TextAsset;
                return asset != null ? asset.text : null;
            }
#endif
            EnsureConfigBundleLoaded();
            if (configBundle == null)
            {
                return null;
            }

            TextAsset fromBundle = configBundle.LoadAsset<TextAsset>(file);
            return fromBundle != null ? fromBundle.text : null;
        }

        static void EnsureConfigBundleLoaded()
        {
            if (configBundle != null)
            {
                return;
            }

            string path = AssetBundlePathHelper.GetRuntimeLoadPath(LubanConfigAbPaths.BundleName);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[LubanConfigLoader] AB path empty: " + LubanConfigAbPaths.BundleName);
                return;
            }

#if !UNITY_ANDROID || UNITY_EDITOR
            if (!path.Contains("://") && !File.Exists(path))
            {
                Debug.LogWarning("[LubanConfigLoader] AB file missing: " + path);
                return;
            }
#endif

            configBundle = AssetBundle.LoadFromFile(path);
            if (configBundle == null)
            {
                Debug.LogWarning("[LubanConfigLoader] LoadFromFile failed: " + path);
            }
        }

        static void UnloadConfigBundle()
        {
            if (configBundle == null)
            {
                return;
            }

            configBundle.Unload(true);
            configBundle = null;
        }

#if UNITY_EDITOR
        static bool ShouldUseEditorAssetLoader()
        {
            var loadMode = (LoadMode)EditorPrefs.GetInt("GameLoadMode", 0);
            return loadMode == LoadMode.Development || loadMode == LoadMode.DebugMode;
        }
#endif
    }
}
