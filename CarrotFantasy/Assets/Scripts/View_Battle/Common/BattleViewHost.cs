using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 挂在 BattleRoot 上，持久持有 SceneContainer。
    /// SceneContainer 由 <see cref="BattleScene.InitSceneObject"/> 显式绑定，不在 Awake 里猜测/新建/销毁。
    /// </summary>
    public class BattleViewHost : MonoBehaviour
    {
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

        GameObject sceneContainer;
        readonly Dictionary<string, GameObject> containerDic = new Dictionary<string, GameObject>();

        public GameObject SceneContainer
        {
            get { return this.sceneContainer; }
        }

        /// <summary>由 BattleScene 在进关时调用，绑定 .unity 里已有的 SceneContainer。</summary>
        public void BindSceneContainer(GameObject container)
        {
            if (container == null)
            {
                BattleFlowLog.Abort("BindSceneContainer", "container=null");
                return;
            }

            if (container.transform.parent != this.transform)
            {
                BattleFlowLog.Abort(
                    "BindSceneContainer",
                    "SceneContainer#" + container.GetInstanceID() +
                    " 不是 BattleRoot#" + this.gameObject.GetInstanceID() + " 的直接子节点");
                return;
            }

            this.sceneContainer = container;
            this.containerDic.Clear();
            this.SyncExistingContainers();
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

        bool RequireSceneContainerBound(string step)
        {
            if (this.sceneContainer != null)
            {
                return true;
            }

            BattleFlowLog.Abort(
                step,
                "SceneContainer 未绑定，请检查 BattleScene.InitSceneObject 是否先于 Session 执行");
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

        /// <summary>重开时仅清理 Grid/UI 等子容器，永不销毁 SceneContainer 节点。</summary>
        public void ClearRegisteredContainers()
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
    }
}
