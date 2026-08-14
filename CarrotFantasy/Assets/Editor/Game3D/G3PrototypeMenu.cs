using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

using CarrotFantasy.Game3D;

/// <summary>
/// 编辑器菜单：一键在打开的场景中搭建 3D 渲染框架原型。
/// 根节点 G3_PrototypeRoot 挂 G3SceneBootstrap，Play 后自动组装相机/地形/光照/示例立绘。
/// </summary>
public static class G3PrototypeMenu
{
    const string RootName = "G3_PrototypeRoot";

    [MenuItem("Tools/Game3D/Create Prototype Scene", false, 100)]
    static void CreatePrototype()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene == null || !scene.IsValid())
        {
            Debug.LogError("[Game3D] 当前没有打开场景，请先新建/打开一个场景。");
            return;
        }

        DestroyExistingRoot();

        GameObject root = new GameObject(RootName);
        root.AddComponent<G3SceneBootstrap>();

        Undo.RegisterCreatedObjectUndo(root, "Create Game3D Prototype");
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;

        Debug.Log("[Game3D] 原型根节点已创建：" + RootName + "，点击 Play 查看渲染框架效果。");
    }

    [MenuItem("Tools/Game3D/Include Game3D Shaders in Build", false, 102)]
    static void IncludeShadersInBuild()
    {
        SerializedObject graphicsSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        SerializedProperty alwaysIncluded = graphicsSettings.FindProperty("m_AlwaysIncludedShaders");
        if (alwaysIncluded == null)
        {
            Debug.LogError("[Game3D] 无法读取 Always Included Shaders。");
            return;
        }

        string[] shaderPaths =
        {
            "Assets/Game3D/Shaders/G3UnitSprite.shader",
            "Assets/Game3D/Shaders/G3Terrain.shader",
        };

        foreach (string path in shaderPaths)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
            {
                Debug.LogWarning("[Game3D] shader 不存在：" + path);
                continue;
            }

            bool found = false;
            for (int i = 0; i < alwaysIncluded.arraySize; i++)
            {
                if (alwaysIncluded.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                alwaysIncluded.InsertArrayElementAtIndex(alwaysIncluded.arraySize);
                alwaysIncluded.GetArrayElementAtIndex(alwaysIncluded.arraySize - 1).objectReferenceValue = shader;
                Debug.Log("[Game3D] 已加入 Always Included Shaders：" + shader.name);
            }
        }

        graphicsSettings.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Game3D/Rebuild Prototype", false, 101)]
    static void RebuildPrototype()
    {
        G3SceneBootstrap bootstrap = Object.FindObjectOfType<G3SceneBootstrap>();
        if (bootstrap == null)
        {
            Debug.LogError("[Game3D] 场景中未找到 G3SceneBootstrap，请先执行 Create Prototype Scene。");
            return;
        }
        bootstrap.BuildAll();
        Debug.Log("[Game3D] 原型已按当前参数重建。");
    }

    static void DestroyExistingRoot()
    {
        G3SceneBootstrap existing = Object.FindObjectOfType<G3SceneBootstrap>();
        if (existing == null)
        {
            return;
        }
        GameObject go = existing.gameObject;
        Undo.DestroyObjectImmediate(go);
    }
}
