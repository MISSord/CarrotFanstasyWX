using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BattleView_base
    {
        public GameObject rootGameObject;
        public BattleViewHost ViewHost { get; private set; }

        public BaseBattle battle;
        public EventDispatcher eventDispatcher { get; private set; }
        public EventDispatcher bvEventDispatcher { get; private set; }

        protected Dictionary<String, BaseBattleViewComponent> componentDic = new Dictionary<string, BaseBattleViewComponent>();
        protected List<BaseBattleViewComponent> componentList = new List<BaseBattleViewComponent>();

        public bool isStart;
        public bool isGameObjectLoaded;

        /// <summary>对象池回收时移出屏幕的默认位置。</summary>
        public static readonly Vector3 OffscreenPoolPosition = new Vector3(1000f, 1000f, 0f);

        public Vector3 initTran = OffscreenPoolPosition;

        public BattleView_base(BaseBattle battle, GameObject viewRoot, BattleViewHost viewHost)
        {
            battle.isIgnoreViewListener = false;
            this.battle = battle;
            this.rootGameObject = viewRoot;
            this.ViewHost = viewHost;

            this.eventDispatcher = this.battle.eventDispatcher;
            this.bvEventDispatcher = new EventDispatcher();
            this.isStart = false;
        }

        protected virtual void AddListener() { }

        protected virtual void RemoveListener() { }

        public bool HasRegisteredComponents
        {
            get { return this.componentList.Count > 0; }
        }

        public GameObject GetViewRoot()
        {
            return this.rootGameObject;
        }

        /// <summary>离开战斗场景或重开前，显式拆除 Grid/UI 等子容器。</summary>
        public void TearDownSceneContainers()
        {
            BVSceneComponent scene = this.TryGetComponent(BattleViewComponentType.SCENE) as BVSceneComponent;
            if (scene != null)
            {
                scene.TearDownRegisteredContainers();
            }
        }

        public virtual void Init()
        {
            this.AddListener();
        }

        /// <summary>预加载完成后初始化 World UI 壳、标准容器及依赖资源的视图组件。</summary>
        public bool InitContentComponents()
        {
            if (this.ViewHost == null)
            {
                BattleFlowLog.Abort("InitContentComponents", "ViewHost 未绑定");
                return false;
            }

            BattleFlowLog.ViewHostSnapshot("InitContentComponents/前", this.ViewHost);

            this.ViewHost.EnsureStandardContentContainers();

            BVBattleWorldUiComponent worldUi = this.TryGetComponent(BattleViewComponentType.WORLD_UI) as BVBattleWorldUiComponent;
            if (worldUi != null)
            {
                worldUi.EnsureCanvasesReady();
            }

            for (int i = 0; i < this.componentList.Count; i++)
            {
                BaseBattleViewComponent component = this.componentList[i];
                if (component.componentType == BattleViewComponentType.SCENE ||
                    component.componentType == BattleViewComponentType.WORLD_UI)
                {
                    continue;
                }

                try
                {
                    component.Init();
                }
                catch (Exception ex)
                {
                    BattleFlowLog.Abort("InitContentComponents", component.componentType + " -> " + ex.Message);
                    return false;
                }
            }

            BattleFlowLog.ViewHostSnapshot("InitContentComponents/后", this.ViewHost);
            return true;
        }

        public BaseBattleViewComponent TryGetComponent(String type)
        {
            BaseBattleViewComponent component;
            if (this.componentDic.TryGetValue(type, out component))
            {
                return component;
            }

            return null;
        }

        public void AddComponent(BaseBattleViewComponent component)
        {
            if (component == null) return;
            this.componentDic.Add(component.componentType, component);
            this.componentList.Add(component);
        }

        public void RemoveComponent(BaseBattleViewComponent component)
        {
            if (component == null) return;
            component.Dispose();
            bool isSuc1 = this.componentDic.Remove(component.componentType);
            bool isSuc2 = this.componentList.Remove(component);
            if (isSuc1 == false || isSuc2 == false)
            {
                //出问题

            }
        }

        public BaseBattleViewComponent GetComponent(String type)
        {
            return this.componentDic[type];
        }

        public void OnTick(float time)
        {
            if (this.battle.isPause == true) return;
            for (int i = 0; i <= componentList.Count - 1; i++)
            {
                this.componentList[i].OnTick(time);
            }
        }

        public virtual void StartGame()
        {
            if (this.isStart == true) return;
            for (int i = 0; i <= this.componentList.Count - 1; i++)
            {
                BaseBattleViewComponent component = this.componentList[i];
                if (component == null)
                {
                    continue;
                }

                component.Start();
            }
            this.isStart = true;
        }

        public virtual void ClearGameInfo()
        {
            for (int i = this.componentList.Count - 1; i >= 0; i--)
            {
                this.componentList[i].ClearGameInfo();
            }
            this.componentDic.Clear();
            this.componentList.Clear();
            this.isStart = false;
        }

        /// <summary>结束单局逻辑并清理组件状态，但不销毁 BattleViewHost 上的 SceneContainer。</summary>
        public virtual void ShutdownContentOnly()
        {
            for (int i = this.componentList.Count - 1; i >= 0; i--)
            {
                this.componentList[i].ClearGameInfo();
            }
            this.isStart = false;
        }

        public virtual void Dispose()
        {
            this.RemoveListener();
            for (int i = this.componentList.Count - 1; i >= 0; i--)
            {
                this.componentList[i].ClearGameInfo();
            }

            this.TearDownSceneContainers();

            for (int i = this.componentList.Count - 1; i >= 0; i--)
            {
                this.componentList[i].Dispose();
            }
            this.componentList.Clear();
            this.componentDic.Clear();
            GameViewObjectPool.Instance.Dispose();
            this.bvEventDispatcher.Dispose();
        }
    }
}
