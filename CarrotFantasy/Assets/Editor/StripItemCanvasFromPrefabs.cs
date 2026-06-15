using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>批量移除 Item 预制体上内嵌的 ItemCanvas（血条改由 MonsterCanvas 模板按需创建）。</summary>
public static class StripItemCanvasFromPrefabs
{
    const string ItemPrefabFolder = "Assets/Game/FightPart/Item";

    [MenuItem("Tools/Battle/Strip ItemCanvas From Item Prefabs")]
    public static void StripAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ItemPrefabFolder });
        int changed = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (StripOne(path))
            {
                changed++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[StripItemCanvas] 已处理 " + changed + " 个 Item 预制体。");
    }

    static bool StripOne(string assetPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
        if (root == null)
        {
            return false;
        }

        Transform itemCanvas = root.transform.Find("ItemCanvas");
        if (itemCanvas == null)
        {
            PrefabUtility.UnloadPrefabContents(root);
            return false;
        }

        Object.DestroyImmediate(itemCanvas.gameObject);
        PrefabUtility.SaveAsPrefabAsset(root, assetPath);
        PrefabUtility.UnloadPrefabContents(root);
        return true;
    }
}
