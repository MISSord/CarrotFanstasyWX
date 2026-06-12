using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarrotFantasy
{
    /// <summary>进入战斗场景时校正摄像机与根节点，保证 2D 战斗内容落在主相机视野内。</summary>
    public static class BattleScenePresentation
    {
        public const string BattleRootName = "BattleRoot";
        public const string BattleUnitySceneName = "BattleScene";

        const float DefaultOrthoSize = 5f;
        static readonly Vector3 DefaultCameraPosition = new Vector3(0f, 0f, -10f);

        /// <summary>场景进入时解析 BattleRoot，并清理 DontDestroyOnLoad 中的残留副本。</summary>
        public static GameObject ResolveBattleRootForSceneEntry()
        {
            CleanupStaleBattleRootsInDontDestroyOnLoad();
            return FindBattleRootInUnityScene(BattleUnitySceneName);
        }

        /// <summary>战斗进行中解析 BattleRoot（不清理 DDOL，避免误删仍在使用的根节点）。</summary>
        public static GameObject ResolveBattleRootInActiveScene()
        {
            GameObject root = FindBattleRootInUnityScene(BattleUnitySceneName);
            if (root != null)
            {
                return root;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.name != BattleUnitySceneName)
            {
                return FindBattleRootInUnityScene(activeScene.name);
            }

            return null;
        }

        /// <summary>只使用场景内已有的 BattleRoot，不运行时创建、不 Destroy 重复根（避免误删已挂载内容）。</summary>
        static GameObject FindBattleRootInUnityScene(string unitySceneName)
        {
            if (string.IsNullOrEmpty(unitySceneName))
            {
                BattleFlowLog.Abort("FindBattleRoot", "unitySceneName 为空");
                return null;
            }

            Scene targetScene = SceneLoader.FindLoadedSceneByName(unitySceneName);
            if (!targetScene.IsValid())
            {
                Scene activeScene = SceneManager.GetActiveScene();
                BattleFlowLog.Abort(
                    "FindBattleRoot",
                    "scene=" + unitySceneName +
                    " 未加载; active=" + activeScene.name +
                    " sceneCount=" + SceneManager.sceneCount +
                    " loaded=[" + SceneLoader.BuildLoadedSceneNameList() + "]");
                return null;
            }

            GameObject[] roots = targetScene.GetRootGameObjects();
            GameObject namedRoot = null;
            GameObject rootWithSceneContainer = null;

            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null || root.name != BattleRootName)
                {
                    continue;
                }

                if (namedRoot == null)
                {
                    namedRoot = root;
                }

                if (HasDirectChildNamed(root.transform, BattleViewHost.SceneContainerName))
                {
                    rootWithSceneContainer = root;
                }
            }

            int rootCount = CountNamedRoots(roots, BattleRootName);
            if (rootCount > 1)
            {
                BattleFlowLog.Step(
                    "FindBattleRoot",
                    "警告：场景内存在 " + rootCount +
                    " 个 BattleRoot，请检查场景是否重复。不会自动 Destroy。");
            }

            GameObject chosen = rootWithSceneContainer != null ? rootWithSceneContainer : namedRoot;
            if (chosen != null)
            {
                BattleFlowLog.Step(
                    "FindBattleRoot",
                    "scene=" + targetScene.name +
                    " BattleRoot#" + chosen.GetInstanceID() +
                    " hasSceneContainer=" + (rootWithSceneContainer != null) +
                    " childCount=" + chosen.transform.childCount +
                    " rootCount=" + rootCount);
                return chosen;
            }

            BattleFlowLog.Abort(
                "FindBattleRoot",
                "scene=" + targetScene.name + " 中未找到 BattleRoot，请检查 BattleScene.unity");
            return null;
        }

        static int CountNamedRoots(GameObject[] roots, string rootName)
        {
            int count = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == rootName)
                {
                    count++;
                }
            }

            return count;
        }

        static bool HasDirectChildNamed(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == childName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>解析 BattleRoot 下唯一的 SceneContainer；只复用场景内节点，不运行时创建。</summary>
        public static GameObject ResolveSceneContainerUnderBattleRoot(GameObject battleRoot)
        {
            if (battleRoot == null)
            {
                BattleFlowLog.Abort("ResolveSceneContainer", "battleRoot=null");
                return null;
            }

            Transform canonical = null;
            int duplicateCount = 0;
            var childNames = new StringBuilder(64);

            Transform root = battleRoot.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (childNames.Length > 0)
                {
                    childNames.Append(',');
                }

                childNames.Append(child.name);

                if (child.name == BattleViewHost.SceneContainerName)
                {
                    if (canonical == null)
                    {
                        canonical = child;
                    }
                    else
                    {
                        duplicateCount++;
                    }
                }
            }

            if (canonical != null)
            {
                if (duplicateCount > 0)
                {
                    BattleFlowLog.Step(
                        "ResolveSceneContainer",
                        "警告：BattleRoot#" + battleRoot.GetInstanceID() +
                        " 下存在多个 SceneContainer，使用第一个 #" +
                        canonical.gameObject.GetInstanceID());
                }

                BattleFlowLog.Step(
                    "ResolveSceneContainer",
                    "复用 SceneContainer#" + canonical.gameObject.GetInstanceID() +
                    " children=" + canonical.childCount +
                    " BattleRoot#" + battleRoot.GetInstanceID() +
                    " siblings=[" + childNames + "]");
                return canonical.gameObject;
            }

            BattleFlowLog.Abort(
                "ResolveSceneContainer",
                "BattleRoot#" + battleRoot.GetInstanceID() +
                " 下无 SceneContainer，siblings=[" + childNames + "]");
            return null;
        }

        static void CleanupStaleBattleRootsInDontDestroyOnLoad()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || scene.name != "DontDestroyOnLoad")
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int j = 0; j < roots.Length; j++)
                {
                    GameObject root = roots[j];
                    if (root != null && root.name == BattleRootName)
                    {
                        BattleFlowLog.Step(
                            "CleanupStaleBattleRoots",
                            "警告：DontDestroyOnLoad 中存在残留 BattleRoot#" + root.GetInstanceID() +
                            "，不会自动 Destroy，请检查是否误 Move 到 DDOL");
                    }
                }
            }
        }

        public static void EnsureBattleRootInActiveScene(GameObject battleRoot)
        {
            if (battleRoot == null)
            {
                return;
            }

            Scene rootScene = battleRoot.scene;
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene != rootScene)
            {
                BattleFlowLog.Step(
                    "EnsureBattleRootInActiveScene",
                    "SetActiveScene " + rootScene.name +
                    " (BattleRoot#" + battleRoot.GetInstanceID() +
                    ", was " + activeScene.name + ")");
                SceneManager.SetActiveScene(rootScene);
            }
        }

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
