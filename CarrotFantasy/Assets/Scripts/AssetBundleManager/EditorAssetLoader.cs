using UnityEditor;
using UnityEngine;

public static class EditorAssetLoader
{
    /// <summary>
    /// 在编辑器下加载指定路径的资源
    /// </summary>
    /// <typeparam name="T">资源的类型（例如：GameObject, Texture2D, AudioClip）</typeparam>
    /// <param name="assetPathUnderAssets">资源在"Assets"文件夹下的路径</param>
    /// <param name="assetName">资源文件名（不含扩展名）</param>
    /// <returns>加载成功的资源对象，失败则返回null</returns>
    public static UnityEngine.Object LoadAssetAtPath(string assetPathUnderAssets, string assetName, System.Type expectedType = null)
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(assetPathUnderAssets) || string.IsNullOrEmpty(assetName))
        {
            Debug.LogError("资源路径或资源名不能为空！");
            return null;
        }

        var strings = AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(assetPathUnderAssets, assetName);
        if (strings.Length > 0)
        {
            for (int i = 0; i < strings.Length; i++)
            {
                string path = strings[i];
                UnityEngine.Object loadedAsset = TryLoadBestMatch(path, assetName, expectedType);
                if (loadedAsset != null)
                {
                    Debug.Log($"资源加载成功：{path} -> {loadedAsset.name} ({loadedAsset.GetType().Name})");
                    return loadedAsset;
                }
            }

            Debug.LogError($"资源加载失败：bundle={assetPathUnderAssets}, asset={assetName}。请检查路径和类型是否正确。");
            return null;
        }

        return null;
#else
        Debug.LogWarning("AssetDatabase只能在编辑器模式下使用。");
        return null;
#endif
    }

    /// <summary>
    /// 加载指定完整路径的资源（非泛型版本）
    /// </summary>
    public static Object LoadAssetAtPath(string fullAssetPath)
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(fullAssetPath))
        {
            Debug.LogError("资源路径不能为空！");
            return null;
        }

        if (!fullAssetPath.StartsWith("Assets/"))
        {
            Debug.LogError("资源路径必须以'Assets/'开头！");
            return null;
        }

        Object loadedAsset = AssetDatabase.LoadAssetAtPath(fullAssetPath, typeof(Object));

        if (loadedAsset != null)
        {
            Debug.Log($"资源加载成功：{fullAssetPath}");
        }
        else
        {
            Debug.LogError($"资源加载失败：{fullAssetPath}");
        }

        return loadedAsset;
#else
        Debug.LogWarning("AssetDatabase只能在编辑器模式下使用。");
        return null;
#endif
    }

#if UNITY_EDITOR
    private static UnityEngine.Object TryLoadBestMatch(string fullAssetPath, string assetName, System.Type expectedType)
    {
        if (string.IsNullOrEmpty(fullAssetPath) || string.IsNullOrEmpty(assetName))
        {
            return null;
        }

        UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(fullAssetPath);
        bool hasExpectedType = expectedType != null;

        // 1) 优先匹配同名且类型正确的资源
        if (hasExpectedType)
        {
            for (int i = 0; i < allAssets.Length; i++)
            {
                UnityEngine.Object obj = allAssets[i];
                if (obj != null && obj.name == assetName && expectedType.IsAssignableFrom(obj.GetType()))
                {
                    return obj;
                }
            }
        }

        // 2) 其次匹配任意名称但类型正确的资源
        if (hasExpectedType)
        {
            for (int i = 0; i < allAssets.Length; i++)
            {
                UnityEngine.Object obj = allAssets[i];
                if (obj != null && expectedType.IsAssignableFrom(obj.GetType()))
                {
                    return obj;
                }
            }
        }

        // 3) 再匹配同名任意资源（兼容旧逻辑）
        for (int i = 0; i < allAssets.Length; i++)
        {
            UnityEngine.Object obj = allAssets[i];
            if (obj != null && obj.name == assetName)
            {
                return obj;
            }
        }

        // 4) 最后回退主资源（例如 Texture2D、AudioClip、Prefab）
        return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fullAssetPath);
    }
#endif
}