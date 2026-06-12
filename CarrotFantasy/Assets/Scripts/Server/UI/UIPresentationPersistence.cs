using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局唯一 UI 根节点：UILayer / UICamera 由本类创建或接管，并 DontDestroyOnLoad。
/// 各玩法场景（MainScene、BattleScene 等）无需再放置同名节点。
/// </summary>
public static class UIPresentationPersistence
{
    const string UiLayerName = "UILayer";
    const string UiCameraName = "UICamera";

    /// <summary>与历史场景 UICamera 一致：Layer 5 + Layer 6。</summary>
    const int UiCameraCullingMask = 1 << 5 | 1 << 6;

    static GameObject _persistentUiLayer;
    static GameObject _persistentUiCameraGo;
    static Camera _persistentUiCamera;

    public static GameObject EnsureGlobalUiLayer()
    {
        if (_persistentUiLayer != null)
        {
            RemoveDuplicateRootsInActiveScene(UiLayerName, _persistentUiLayer);
            return _persistentUiLayer;
        }

        GameObject found = GameObject.Find(UiLayerName);
        if (found == null)
        {
            found = CreateDefaultUiLayer();
        }

        Object.DontDestroyOnLoad(found);
        _persistentUiLayer = found;
        return _persistentUiLayer;
    }

    public static Camera EnsureGlobalUiCamera()
    {
        if (_persistentUiCamera != null)
        {
            RemoveDuplicateRootsInActiveScene(UiCameraName, _persistentUiCameraGo);
            return _persistentUiCamera;
        }

        GameObject found = GameObject.Find(UiCameraName);
        if (found == null)
        {
            _persistentUiCamera = CreateDefaultUiCamera();
            _persistentUiCameraGo = _persistentUiCamera.gameObject;
            Object.DontDestroyOnLoad(_persistentUiCameraGo);
        }
        else
        {
            Object.DontDestroyOnLoad(found);
            _persistentUiCameraGo = found;
            _persistentUiCamera = found.GetComponent<Camera>();
            if (_persistentUiCamera == null)
            {
                _persistentUiCamera = found.AddComponent<Camera>();
                ApplyDefaultUiCameraSettings(_persistentUiCamera);
            }
        }

        return _persistentUiCamera;
    }

    static GameObject CreateDefaultUiLayer()
    {
        GameObject go = new GameObject(UiLayerName);
        go.layer = 5;
        return go;
    }

    static Camera CreateDefaultUiCamera()
    {
        GameObject go = new GameObject(UiCameraName);
        Camera camera = go.AddComponent<Camera>();
        ApplyDefaultUiCameraSettings(camera);
        go.transform.position = new Vector3(0f, 0f, -10f);
        return camera;
    }

    static void ApplyDefaultUiCameraSettings(Camera camera)
    {
        camera.clearFlags = CameraClearFlags.Depth;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1000f;
        camera.depth = 1f;
        camera.cullingMask = UiCameraCullingMask;
    }

    static void RemoveDuplicateRootsInActiveScene(string objectName, GameObject keeper)
    {
        if (keeper == null || string.IsNullOrEmpty(objectName))
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root == keeper)
            {
                continue;
            }

            if (root.name == objectName)
            {
                Object.Destroy(root);
            }
        }
    }
}
