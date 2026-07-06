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

        /// <summary>BattleScene 加载完成后查找场景内唯一的 BattleViewHost。</summary>
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

            GameObject[] roots = targetScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                {
                    continue;
                }

                BattleViewHost host = root.GetComponent<BattleViewHost>();
                if (host != null)
                {
                    return host;
                }

                host = root.GetComponentInChildren<BattleViewHost>(true);
                if (host != null)
                {
                    return host;
                }
            }

            BattleFlowLog.Abort(
                "FindInLoadedBattleScene",
                "scene=" + targetScene.name + " 中未找到 BattleViewHost");
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
            }
        }

        void ApplySceneContainer(GameObject container)
        {
            if (container == null)
            {
                return;
            }

            if (container.transform.parent != this.transform)
            {
                BattleFlowLog.Abort(
                    "ApplySceneContainer",
                    "SceneContainer#" + container.GetInstanceID() +
                    " 不是 BattleRoot#" + this.gameObject.GetInstanceID() + " 的直接子节点");
                return;
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
