using UnityEngine;
using UnityEditor;

// Runs on model import; also exposes a menu to build prefab from selected FBX in Hierarchy.
public class FbxToPrefabProcessor : AssetPostprocessor
{
    void OnPostprocessModel(GameObject input)
    {
        if (assetPath.Contains(".fbx") && assetPath.Contains("Assets/YourTargetFolder"))
        {
            CreatePrefabFromModel(input);
        }
    }

    [MenuItem("Tools/Create Prefab From Selected FBX")]
    static void CreatePrefabFromSelectedFBX()
    {
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject != null)
        {
            CreatePrefabFromModel(selectedObject);
        }
        else
        {
            Debug.LogWarning("Please select an FBX instance in the Hierarchy first.");
        }
    }

    static void CreatePrefabFromModel(GameObject modelGameObject)
    {
        string originalPath = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(modelGameObject) ?? modelGameObject);
        string prefabPath = originalPath.Replace(".fbx", ".prefab");

        if (string.IsNullOrEmpty(originalPath) || !originalPath.EndsWith(".fbx"))
        {
            Debug.LogWarning("Selected object is not an FBX asset or path is invalid.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            Debug.LogWarning($"A prefab already exists at {prefabPath}. Aborting to avoid overwriting.");
            return;
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(modelGameObject, prefabPath);
        if (prefab != null)
        {
            Debug.Log($"Prefab created successfully at: {prefabPath}");
        }
        else
        {
            Debug.LogError("Failed to create prefab.");
        }
        AssetDatabase.Refresh();
    }
}
