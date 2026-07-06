using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 战斗视图根：BuildOnce → ResetForReplay → Dispose。
    /// </summary>
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

        /// <summary>静态战斗视图（格子 / UI 壳等）是否已 BuildOnce。</summary>
        public bool IsContentBuilt { get; private set; }

        /// <summary>对象池回收时移出屏幕的默认位置。</summary>
        public static readonly Vector3 OffscreenPoolPosition = new Vector3(1000f, 1000f, 0f);

        /// <summary>从池取出后挂到容器并清零 local 变换，便于 InitViewPosition 写入正确坐标。</summary>
        public static void AttachPooledVisualToContainer(Transform visual, Transform container)
        {
            if (visual == null || container == null)
            {
                return;
            }

            visual.SetParent(container, false);
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
        }

        public Vector3 initTran = OffscreenPoolPosition;

        public BattleView_base(BaseBattle battle, BattleViewHost viewHost)
        {
            battle.isIgnoreViewListener = false;
            this.battle = battle;
            this.ViewHost = viewHost;
            this.rootGameObject = viewHost != null ? viewHost.gameObject : null;

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

        /// <summary>离场景 Dispose 时销毁 Grid/UI 等内容容器；同关重开勿调用。</summary>
        public void DestroySceneContentContainers()
        {
            BVSceneComponent scene = this.TryGetComponent(BattleViewComponentType.SCENE) as BVSceneComponent;
            if (scene != null)
            {
                scene.DestroyContentContainers();
            }
        }

        public virtual void Init()
        {
            this.AddListener();
        }

        /// <summary>预加载完成后 BuildOnce：标准容器 + 依赖 Prefab 的静态视图。</summary>
        public bool BuildContentComponents()
        {
            if (this.IsContentBuilt)
            {
                return true;
            }

            if (!this.InitContentComponentsInternal())
            {
                return false;
            }

            this.IsContentBuilt = true;
            return true;
        }

        bool InitContentComponentsInternal()
        {
            if (this.ViewHost == null)
            {
                BattleFlowLog.Abort("InitContentComponents", "ViewHost 未绑定");
                return false;
            }

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

                if (component.IsBuilt)
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

            return true;
        }

        /// <summary>校验 SceneContainer 与 Grid 是否就绪（Build 或 Reset 后）。</summary>
        public bool ValidateSceneContent()
        {
            if (this.ViewHost == null)
            {
                return false;
            }

            if (this.ViewHost.GetSceneContainerChildCount() < 6)
            {
                return false;
            }

            return this.ViewHost.GetContainerChildCount("GridContainer") > 0;
        }

        /// <summary>
        /// 同关重开唯一入口：回池 → resetModel → 各组件 ApplyModelForReplay。
        /// </summary>
        public void ResetForReplay(Action resetModel)
        {
            for (int i = 0; i < this.componentList.Count; i++)
            {
                this.componentList[i].ReturnUnitsToPoolForReplay();
            }

            BVBattleWorldUiComponent worldUi = this.TryGetComponent(BattleViewComponentType.WORLD_UI) as BVBattleWorldUiComponent;
            if (worldUi != null)
            {
                worldUi.ClearTransientEffectsForReplay();
            }

            resetModel();

            for (int i = 0; i < this.componentList.Count; i++)
            {
                this.componentList[i].ApplyModelForReplay();
            }

            this.isStart = false;
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
            this.IsContentBuilt = false;
        }

        public virtual void Dispose()
        {
            this.RemoveListener();
            for (int i = this.componentList.Count - 1; i >= 0; i--)
            {
                this.componentList[i].ClearGameInfo();
            }

            this.DestroySceneContentContainers();

            for (int i = this.componentList.Count - 1; i >= 0; i--)
            {
                this.componentList[i].Dispose();
            }
            this.componentList.Clear();
            this.componentDic.Clear();
            this.IsContentBuilt = false;
            GameViewObjectPool.Instance.Dispose();
            this.bvEventDispatcher.Dispose();
        }
    }
}
