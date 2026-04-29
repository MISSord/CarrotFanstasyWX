using CarrotFantasy;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class PrefabUIImageLoaderAutoBinder
{
    private static bool _isProcessingWillSave;

    static PrefabUIImageLoaderAutoBinder()
    {
        PrefabStage.prefabSaving -= OnPrefabSaving;
        PrefabStage.prefabSaving += OnPrefabSaving;
    }

    private static void OnPrefabSaving(GameObject prefabRoot)
    {
        if (prefabRoot == null)
        {
            return;
        }

        ProcessPrefabRoot(prefabRoot);
    }

    internal static bool ProcessPrefabRoot(GameObject prefabRoot)
    {
        if (prefabRoot == null)
        {
            return false;
        }

        bool changed = false;
        Graphic[] graphics = prefabRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (!(graphic is Image) && !(graphic is RawImage))
            {
                continue;
            }

            UIImageLoader loader = graphic.GetComponent<UIImageLoader>();
            if (loader == null)
            {
                loader = graphic.gameObject.AddComponent<UIImageLoader>();
                changed = true;
            }

            if (loader != null && loader.EditorRefreshBindingAndPath())
            {
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(prefabRoot);
        }

        return changed;
    }

    internal static bool IsProcessingWillSave()
    {
        return _isProcessingWillSave;
    }

    internal static void SetProcessingWillSave(bool processing)
    {
        _isProcessingWillSave = processing;
    }
}

public class PrefabUIImageLoaderSaveProcessor : AssetModificationProcessor
{
    public static string[] OnWillSaveAssets(string[] paths)
    {
        if (PrefabUIImageLoaderAutoBinder.IsProcessingWillSave())
        {
            return paths;
        }

        PrefabUIImageLoaderAutoBinder.SetProcessingWillSave(true);
        try
        {
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
                if (prefabRoot == null)
                {
                    continue;
                }

                bool changed = false;
                try
                {
                    changed = PrefabUIImageLoaderAutoBinder.ProcessPrefabRoot(prefabRoot);
                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }
        finally
        {
            PrefabUIImageLoaderAutoBinder.SetProcessingWillSave(false);
        }

        return paths;
    }
}

public static class PrefabUIImageLoaderAssetMenu
{
    [MenuItem("Assets/批量处理预制体方法", false, 2000)]
    private static void BatchProcessSelectedPrefabs()
    {
        CleanUnusedCanvasRendererInSelectedPrefabs();
        RefreshSelectedPrefabs();
    }

    [MenuItem("Assets/批量处理预制体方法", true)]
    private static bool BatchProcessSelectedPrefabsValidate()
    {
        return HasValidPrefabSelection();
    }

    [MenuItem("Assets/刷新UIImageLoader与AB路径", false, 2001)]
    private static void RefreshSelectedPrefabs()
    {
        HashSet<string> prefabPaths = GetSelectedPrefabPaths();

        if (prefabPaths.Count == 0)
        {
            Debug.LogWarning("选中资源中未找到Prefab。");
            return;
        }

        int changedCount = 0;
        foreach (string prefabPath in prefabPaths)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                continue;
            }

            try
            {
                bool changed = PrefabUIImageLoaderAutoBinder.ProcessPrefabRoot(prefabRoot);
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedCount++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"UIImageLoader刷新完成，处理Prefab {prefabPaths.Count} 个，发生变更 {changedCount} 个。");
    }

    [MenuItem("Assets/刷新UIImageLoader与AB路径", true)]
    private static bool RefreshSelectedPrefabsValidate()
    {
        return HasValidPrefabSelection();
    }

    private static void CleanUnusedCanvasRendererInSelectedPrefabs()
    {
        HashSet<string> prefabPaths = GetSelectedPrefabPaths();
        if (prefabPaths.Count == 0)
        {
            return;
        }

        int changedPrefabCount = 0;
        int removedRendererCount = 0;
        foreach (string prefabPath in prefabPaths)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                continue;
            }

            try
            {
                int removedInPrefab = RemoveUnusedCanvasRenderer(prefabRoot);
                if (removedInPrefab > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                    removedRendererCount += removedInPrefab;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        if (changedPrefabCount > 0)
        {
            Debug.Log($"CanvasRenderer清理完成，处理Prefab {prefabPaths.Count} 个，变更Prefab {changedPrefabCount} 个，移除组件 {removedRendererCount} 个。");
        }
    }

    private static int RemoveUnusedCanvasRenderer(GameObject prefabRoot)
    {
        int removedCount = 0;
        CanvasRenderer[] renderers = prefabRoot.GetComponentsInChildren<CanvasRenderer>(true);
        for (int i = renderers.Length - 1; i >= 0; i--)
        {
            CanvasRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            // CanvasRenderer通常由Graphic体系驱动；没有Graphic时视为无用组件。
            Graphic graphic = renderer.GetComponent<Graphic>();
            if (graphic != null)
            {
                continue;
            }

            Object.DestroyImmediate(renderer, true);
            removedCount++;
        }

        return removedCount;
    }

    private static HashSet<string> GetSelectedPrefabPaths()
    {
        string[] selectedGuids = Selection.assetGUIDs;
        HashSet<string> prefabPaths = new HashSet<string>();
        if (selectedGuids == null || selectedGuids.Length == 0)
        {
            return prefabPaths;
        }

        for (int i = 0; i < selectedGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(selectedGuids[i]);
            CollectPrefabPaths(path, prefabPaths);
        }

        return prefabPaths;
    }

    private static bool HasValidPrefabSelection()
    {
        return GetSelectedPrefabPaths().Count > 0;
    }

    private static void CollectPrefabPaths(string path, HashSet<string> output)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (path.EndsWith(".prefab"))
        {
            output.Add(path);
            return;
        }

        if (!AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { path });
        for (int i = 0; i < guids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.IsNullOrEmpty(prefabPath) && prefabPath.EndsWith(".prefab"))
            {
                output.Add(prefabPath);
            }
        }
    }
}
