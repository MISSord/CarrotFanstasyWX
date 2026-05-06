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

        private readonly Dictionary<string, GameObject> _itemPrefabTemplates = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, AssetLoadHandle> _itemPrefabHandles = new Dictionary<string, AssetLoadHandle>();
        private GameObject _destroyEffectTemplate;
        private AssetLoadHandle _destroyEffectHandle;

        public BVItemComponent(BattleView_base battleView) : base(battleView)
        {
            this.itemComponent = (BattleItemComponent)this.battleView.battle.GetComponent(BattleComponentType.ItemComponent);
            BattleDataComponent dataComponent = (BattleDataComponent)this.battleView.battle.GetComponent(BattleComponentType.DataComponent);
            this._itemBigLevel = dataComponent.bigLevel;
            this.componentType = BattleViewComponentType.Item;
        }

        public override void Init()
        {
            GameViewObjectPool.Instance.RegisterGameObject(BattleUnitViewType.DestroyEffect);

            BVSceneComponent scene = (BVSceneComponent)this.battleView.GetComponent(BattleViewComponentType.SCENE);
            this.rootGameObject = scene.RegisterGameContainer("ItemContainer");
            List<BattleUnit_Item> itemList = this.itemComponent.battleItemList;
            for (int i = 0; i <= itemList.Count - 1; i++)
            {
                this.CreateItemView(itemList[i]);
            }
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

        private GameObject GetItemPrefabTemplate(int bigLevel, int itemId)
        {
            string bundleName = FightViewPrefabAb.ItemBundleName(bigLevel);
            string assetName = itemId.ToString();
            string cacheKey = bundleName + "/" + assetName;
            if (_itemPrefabTemplates.TryGetValue(cacheKey, out GameObject cached) && cached != null)
            {
                return cached;
            }

            GameObject tpl = GameObjectResourceManager.Instance.LoadPrefabBlocking(bundleName, assetName, out AssetLoadHandle handle);
            if (tpl == null)
            {
                Debug.LogError($"[BVItemComponent] 道具预制体加载失败: bundle={bundleName}, asset={assetName}");
                return null;
            }

            _itemPrefabTemplates[cacheKey] = tpl;
            _itemPrefabHandles[cacheKey] = handle;
            return tpl;
        }

        private void CreateItemView(BattleUnit_Item item)
        {
            BattleUnitView_Item itemView = new BattleUnitView_Item();
            GameObject tpl = GetItemPrefabTemplate(_itemBigLevel, item.itemId);
            if (tpl == null)
            {
                return;
            }

            GameObject itemGo = GameObject.Instantiate(tpl);
            itemGo.transform.SetParent(this.rootGameObject.transform);
            itemView.InitTransform(itemGo.transform);
            itemView.LoadInfo(this.battleView, item);
            itemView.Init();
            this.itemDic.Add(item, itemView);
        }

        private void RemoveItemView(String type, BattleUnit obj)
        {
            if (type.Equals(BattleUnitType.ITEM))
            {
                BattleUnit_Item item = (BattleUnit_Item)obj;
                BattleUnitView_Item itemView = this.itemDic[item];
                GameObject.Destroy(itemView.transform.gameObject);
                itemView.ClearUnitInfo();
                this.itemDic.Remove(item);
                GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Item, itemView);

                //特效
                GameObject sell = GameViewObjectPool.Instance.GetNewGameObject(BattleUnitViewType.DestroyEffect);
                if (sell == null)
                {
                    if (_destroyEffectTemplate == null)
                    {
                        _destroyEffectTemplate = GameObjectResourceManager.Instance.LoadPrefabBlocking(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.DestoryEffect, out _destroyEffectHandle);
                    }

                    sell = _destroyEffectTemplate != null ? GameObject.Instantiate(_destroyEffectTemplate) : null;
                }
                sell.transform.GetComponent<Animator>().enabled = true;
                UnitTransformComponent tran = (UnitTransformComponent)obj.GetComponent(UnitComponentType.TRANSFORM);
                sell.transform.position = new Vector3((float)tran.lastFrameX, (float)tran.lastFrameY, 0);
                Sche.DelayExeOnceTimes(() =>
                {
                    sell.transform.GetComponent<Animator>().enabled = false;
                    GameViewObjectPool.Instance.PushGameObjectToPool(BattleUnitViewType.DestroyEffect, sell);
                }, 0.5f);
            }
        }

        public override void ClearGameInfo()
        {
            foreach (KeyValuePair<string, AssetLoadHandle> kv in _itemPrefabHandles)
            {
                kv.Value.Dispose();
            }

            _itemPrefabHandles.Clear();
            _itemPrefabTemplates.Clear();
            if (_destroyEffectHandle.IsValid)
            {
                _destroyEffectHandle.Dispose();
                _destroyEffectHandle = AssetLoadHandle.Invalid;
            }

            _destroyEffectTemplate = null;
            foreach (KeyValuePair<BattleUnit_Item, BattleUnitView_Item> info in this.itemDic)
            {
                GameObject.Destroy(info.Value.transform.gameObject);
                info.Value.ClearUnitInfo();
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
