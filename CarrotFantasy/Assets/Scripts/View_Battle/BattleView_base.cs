using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BattleView_base
    {
        public GameObject rootGameObject;

        public BaseBattle battle;
        public EventDispatcher eventDispatcher { get; private set; }

        public EventDispatcher bvEventDispatcher { get; private set; }

        protected Dictionary<String, BaseBattleViewComponent> componentDic = new Dictionary<string, BaseBattleViewComponent>();
        protected List<BaseBattleViewComponent> componentList = new List<BaseBattleViewComponent>();

        public bool isStart;
        public bool isGameObjectLoaded;

        public Vector3 initTran = new Vector3(1000, 1000, 0);

        public BattleView_base(BaseBattle battle)
        {
            battle.isIgnoreViewListener = false;
            this.battle = battle;

            this.eventDispatcher = this.battle.eventDispatcher;
            this.bvEventDispatcher = new EventDispatcher();
            this.isStart = false;
        }

        public virtual void AddListener() { }

        public virtual void RemoveListener() { }

        public virtual void Init()
        {
            this.AddListener();
        }

        public virtual void InitComponents() //最后调用
        {
            for (int i = 0; i < this.componentList.Count; i++)
            {
                this.componentList[i].Init();
            }
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
                this.componentList[i].Start();
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

        public virtual void Dispose()
        {
            this.RemoveListener();
            for (int i = this.componentList.Count - 1; i >= 0; i--)
            {
                this.componentList[i].Dispose();
            }
            this.componentList.Clear();
            this.componentDic.Clear();
            AssetObjectPool.Instance.Dispose();
            GameViewObjectPool.Instance.Dispose();
            this.bvEventDispatcher.Dispose();
        }
    }
}
