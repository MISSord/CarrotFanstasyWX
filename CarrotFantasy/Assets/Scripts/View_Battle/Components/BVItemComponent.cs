using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BVItemComponent : BaseBattleViewComponent
    {
        public Dictionary<BattleUnit_Item, BattleUnitView_Item> itemDic = new Dictionary<BattleUnit_Item, BattleUnitView_Item>();
        public BattleItemComponent itemComponent;
        private int _itemBigLevel;
        private GameObject rootGameObject;
        private GameObject _hpBarCanvasTemplate;

        private readonly HashSet<string> _registeredItemPoolKeys = new HashSet<string>();

        public BVItemComponent(BattleView_base battleView) : base(battleView)
        {
            this.itemComponent = (BattleItemComponent)this.battleView.battle.GetComponent(BattleComponentType.ItemComponent);
            BattlePVEDataComponent pveData = BattlePVEDataComponent.GetFrom(this.battleView.battle);
            this._itemBigLevel = pveData != null ? pveData.bigLevel : 1;
            this.componentType = BattleViewComponentType.Item;
        }

        public override void Init()
        {
            this.RemoveListener();
            this.itemDic.Clear();

            BattleViewEffectHelper.EnsureDestroyEffectPoolRegistered();

            BVSceneComponent scene = this.battleView.TryGetComponent(BattleViewComponentType.SCENE) as BVSceneComponent;
            if (scene == null)
            {
                Debug.LogError("[BVItemComponent] BVSceneComponent 未注册。");
                return;
            }

            this.rootGameObject = scene.RegisterGameContainer("ItemContainer");

            if (!BattleViewPrefabPreloader.TryGetTemplate(
                FightViewPrefabAb.FightPartBundle,
                FightViewPrefabAb.MonsterCanvas,
                out this._hpBarCanvasTemplate))
            {
                Debug.LogError("[BVItemComponent] MonsterCanvas 未预加载，物品血条将无法创建。");
            }

            List<BattleUnit_Item> itemList = this.itemComponent.battleItemList;
            for (int i = 0; i <= itemList.Count - 1; i++)
            {
                this.CreateItemView(itemList[i]);
            }
            this.RemoveListener();
            this.AddListener();
        }

        private void AddListener()
        {
            this.eventDispatcher.AddListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.RemoveItemView);
        }

        private void RemoveListener()
        {
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.RemoveItemView);
        }

        static string GetItemPoolKey(int bigLevel, int itemId)
        {
            return string.Format("Item_{0}_{1}", bigLevel, itemId);
        }

        void EnsureItemPoolRegistered(int itemId)
        {
            string poolKey = GetItemPoolKey(this._itemBigLevel, itemId);
            if (this._registeredItemPoolKeys.Add(poolKey))
            {
                GameViewObjectPool.Instance.RegisterGameObject(poolKey);
            }
        }

        private GameObject GetItemPrefabTemplate(int bigLevel, int itemId)
        {
            string bundleName = FightViewPrefabAb.FightPartItemBundle;
            string assetName = FightViewPrefabAb.ItemAssetName(bigLevel, itemId);
            GameObject tpl;
            if (BattleViewPrefabPreloader.TryGetTemplate(bundleName, assetName, out tpl))
            {
                return tpl;
            }

            Debug.LogError($"[BVItemComponent] 道具预制体未预加载: bundle={bundleName}, asset={assetName}");
            return null;
        }

        private GameObject RentItemGameObject(int itemId)
        {
            this.EnsureItemPoolRegistered(itemId);
            string poolKey = GetItemPoolKey(this._itemBigLevel, itemId);
            GameObject itemGo = GameViewObjectPool.Instance.GetNewGameObject(poolKey);
            if (itemGo == null)
            {
                GameObject tpl = GetItemPrefabTemplate(this._itemBigLevel, itemId);
                if (tpl == null)
                {
                    return null;
                }

                itemGo = GameObject.Instantiate(tpl);
            }

            itemGo.transform.SetParent(this.rootGameObject.transform);
            return itemGo;
        }

        private void ReturnItemGameObject(int itemId, GameObject itemGo)
        {
            if (itemGo == null)
            {
                return;
            }

            string poolKey = GetItemPoolKey(this._itemBigLevel, itemId);
            GameViewObjectPool.Instance.PushGameObjectToPool(poolKey, itemGo);
        }

        private void CreateItemView(BattleUnit_Item item)
        {
            if (item == null || this.itemDic.ContainsKey(item))
            {
                return;
            }

            BattleUnitView_Item itemView = GameViewObjectPool.Instance.getNewBattleUnitView<BattleUnitView_Item>(BattleUnitViewType.Item);
            if (itemView == null)
            {
                itemView = new BattleUnitView_Item();
            }

            GameObject itemGo = RentItemGameObject(item.itemId);
            if (itemGo == null)
            {
                return;
            }

            itemView.InitTransform(itemGo.transform);
            itemView.ConfigureHpBarTemplate(this._hpBarCanvasTemplate);
            itemView.LoadInfo(this.battleView, item);
            itemView.Init();
            this.itemDic.Add(item, itemView);
        }

        public override void OnTick(float time)
        {
            foreach (KeyValuePair<BattleUnit_Item, BattleUnitView_Item> info in this.itemDic)
            {
                info.Value.OnTick(time);
            }
        }

        private void RemoveItemView(String type, BattleUnit obj)
        {
            if (!type.Equals(BattleUnitType.ITEM))
            {
                return;
            }

            BattleUnit_Item item = (BattleUnit_Item)obj;
            if (!this.itemDic.TryGetValue(item, out BattleUnitView_Item itemView))
            {
                return;
            }

            int itemId = item.itemId;
            GameObject itemGo = itemView.transform != null ? itemView.transform.gameObject : null;
            itemView.ClearUnitInfo();
            this.ReturnItemGameObject(itemId, itemGo);
            this.itemDic.Remove(item);
            GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Item, itemView);
            BattleViewEffectHelper.PlayDestroyAt(obj);
        }

        public override void ClearGameInfo()
        {
            _registeredItemPoolKeys.Clear();
            _hpBarCanvasTemplate = null;

            foreach (KeyValuePair<BattleUnit_Item, BattleUnitView_Item> info in this.itemDic)
            {
                int itemId = info.Key.itemId;
                GameObject itemGo = info.Value.transform != null ? info.Value.transform.gameObject : null;
                info.Value.ClearUnitInfo();
                this.ReturnItemGameObject(itemId, itemGo);
                GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Item, info.Value);
            }
            this.itemDic.Clear();
            this.RemoveListener();
        }

        public override void Dispose()
        {
            this.ClearGameInfo();
            base.Dispose();
        }
    }
}
