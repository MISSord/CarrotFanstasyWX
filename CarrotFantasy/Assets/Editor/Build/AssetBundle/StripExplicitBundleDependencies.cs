using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;

/// <summary>
/// 放在依赖计算之后、分包之前。
/// 显式清单里的资源如果被另一个包的资源引用，把它从依赖列表里拿掉。
/// 贴图和图集一律剔除，不论是否跨包。同一个包里的其它引用保留。
/// </summary>
public sealed class StripExplicitBundleDependencies : IBuildTask
{
    public int Version
    {
        get { return 1; }
    }

#pragma warning disable 649
    [InjectContext(ContextUsage.In)]
    IBundleBuildContent m_Content;

    [InjectContext]
    IDependencyData m_DependencyData;
#pragma warning restore 649

    public ReturnCode Run()
    {
        TextureOrAtlasCache.Clear();
        var guidToBundle = new Dictionary<GUID, string>();
        foreach (KeyValuePair<string, List<GUID>> bundle in m_Content.BundleLayout)
        {
            List<GUID> assets = bundle.Value;
            for (int i = 0; i < assets.Count; i++)
            {
                guidToBundle[assets[i]] = bundle.Key;
            }
        }

        int removed = 0;
        foreach (KeyValuePair<GUID, AssetLoadInfo> pair in m_DependencyData.AssetInfo)
        {
            removed += Strip(pair.Key, pair.Value.referencedObjects, guidToBundle);
        }

        foreach (KeyValuePair<GUID, SceneDependencyInfo> pair in m_DependencyData.SceneInfo)
        {
            SceneDependencyInfo sceneInfo = pair.Value;
            List<ObjectIdentifier> kept = new List<ObjectIdentifier>();
            int sceneRemoved = Filter(pair.Key, sceneInfo.referencedObjects, guidToBundle, kept);
            if (sceneRemoved > 0 && TryAssignSceneReferences(ref sceneInfo, kept))
            {
                m_DependencyData.SceneInfo[pair.Key] = sceneInfo;
                removed += sceneRemoved;
            }
        }

        Debug.Log("[AB Build][SBP] 已从跨包依赖中剔除显式资源引用 " + removed + " 条。同包引用保留。");
        return ReturnCode.Success;
    }

    static int Strip(GUID owner, List<ObjectIdentifier> referenced, Dictionary<GUID, string> guidToBundle)
    {
        if (referenced == null || referenced.Count == 0)
        {
            return 0;
        }

        guidToBundle.TryGetValue(owner, out string ownerBundle);
        int removed = 0;
        for (int i = referenced.Count - 1; i >= 0; i--)
        {
            if (!ShouldStrip(ownerBundle, referenced[i].guid, guidToBundle))
            {
                continue;
            }

            referenced.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    static int Filter(
        GUID owner,
        IReadOnlyList<ObjectIdentifier> referenced,
        Dictionary<GUID, string> guidToBundle,
        List<ObjectIdentifier> kept)
    {
        if (referenced == null || referenced.Count == 0)
        {
            return 0;
        }

        guidToBundle.TryGetValue(owner, out string ownerBundle);
        int removed = 0;
        for (int i = 0; i < referenced.Count; i++)
        {
            ObjectIdentifier reference = referenced[i];
            if (ShouldStrip(ownerBundle, reference.guid, guidToBundle))
            {
                removed++;
                continue;
            }

            kept.Add(reference);
        }

        return removed;
    }

    static readonly Dictionary<GUID, bool> TextureOrAtlasCache = new Dictionary<GUID, bool>();

    static bool ShouldStrip(string ownerBundle, GUID referencedGuid, Dictionary<GUID, string> guidToBundle)
    {
        if (IsTextureOrAtlas(referencedGuid))
        {
            return true;
        }

        if (!guidToBundle.TryGetValue(referencedGuid, out string referencedBundle))
        {
            return false;
        }

        return string.IsNullOrEmpty(ownerBundle)
            || !string.Equals(ownerBundle, referencedBundle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>贴图和图集各自有包，引用方不再带着它们。</summary>
    static bool IsTextureOrAtlas(GUID guid)
    {
        if (TextureOrAtlasCache.TryGetValue(guid, out bool cached))
        {
            return cached;
        }

        string path = AssetDatabase.GUIDToAssetPath(guid);
        Type mainType = string.IsNullOrEmpty(path) ? null : AssetDatabase.GetMainAssetTypeAtPath(path);
        bool match = mainType != null
            && (typeof(Texture).IsAssignableFrom(mainType) || mainType.Name == "SpriteAtlas");
        TextureOrAtlasCache[guid] = match;
        return match;
    }

    /// <summary>
    /// Unity 2023 里场景依赖是只读集合，没有公开的替换入口，写回私有字段。
    /// </summary>
    static bool TryAssignSceneReferences(ref SceneDependencyInfo sceneInfo, List<ObjectIdentifier> kept)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo field = typeof(SceneDependencyInfo).GetField("referencedObjects", flags);
        if (field == null)
        {
            foreach (FieldInfo candidate in typeof(SceneDependencyInfo).GetFields(flags))
            {
                if (candidate.Name.IndexOf("eferenced", StringComparison.Ordinal) >= 0)
                {
                    field = candidate;
                    break;
                }
            }
        }

        if (field == null)
        {
            return false;
        }

        object boxed = sceneInfo;
        if (field.FieldType == typeof(ReadOnlyCollection<ObjectIdentifier>))
        {
            field.SetValue(boxed, new ReadOnlyCollection<ObjectIdentifier>(kept));
        }
        else if (field.FieldType == typeof(ObjectIdentifier[]))
        {
            field.SetValue(boxed, kept.ToArray());
        }
        else if (field.FieldType == typeof(List<ObjectIdentifier>))
        {
            field.SetValue(boxed, kept);
        }
        else
        {
            return false;
        }

        sceneInfo = (SceneDependencyInfo)boxed;
        return true;
    }
}
