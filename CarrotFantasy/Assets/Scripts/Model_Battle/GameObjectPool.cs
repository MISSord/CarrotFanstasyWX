using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class GameObjectPool
    {
        private static GameObjectPool gamePool;
        private Dictionary<String, List<BattleUnit>> curObjectDic = new Dictionary<String, List<BattleUnit>>();
        private Dictionary<String, List<BaseUnitComponent>> curUnitObjectDic = new Dictionary<string, List<BaseUnitComponent>>();

        public static GameObjectPool Instance
        {
            get
            {
                if (gamePool == null)
                {
                    gamePool = new GameObjectPool();
                    gamePool.Init();
                }
                return gamePool;
            }
        }

        public void Init()
        {
            this.RegisterBattleUnit(BattleUnitType.MONSTER);
            this.RegisterBattleUnit(BattleUnitType.TOWER);
            this.RegisterBattleUnit(BattleUnitType.BULLET);

            this.RegisterUnitComponent(UnitComponentType.TRANSFORM);
            this.RegisterUnitComponent(UnitComponentType.MOVE_MONSTER);
            this.RegisterUnitComponent(UnitComponentType.MOVE_BULLET);
            this.RegisterUnitComponent(UnitComponentType.MOVE_BULLET_ONE);
            this.RegisterUnitComponent(UnitComponentType.BEHIT);
        }

        public void RegisterBattleUnit(String name)
        {
            if (!curObjectDic.ContainsKey(name))
            {
                List<BattleUnit> battleList = new List<BattleUnit>();
                curObjectDic.Add(name, battleList);
            }
        }

        public void RegisterUnitComponent(String name)
        {
            if (!curUnitObjectDic.ContainsKey(name))
            {
                List<BaseUnitComponent> battleList = new List<BaseUnitComponent>();
                curUnitObjectDic.Add(name, battleList);
            }
        }

        public T getNewBattleUnit<T>(String name) where T : BattleUnit
        {
            List<BattleUnit> curList;
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
                BattleUnit cur = curList[0];
                curList.RemoveAt(0);
                //Debug.Log(String.Format("{0}取出对象池，目前长度{1}", name, curList.Count));
                return (T)cur;
            }
        }

        public T getNewUnitComponent<T>(String name) where T : BaseUnitComponent
        {
            List<BaseUnitComponent> curList;
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
                BaseUnitComponent cur = curList[0];
                curList.RemoveAt(0);
                //Debug.Log(String.Format("{0}取出组件对象池，目前长度{1}", name, curList.Count));
                return (T)cur;
            }
        }


        public void PushObjectToPool(String name, BattleUnit unit)
        {
            List<BattleUnit> curList = this.curObjectDic[name];
            curList.Add(unit);
            //Debug.Log(String.Format("{0}放回到对象池，目前长度{1}", name, curList.Count));
        }

        public void PushObjectToPool(String name, BaseUnitComponent unit)
        {
            List<BaseUnitComponent> curList = this.curUnitObjectDic[name];
            curList.Add(unit);
            //Debug.Log(String.Format("{0}放回到组件对象池，目前长度{1}", name, curList.Count));
        }

        public void Dispose()
        {
            foreach (KeyValuePair<String, List<BattleUnit>> info in this.curObjectDic)
            {
                for (int i = 0; i < info.Value.Count; i++)
                {
                    info.Value[i].Dispose();
                }
                info.Value.Clear();
            }
            foreach (KeyValuePair<String, List<BaseUnitComponent>> info in this.curUnitObjectDic)
            {
                for (int i = 0; i < info.Value.Count; i++)
                {
                    info.Value[i].Dispose();
                }
                info.Value.Clear();
            }
        }
    }
}
