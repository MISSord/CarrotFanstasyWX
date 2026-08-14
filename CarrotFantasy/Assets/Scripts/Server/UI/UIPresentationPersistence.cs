using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局唯一 UI 根节点：UILayer / UICamera 由本类创建或接管，并 DontDestroyOnLoad。
/// 各玩法场景（MainScene、BattleScene 等）无需再放置同名节点。
/// URP 下 UICamera 必须是 Overlay，并挂到当前场景 MainCamera 的 Camera Stack，
/// 否则会作为第二台 Base 相机 Clear 掉主相机画面。
/// </summary>
public static class UIPresentationPersistence
{
    const string UiLayerName = "UILayer";
    const string UiCameraName = "UICamera";
    const string MainCameraName = "MainCamera";

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
            ConfigureUiCameraAsOverlay(_persistentUiCamera);
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

        ConfigureUiCameraAsOverlay(_persistentUiCamera);
        return _persistentUiCamera;
    }

    /// <summary>
    /// 将全局 UICamera（Overlay）挂到当前场景主相机（Base）的 Camera Stack。
    /// 切场景后应再次调用，因为 MainCamera 通常随场景销毁重建。
    /// </summary>
    public static void BindUiCameraToSceneMainCamera()
    {
        Camera uiCamera = EnsureGlobalUiCamera();
        Camera baseCamera = FindSceneMainCamera();
        if (uiCamera == null || baseCamera == null)
        {
            return;
        }

        BindUiCameraToBaseCamera(baseCamera, uiCamera);
    }

    public static void BindUiCameraToBaseCamera(Camera baseCamera, Camera uiCamera = null)
    {
        if (baseCamera == null)
        {
            return;
        }

        if (uiCamera == null)
        {
            uiCamera = EnsureGlobalUiCamera();
        }

        if (uiCamera == null || uiCamera == baseCamera)
        {
            return;
        }

        ConfigureUiCameraAsOverlay(uiCamera);

        UniversalAdditionalCameraData baseData = baseCamera.GetUniversalAdditionalCameraData();
        baseData.renderType = CameraRenderType.Base;

        // Overlay 只能挂在一个 Base 上；先从其它 Base 的 stack 移除
        DetachOverlayFromOtherBaseCameras(uiCamera, baseCamera);

        if (!baseData.cameraStack.Contains(uiCamera))
        {
            baseData.cameraStack.Add(uiCamera);
        }
    }

    static void ConfigureUiCameraAsOverlay(Camera camera)
    {
        if (camera == null)
        {
            return;
        }

        ApplyDefaultUiCameraSettings(camera);

        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderType = CameraRenderType.Overlay;
    }

    static void DetachOverlayFromOtherBaseCameras(Camera overlayCamera, Camera keepBaseCamera)
    {
        Camera[] cameras = Object.FindObjectsOfType<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam == null || cam == keepBaseCamera || cam == overlayCamera)
            {
                continue;
            }

            UniversalAdditionalCameraData data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null || data.renderType != CameraRenderType.Base)
            {
                continue;
            }

            if (data.cameraStack != null && data.cameraStack.Contains(overlayCamera))
            {
                data.cameraStack.Remove(overlayCamera);
            }
        }
    }

    static Camera FindSceneMainCamera()
    {
        Camera main = Camera.main;
        if (main != null)
        {
            return main;
        }

        GameObject go = GameObject.Find(MainCameraName);
        return go != null ? go.GetComponent<Camera>() : null;
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
        // Overlay 模式下不会清颜色；保留 Depth 作为误配成 Base 时的兜底
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
