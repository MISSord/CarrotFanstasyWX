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

        private int _useEpoch;

        public BVItemComponent(BattleView_base battleView) : base(battleView)
        {
            this.itemComponent = (BattleItemComponent)this.battleView.battle.GetComponent(BattleComponentType.ItemComponent);
            BattlePVEDataComponent pveData = BattlePVEDataComponent.GetFrom(this.battleView.battle);
            this._itemBigLevel = pveData != null ? pveData.bigLevel : 1;
            this.componentType = BattleViewComponentType.Item;
        }

        public override void CollectBoot(AssetBundleBatch batch)
        {
            if (this.itemComponent == null || this.itemComponent.battleItemList == null)
            {
                return;
            }

            for (int i = 0; i < this.itemComponent.battleItemList.Count; i++)
            {
                BattleUnit_Item item = this.itemComponent.battleItemList[i];
                batch.Keep<GameObject>(FightViewPrefabAb.ItemBundleName(this._itemBigLevel), item.itemId.ToString());
            }
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

        private void CreateItemView(BattleUnit_Item item)
        {
            GameObject tpl = this.BundleScope.Get<GameObject>(FightViewPrefabAb.ItemBundleName(this._itemBigLevel), item.itemId.ToString());
            if (tpl == null)
            {
                return;
            }

            BattleUnitView_Item itemView = new BattleUnitView_Item();
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
                itemView.ClearUnitInfo();
                GameObject.Destroy(itemView.transform.gameObject);
                this.itemDic.Remove(item);
                GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Item, itemView);

                //特效
                GameObject sell = GameViewObjectPool.Instance.GetNewGameObject(BattleUnitViewType.DestroyEffect);
                int epoch = this._useEpoch;
                if (sell == null)
                {
                    this.BundleScope.Use<GameObject>(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.DestoryEffect, tpl =>
                    {
                        if (epoch != this._useEpoch || tpl == null)
                        {
                            return;
                        }

                        this.PlayDestroyEffect(GameObject.Instantiate(tpl), obj);
                    });
                }
                else
                {
                    this.PlayDestroyEffect(sell, obj);
                }
            }
        }

        private void PlayDestroyEffect(GameObject sell, BattleUnit obj)
        {
            sell.transform.GetComponent<Animator>().enabled = true;
            UnitTransformComponent tran = (UnitTransformComponent)obj.GetComponent(UnitComponentType.TRANSFORM);
            sell.transform.position = new Vector3((float)tran.lastFrameX, (float)tran.lastFrameY, 0);
            Sche.DelayExeOnceTimes(() =>
            {
                if (sell == null)
                {
                    return;
                }

                sell.transform.GetComponent<Animator>().enabled = false;
                GameViewObjectPool.Instance.PushGameObjectToPool(BattleUnitViewType.DestroyEffect, sell);
            }, 0.5f);
        }

        public override void ClearGameInfo()
        {
            this._useEpoch++;
            foreach (KeyValuePair<BattleUnit_Item, BattleUnitView_Item> info in this.itemDic)
            {
                info.Value.ClearUnitInfo();
                GameObject.Destroy(info.Value.transform.gameObject);
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
