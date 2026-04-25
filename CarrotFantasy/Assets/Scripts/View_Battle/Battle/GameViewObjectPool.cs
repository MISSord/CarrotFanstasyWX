using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class GameViewObjectPool
    {
        private static GameViewObjectPool gamePool;
        private Dictionary<String, List<BattleUnitView>> curObjectDic = new Dictionary<String, List<BattleUnitView>>();
        private Dictionary<String, List<BaseUnitViewComponent>> curUnitObjectDic = new Dictionary<string, List<BaseUnitViewComponent>>();

        private Dictionary<String, List<GameObject>> curGameObjectDic = new Dictionary<string, List<GameObject>>();

        public static GameViewObjectPool Instance
        {
            get
            {
                if (gamePool == null)
                {
                    gamePool = new GameViewObjectPool();
                    gamePool.Init();
                }
                return gamePool;
            }
        }

        public void Init()
        {
            this.RegisterBattleUnitView(BattleUnitViewType.Monster);
            this.RegisterBattleUnitView(BattleUnitViewType.Bullet);
            this.RegisterBattleUnitView(BattleUnitViewType.Tower);
            this.RegisterBattleUnitView(BattleUnitViewType.Item);
        }

        public void RegisterBattleUnitView(String name)
        {
            if (!curObjectDic.ContainsKey(name))
            {
                List<BattleUnitView> battleList = new List<BattleUnitView>();
                curObjectDic.Add(name, battleList);
            }
        }

        public void RegisterGameObject(String name)
        {
            if (!curGameObjectDic.ContainsKey(name))
            {
                List<GameObject> battleList = new List<GameObject>();
                curGameObjectDic.Add(name, battleList);
            }
        }

        public void RegisterUnitViewComponent(String name)
        {
            if (!curUnitObjectDic.ContainsKey(name))
            {
                List<BaseUnitViewComponent> battleList = new List<BaseUnitViewComponent>();
                curUnitObjectDic.Add(name, battleList);
            }
        }

        public GameObject GetNewGameObject(String name)
        {
            List<GameObject> curList;
            if (!curGameObjectDic.TryGetValue(name, out curList))
            {
                Debug.LogError(String.Format("没有注册{0}", name));
                return null;
            }
            if (curList.Count == 0)
            {
                return null;
            }
            else
            {
                GameObject cur = curList[0];
                curList.RemoveAt(0);
                return cur;
            }
        }

        public T getNewBattleUnitView<T>(String name) where T : BattleUnitView
        {
            List<BattleUnitView> curList;
            if (!curObjectDic.TryGetValue(name, out curList))
            {
                Debug.LogError(String.Format("没有注册{0}", name));
                return null;
            }
            if (curList.Count == 0)
            {
                return null;
            }
            else
            {
                BattleUnitView cur = curList[0];
                curList.RemoveAt(0);
                return (T)cur;
            }
        }

        public T getNewUnitViewComponent<T>(String name) where T : BaseUnitViewComponent
        {
            List<BaseUnitViewComponent> curList;
            if (!curUnitObjectDic.TryGetValue(name, out curList))
            {
                Debug.LogError(String.Format("没有注册{0}", name));
                return null;
            }
            if (curList.Count == 0)
            {
                return null;
            }
            else
            {
                BaseUnitViewComponent cur = curList[0];
                curList.RemoveAt(0);
                return (T)cur;
            }
        }


        public void PushViewObjectToPool(String name, BattleUnitView unit)
        {
            List<BattleUnitView> curList = this.curObjectDic[name];
            curList.Add(unit);
            //Debug.Log(String.Format("{0}放回到视图对象池，目前长度{1}", name, curList.Count));
        }

        public void PushViewObjectToPool(String name, BaseUnitViewComponent unit)
        {
            List<BaseUnitViewComponent> curList = this.curUnitObjectDic[name];
            curList.Add(unit);
            //Debug.Log(String.Format("{0}放回到视图组件对象池，目前长度{1}", name, curList.Count));
        }

        public void PushGameObjectToPool(String name, GameObject node)
        {
            List<GameObject> curList = this.curGameObjectDic[name];
            node.transform.position = GameManager.Instance.baseBattleView.initTran;
            curList.Add(node);
            //Debug.Log(String.Format("{0}放回到视图游戏对象池，目前长度{1}", name, curList.Count));

        }

        public void ClearGameInfo()
        {
            this.curGameObjectDic.Clear();
            GC.Collect();
        }

        public void Dispose()
        {
            foreach (KeyValuePair<String, List<BattleUnitView>> info in this.curObjectDic)
            {
                for (int i = 0; i < info.Value.Count; i++)
                {
                    info.Value[i].Dispose();
                }
            }
            this.curObjectDic.Clear();
            foreach (KeyValuePair<String, List<BaseUnitViewComponent>> info in this.curUnitObjectDic)
            {
                for (int i = 0; i < info.Value.Count; i++)
                {
                    info.Value[i].Dispose();
                }
            }
            this.curUnitObjectDic.Clear();
            this.curGameObjectDic.Clear();

            gamePool = null;
            GC.Collect();
        }
    }
}
