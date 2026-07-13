using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarrotFantasy
{
    /// <summary>
    /// 战斗场景唯一入口：挂在 BattleRoot 上，Awake 绑定 SceneContainer，管理 6 个内容容器。
    /// </summary>
    public class BattleViewHost : MonoBehaviour
    {
        public const string BattleUnitySceneName = "BattleScene";
        public const string SceneContainerName = "SceneContainer";

        static readonly string[] StandardContentContainers =
        {
            "GridContainer",
            "MonsterContainer",
            "TowerContainer",
            "BulletContainer",
            "ItemContainer",
            "UIContainer",
        };

        [SerializeField] GameObject sceneContainerRef;

        GameObject sceneContainer;
        readonly Dictionary<string, GameObject> containerDic = new Dictionary<string, GameObject>();

        public GameObject SceneContainer
        {
            get { return this.sceneContainer; }
        }

        public bool IsReady
        {
            get { return this.sceneContainer != null; }
        }

        void Awake()
        {
            this.EnsureSceneContainerBound();
        }

        /// <summary>进关时解析并绑定 SceneContainer（序列化引用或 direct child fallback）。</summary>
        public bool EnsureReady()
        {
            this.EnsureSceneContainerBound();
            if (this.IsReady)
            {
                return true;
            }

            BattleFlowLog.Abort(
                "BattleViewHost.EnsureReady",
                "BattleRoot#" + this.gameObject.GetInstanceID() + " 未绑定 SceneContainer");
            return false;
        }

        /// <summary>
        /// BattleScene 加载完成后查找场景内唯一的 BattleViewHost。
        /// HybridCLR IL2CPP 下场景内序列化的热更 MonoBehaviour 常会变成 Missing Script，
        /// 因此找不到时回退到对 BattleRoot 运行时 AddComponent（与 GameModeSelectGui 同类做法）。
        /// </summary>
        public static BattleViewHost FindInLoadedBattleScene()
        {
            Scene targetScene = SceneLoader.FindLoadedSceneByName(BattleUnitySceneName);
            if (!targetScene.IsValid())
            {
                Scene activeScene = SceneManager.GetActiveScene();
                if (activeScene.IsValid() && activeScene.name == BattleUnitySceneName)
                {
                    targetScene = activeScene;
                }
            }

            if (!targetScene.IsValid())
            {
                BattleFlowLog.Abort(
                    "FindInLoadedBattleScene",
                    "scene=" + BattleUnitySceneName +
                    " 未加载; active=" + SceneManager.GetActiveScene().name);
                return null;
            }

            BattleViewHost host = TryAddHostToBattleRoot(targetScene);
            if (host != null)
            {
                return host;
            }

            BattleFlowLog.Abort(
                "FindInLoadedBattleScene",
                "scene=" + targetScene.name + " 中未找到 BattleViewHost，且无法定位 BattleRoot");
            return null;
        }

        static BattleViewHost TryAddHostToBattleRoot(Scene targetScene)
        {
            GameObject battleRoot = FindBattleRoot(targetScene);
            if (battleRoot == null)
            {
                return null;
            }

            BattleViewHost existing = battleRoot.GetComponent<BattleViewHost>();
            if (existing != null)
            {
                existing.EnsureSceneContainerBound();
                return existing;
            }

            BattleViewHost host = battleRoot.AddComponent<BattleViewHost>();
            // 场景序列化的 sceneContainerRef 在 Missing Script 时会丢失，运行时按子节点名补绑。
            host.EnsureSceneContainerBound();
            if (!host.IsReady)
            {
                BattleFlowLog.Abort(
                    "TryAddHostToBattleRoot",
                    "已 AddComponent，但未找到名为 SceneContainer 的子节点");
                return null;
            }

            return host;
        }

        static GameObject FindBattleRoot(Scene targetScene)
        {
            GameObject[] roots = targetScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root != null && root.name == "BattleRoot")
                {
                    return root;
                }
            }

            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                {
                    continue;
                }

                Transform child = root.transform.Find("BattleRoot");
                if (child != null)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        /// <summary>异步预加载回调前确认 BattleRoot 仍归属已加载的 BattleScene。</summary>
        public bool IsSceneAlive()
        {
            if (!this.IsReady)
            {
                return false;
            }

            Scene scene = this.gameObject.scene;
            return scene.IsValid() &&
                   scene.isLoaded &&
                   scene.name == BattleUnitySceneName;
        }

        public void EnsureStandardContentContainers()
        {
            if (!this.RequireSceneContainerBound("EnsureStandardContentContainers"))
            {
                return;
            }

            for (int i = 0; i < StandardContentContainers.Length; i++)
            {
                this.RegisterContainer(StandardContentContainers[i]);
            }
        }

        public GameObject RegisterContainer(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (!this.RequireSceneContainerBound("RegisterContainer"))
            {
                return null;
            }

            GameObject existing;
            if (this.containerDic.TryGetValue(name, out existing) && existing != null)
            {
                return existing;
            }

            GameObject container = new GameObject(name);
            container.transform.SetParent(this.sceneContainer.transform, false);
            container.transform.localPosition = Vector3.zero;
            container.transform.localScale = Vector3.one;
            this.containerDic[name] = container;
            return container;
        }

        /// <summary>离场景 Dispose：销毁 6 个内容容器。同关重开勿调用，应 Reset 容器内物体。</summary>
        public void DestroyContentContainers()
        {
            foreach (KeyValuePair<string, GameObject> pair in this.containerDic)
            {
                if (pair.Value != null)
                {
                    GameObject.Destroy(pair.Value);
                }
            }

            this.containerDic.Clear();
        }

        public int GetSceneContainerChildCount()
        {
            return this.sceneContainer != null ? this.sceneContainer.transform.childCount : 0;
        }

        public int GetContainerChildCount(string containerName)
        {
            if (string.IsNullOrEmpty(containerName))
            {
                return 0;
            }

            GameObject container;
            if (this.containerDic.TryGetValue(containerName, out container) && container != null)
            {
                return container.transform.childCount;
            }

            if (this.sceneContainer == null)
            {
                return 0;
            }

            Transform child = this.sceneContainer.transform.Find(containerName);
            return child != null ? child.childCount : 0;
        }

        void EnsureSceneContainerBound()
        {
            if (this.sceneContainer != null)
            {
                return;
            }

            if (this.sceneContainerRef != null)
            {
                this.ApplySceneContainer(this.sceneContainerRef);
                return;
            }

            Transform child = this.transform.Find(SceneContainerName);
            if (child != null)
            {
                this.ApplySceneContainer(child.gameObject);
                return;
            }

            // 兜底：深度查找（防止层级微调后 Find 直属失败）
            Transform deep = FindDeepChild(this.transform, SceneContainerName);
            if (deep != null)
            {
                this.ApplySceneContainer(deep.gameObject);
            }
        }

        static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                Transform nested = FindDeepChild(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        void ApplySceneContainer(GameObject container)
        {
            if (container == null)
            {
                return;
            }

            // 运行时深度找到的 SceneContainer 若不是直属子节点，提到 BattleRoot 下再绑定。
            if (container.transform.parent != this.transform)
            {
                container.transform.SetParent(this.transform, false);
            }

            this.sceneContainer = container;
            this.sceneContainerRef = container;
            this.containerDic.Clear();
            this.SyncExistingContainers();
        }

        bool RequireSceneContainerBound(string step)
        {
            this.EnsureSceneContainerBound();
            if (this.sceneContainer != null)
            {
                return true;
            }

            BattleFlowLog.Abort(
                step,
                "SceneContainer 未绑定，请检查 BattleScene.unity 中 BattleViewHost 配置");
            return false;
        }

        void SyncExistingContainers()
        {
            if (this.sceneContainer == null)
            {
                return;
            }

            Transform root = this.sceneContainer.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                GameObject cached;
                if (!this.containerDic.TryGetValue(child.name, out cached) || cached == null)
                {
                    this.containerDic[child.name] = child.gameObject;
                }
            }
        }
    }
}
