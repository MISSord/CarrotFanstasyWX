using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>进入战斗场景时校正主相机，保证 2D 战斗内容落在视野内。</summary>
    public static class BattleScenePresentation
    {
        const float DefaultOrthoSize = 5f;
        static readonly Vector3 DefaultCameraPosition = new Vector3(6.6f, 4.4f, -10f);

        public static void ConfigureMainCameraForBattle()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraGo = GameObject.Find("MainCamera");
                if (cameraGo != null)
                {
                    mainCamera = cameraGo.GetComponent<Camera>();
                }
            }

            if (mainCamera == null)
            {
                Debug.LogWarning("[BattleScenePresentation] 未找到 MainCamera，战斗画面可能不可见。");
                return;
            }

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = DefaultOrthoSize;
            mainCamera.transform.position = DefaultCameraPosition;
            mainCamera.transform.rotation = Quaternion.identity;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.45f, 0.72f, 0.35f);
            mainCamera.depth = 0;
            mainCamera.nearClipPlane = 0.1f;
            mainCamera.farClipPlane = 100f;

            if (Debug.isDebugBuild)
            {
                Debug.Log(
                    "[BattleScenePresentation] MainCamera 已配置为正交: pos=" + mainCamera.transform.position +
                    ", size=" + mainCamera.orthographicSize);
            }
        }
    }
}
