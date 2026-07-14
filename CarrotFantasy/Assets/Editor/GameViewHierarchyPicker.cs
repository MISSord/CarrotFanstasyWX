using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Play 模式下的 Game 视图点选工具（EditorApplication.update 驱动）：
/// - Alt+Z：开关
/// - 开启后在 Game 视图右键：选中指针下对象到 Hierarchy
/// 拾取优先级：UI → Physics3D → Physics2D → Renderer 包围盒
/// </summary>
[InitializeOnLoad]
public static class GameViewHierarchyPicker
{
    const string LogTag = "GameViewPicker";
    //const string PrefsWantEnabled = "GameViewHierarchyPicker_WantEnabled";

    static bool s_Active;
    static int s_LastToggleFrame = -1;
    static int s_LastPickFrame = -1;
    static bool s_WasAltZDown;

    static GameViewHierarchyPicker()
    {
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            s_Active = false;
            s_WasAltZDown = false;
            s_LastToggleFrame = -1;
            s_LastPickFrame = -1;

            //if (EditorPrefs.GetBool(PrefsWantEnabled, false))
            //{
            //    SetActive(true, log: true);
            //}
            //else
            //{
            //    Debug.Log($"[{LogTag}] 已就绪。Play 中按 Alt+Z 开启；也可通过菜单 Tools/Game 视图点选 Hierarchy。");
            //}
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            s_Active = false;
            s_WasAltZDown = false;
        }
    }

    static void OnEditorUpdate()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused)
        {
            s_WasAltZDown = false;
            return;
        }

        TryToggleHotkey();
        TryPickOnRightClick();
    }

    static void TryToggleHotkey()
    {
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        bool z = Input.GetKey(KeyCode.Z);
        bool altZDown = alt && z;

        // 边沿检测：避免 EditorApplication.update 一帧多次调用导致连触发
        bool risingEdge = altZDown && !s_WasAltZDown;
        s_WasAltZDown = altZDown;

        if (!risingEdge)
        {
            return;
        }

        if (Time.frameCount == s_LastToggleFrame)
        {
            return;
        }

        s_LastToggleFrame = Time.frameCount;
        SetActive(!s_Active, log: true);
    }

    static void TryPickOnRightClick()
    {
        if (!s_Active)
        {
            return;
        }

        if (!Input.GetMouseButtonDown(1))
        {
            return;
        }

        EditorWindow over = EditorWindow.mouseOverWindow;
        if (over == null || over.GetType().Name != "GameView")
        {
            return;
        }

        if (Time.frameCount == s_LastPickFrame)
        {
            return;
        }

        s_LastPickFrame = Time.frameCount;
        TryPickAndSelect(Input.mousePosition);
    }

    static void SetActive(bool active, bool log)
    {
        s_Active = active;
        //EditorPrefs.SetBool(PrefsWantEnabled, active);
        if (log)
        {
            Debug.Log(active
                ? $"[{LogTag}] 已开启：在 Game 视图右键可选中 Hierarchy 对象（再按 Alt+Z 关闭）"
                : $"[{LogTag}] 已关闭");
        }
    }

    static void TryPickAndSelect(Vector2 screenPos)
    {
        GameObject picked = PickUi(screenPos)
            ?? PickPhysics3D(screenPos)
            ?? PickPhysics2D(screenPos)
            ?? PickByRenderers(screenPos);

        if (picked == null)
        {
            Debug.Log($"[{LogTag}] 未命中任何对象");
            return;
        }

        Selection.activeGameObject = picked;
        EditorGUIUtility.PingObject(picked);
    }

    static GameObject PickUi(Vector2 screenPos)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return null;
        }

        var pointerData = new PointerEventData(eventSystem)
        {
            position = screenPos,
        };

        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);
        return results.Count > 0 ? results[0].gameObject : null;
    }

    static GameObject PickPhysics3D(Vector2 screenPos)
    {
        Camera camera = ResolveCamera();
        if (camera == null)
        {
            return null;
        }

        Ray ray = camera.ScreenPointToRay(screenPos);
        return Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, ~0, QueryTriggerInteraction.Collide)
            ? hit.collider.gameObject
            : null;
    }

    static GameObject PickPhysics2D(Vector2 screenPos)
    {
        Camera camera = ResolveCamera();
        if (camera == null)
        {
            return null;
        }

        Ray ray = camera.ScreenPointToRay(screenPos);
        RaycastHit2D hit = Physics2D.GetRayIntersection(ray, float.MaxValue);
        return hit.collider != null ? hit.collider.gameObject : null;
    }

    static GameObject PickByRenderers(Vector2 screenPos)
    {
        Camera camera = ResolveCamera();
        if (camera == null)
        {
            return null;
        }

        Ray ray = camera.ScreenPointToRay(screenPos);
        Renderer[] renderers = Object.FindObjectsOfType<Renderer>();
        GameObject best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || !renderer.isVisible)
            {
                continue;
            }

            if (renderer.bounds.IntersectRay(ray, out float distance) && distance < bestDistance)
            {
                bestDistance = distance;
                best = renderer.gameObject;
            }
        }

        return best;
    }

    static Camera ResolveCamera()
    {
        Camera main = Camera.main;
        if (main != null && main.isActiveAndEnabled)
        {
            return main;
        }

        Camera[] cameras = Camera.allCameras;
        Camera best = null;
        float bestDepth = float.NegativeInfinity;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || !camera.isActiveAndEnabled)
            {
                continue;
            }

            if (camera.depth >= bestDepth)
            {
                bestDepth = camera.depth;
                best = camera;
            }
        }

        return best;
    }

    static string GetHierarchyPath(GameObject go)
    {
        if (go == null)
        {
            return string.Empty;
        }

        string path = go.name;
        Transform parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    [MenuItem("Tools/Game 视图点选 Hierarchy/开启", false, 500)]
    static void MenuEnable()
    {
        //EditorPrefs.SetBool(PrefsWantEnabled, true);
        if (EditorApplication.isPlaying)
        {
            SetActive(true, log: true);
        }
        else
        {
            Debug.Log($"[{LogTag}] 已标记开启：进入 Play 后自动生效。");
        }
    }

    [MenuItem("Tools/Game 视图点选 Hierarchy/关闭", false, 501)]
    static void MenuDisable()
    {
        //EditorPrefs.SetBool(PrefsWantEnabled, false);
        if (EditorApplication.isPlaying)
        {
            SetActive(false, log: true);
        }
        else
        {
            Debug.Log($"[{LogTag}] 已关闭。");
        }
    }
}
