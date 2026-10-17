#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif
using UnityEngine;

public static class EditorAssetLoader
{
#if UNITY_EDITOR
    /// <summary>编辑器下按 AB 名收集包内全部指定类型资源（Development/Debug 直读用）。</summary>
    public static T[] LoadAllAssetsFromBundle<T>(string bundleName) where T : Object
    {
        if (string.IsNullOrEmpty(bundleName))
        {
            return System.Array.Empty<T>();
        }

        string[] paths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
        if (paths == null || paths.Length == 0)
        {
            return System.Array.Empty<T>();
        }

        var list = new List<T>();
        var seen = new HashSet<int>();
        for (int i = 0; i < paths.Length; i++)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(paths[i]);
            if (assets == null)
            {
                continue;
            }

            for (int j = 0; j < assets.Length; j++)
            {
                T typed = assets[j] as T;
                if (typed == null)
                {
                    continue;
                }

                int id = typed.GetInstanceID();
                if (!seen.Add(id))
                {
                    continue;
                }

                list.Add(typed);
            }
        }

        return list.ToArray();
    }
#endif

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
                    //Debug.Log($"资源加载成功：{path} -> {loadedAsset.name} ({loadedAsset.GetType().Name})");
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
    /// <summary>
    /// 该类型是否会作为"子资源"出现（预制体的子 GameObject、组件等）。
    /// 对这类类型必须禁用"任意同类型"回退，否则名称对不上时会返回子节点。
    /// </summary>
    private static bool IsSubObjectType(System.Type expectedType)
    {
        return expectedType == typeof(GameObject) || typeof(Component).IsAssignableFrom(expectedType);
    }

    /// <summary>
    /// 在 <paramref name="fullAssetPath"/> 内挑选最合适的资源。
    /// <para>
    /// ⚠️ <c>AssetDatabase.LoadAllAssetsAtPath</c> 对预制体会返回【根节点 + 所有子 GameObject + 全部组件】。
    /// 因此"任意同类型"这条回退对 GameObject / Component 只会命中子节点，
    /// 实例化出来是个没有子节点的空壳，后续 <c>Find("子节点")</c> 就会抛 NullReference，
    /// 而报错位置离真正的"名字写错"很远，极难定位。故此类类型直接跳过该回退，改为回退主资源并告警。
    /// </para>
    /// </summary>
    private static UnityEngine.Object TryLoadBestMatch(string fullAssetPath, string assetName, System.Type expectedType)
    {
        if (string.IsNullOrEmpty(fullAssetPath) || string.IsNullOrEmpty(assetName))
        {
            return null;
        }

        UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(fullAssetPath);
        bool hasExpectedType = expectedType != null;
        bool allowAnyNameFallback = hasExpectedType && !IsSubObjectType(expectedType);

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

        // 2) 其次匹配任意名称但类型正确的资源（GameObject/Component 除外，见方法注释）
        if (allowAnyNameFallback)
        {
            for (int i = 0; i < allAssets.Length; i++)
            {
                UnityEngine.Object obj = allAssets[i];
                if (obj != null && expectedType.IsAssignableFrom(obj.GetType()))
                {
                    Debug.LogWarning(
                        "[EditorAssetLoader] 资源名与文件内资源不匹配，退化为「任意同类型」：请求=" + assetName
                        + " 实际=" + obj.name + " 类型=" + expectedType.Name + " 文件=" + fullAssetPath);
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

        // 4) 最后回退主资源（例如 Texture2D、AudioClip、Prefab 根节点）
        UnityEngine.Object mainAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fullAssetPath);
        if (mainAsset != null)
        {
            Debug.LogWarning(
                "[EditorAssetLoader] 文件内没有名为 " + assetName + " 的资源，已回退主资源 "
                + mainAsset.name + "（" + mainAsset.GetType().Name + "）文件=" + fullAssetPath
                + "。请核对资源名是否与预制体/文件内的名称完全一致（含大小写）。");
        }

        return mainAsset;
    }
#endif
}