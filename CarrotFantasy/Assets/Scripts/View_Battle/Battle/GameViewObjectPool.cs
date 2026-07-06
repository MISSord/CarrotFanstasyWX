using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class GameViewObjectPool
    {
        private static GameViewObjectPool gamePool;
        private readonly Dictionary<String, Stack<BattleUnitView>> curObjectDic = new Dictionary<String, Stack<BattleUnitView>>();
        private readonly Dictionary<String, Stack<BaseUnitViewComponent>> curUnitObjectDic = new Dictionary<string, Stack<BaseUnitViewComponent>>();
        private readonly Dictionary<String, Stack<GameObject>> curGameObjectDic = new Dictionary<string, Stack<GameObject>>();

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
                curObjectDic.Add(name, new Stack<BattleUnitView>());
            }
        }

        public void RegisterGameObject(String name)
        {
            if (!curGameObjectDic.ContainsKey(name))
            {
                curGameObjectDic.Add(name, new Stack<GameObject>());
            }
        }

        public void RegisterUnitViewComponent(String name)
        {
            if (!curUnitObjectDic.ContainsKey(name))
            {
                curUnitObjectDic.Add(name, new Stack<BaseUnitViewComponent>());
            }
        }

        public GameObject GetNewGameObject(String name)
        {
            Stack<GameObject> curStack;
            if (!curGameObjectDic.TryGetValue(name, out curStack) || curStack.Count == 0)
            {
                if (!curGameObjectDic.ContainsKey(name))
                {
                    Debug.LogError(String.Format("没有注册{0}", name));
                }
                return null;
            }

            return curStack.Pop();
        }

        public T getNewBattleUnitView<T>(String name) where T : BattleUnitView
        {
            Stack<BattleUnitView> curStack;
            if (!curObjectDic.TryGetValue(name, out curStack) || curStack.Count == 0)
            {
                if (!curObjectDic.ContainsKey(name))
                {
                    Debug.LogError(String.Format("没有注册{0}", name));
                }
                return null;
            }

            return (T)curStack.Pop();
        }

        public T getNewUnitViewComponent<T>(String name) where T : BaseUnitViewComponent
        {
            Stack<BaseUnitViewComponent> curStack;
            if (!curUnitObjectDic.TryGetValue(name, out curStack) || curStack.Count == 0)
            {
                if (!curUnitObjectDic.ContainsKey(name))
                {
                    Debug.LogError(String.Format("没有注册{0}", name));
                }
                return null;
            }

            return (T)curStack.Pop();
        }

        public void PushViewObjectToPool(String name, BattleUnitView unit)
        {
            Stack<BattleUnitView> curStack = this.curObjectDic[name];
            curStack.Push(unit);
        }

        public void PushViewObjectToPool(String name, BaseUnitViewComponent unit)
        {
            Stack<BaseUnitViewComponent> curStack = this.curUnitObjectDic[name];
            curStack.Push(unit);
        }

        public void PushGameObjectToPool(String name, GameObject node)
        {
            if (node == null)
            {
                return;
            }

            if (!this.curGameObjectDic.ContainsKey(name))
            {
                this.RegisterGameObject(name);
            }

            Stack<GameObject> curStack = this.curGameObjectDic[name];
            node.transform.localPosition = BattleView_base.OffscreenPoolPosition;
            curStack.Push(node);
        }

        public void PurgeLegacyNumericPoolKeys()
        {
            List<string> legacyKeys = null;
            foreach (KeyValuePair<string, Stack<GameObject>> kv in this.curGameObjectDic)
            {
                if (!FightViewGameObjectPoolKeys.IsLegacyNumericKey(kv.Key))
                {
                    continue;
                }

                if (legacyKeys == null)
                {
                    legacyKeys = new List<string>();
                }

                legacyKeys.Add(kv.Key);
            }

            if (legacyKeys == null)
            {
                return;
            }

            for (int i = 0; i < legacyKeys.Count; i++)
            {
                string key = legacyKeys[i];
                Stack<GameObject> stack;
                if (!this.curGameObjectDic.TryGetValue(key, out stack))
                {
                    continue;
                }

                while (stack.Count > 0)
                {
                    GameObject go = stack.Pop();
                    if (go != null)
                    {
                        GameObject.Destroy(go);
                    }
                }

                this.curGameObjectDic.Remove(key);
            }
        }

        /// <summary>同关重开：仅清理 legacy 池 key，保留注册与已回池实例供复用。</summary>
        public void PrepareForReplay()
        {
            this.PurgeLegacyNumericPoolKeys();
        }

        public void ClearGameInfo()
        {
            this.PurgeLegacyNumericPoolKeys();
            this.DestroyAndClearAllPooledGameObjects();
            this.DisposeAndClearAllPooledUnitViews();
            BattleViewEffectHelper.ResetTemplates();
        }

        void DisposeAndClearAllPooledUnitViews()
        {
            foreach (KeyValuePair<string, Stack<BattleUnitView>> info in this.curObjectDic)
            {
                Stack<BattleUnitView> stack = info.Value;
                while (stack.Count > 0)
                {
                    BattleUnitView view = stack.Pop();
                    if (view != null)
                    {
                        view.Dispose();
                    }
                }
            }

            foreach (KeyValuePair<string, Stack<BaseUnitViewComponent>> info in this.curUnitObjectDic)
            {
                Stack<BaseUnitViewComponent> stack = info.Value;
                while (stack.Count > 0)
                {
                    BaseUnitViewComponent component = stack.Pop();
                    if (component != null)
                    {
                        component.Dispose();
                    }
                }
            }
        }

        private void DestroyAndClearAllPooledGameObjects()
        {
            foreach (KeyValuePair<String, Stack<GameObject>> kv in this.curGameObjectDic)
            {
                Stack<GameObject> stack = kv.Value;
                while (stack.Count > 0)
                {
                    GameObject go = stack.Pop();
                    if (go != null)
                    {
                        UnityEngine.Object.Destroy(go);
                    }
                }
            }
            this.curGameObjectDic.Clear();
        }

        public void Dispose()
        {
            foreach (KeyValuePair<String, Stack<BattleUnitView>> info in this.curObjectDic)
            {
                while (info.Value.Count > 0)
                {
                    info.Value.Pop().Dispose();
                }
            }
            this.curObjectDic.Clear();

            foreach (KeyValuePair<String, Stack<BaseUnitViewComponent>> info in this.curUnitObjectDic)
            {
                while (info.Value.Count > 0)
                {
                    info.Value.Pop().Dispose();
                }
            }
            this.curUnitObjectDic.Clear();

            this.DestroyAndClearAllPooledGameObjects();

            BattleViewEffectHelper.ResetTemplates();
            gamePool = null;
        }
    }
}
