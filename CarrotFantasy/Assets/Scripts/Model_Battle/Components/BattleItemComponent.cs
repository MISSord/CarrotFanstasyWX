using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    public class BattleItemComponent : BaseBattleComponent
    {
        public List<BattleUnit_Item> battleItemList = new List<BattleUnit_Item>();
        private BattleMapComponent mapComponent;
        private ItemConfigReader itemConfigReader;

        public List<BattleUnit_Item> deadItemList = new List<BattleUnit_Item>();

        public BattleItemComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.itemConfigReader = new ItemConfigReader();
            this.itemConfigReader.Init();
            this.componentType = BattleComponentType.ItemComponent;
        }

        public override void Init()
        {
            this.mapComponent = (BattleMapComponent)(this.baseBattle.GetComponent(BattleComponentType.MapComponent));
            BattleDataComponent dataOne = (BattleDataComponent)this.baseBattle.GetComponent(BattleComponentType.DataComponent);
            BattleMapGrid[,] gridsList = mapComponent.gridsList;

            for (int x = 0; x <= dataOne.xColumn - 1; x++)
            {
                for (int y = 0; y <= dataOne.yRow - 1; y++)
                {
                    if (gridsList[x, y].state.hasItem)
                    {
                        this.CreateItem(gridsList[x, y]);
                    }
                }
            }
        }

        //创建物品
        private void CreateItem(BattleMapGrid mapGrid)
        {
            BattleUnit_Item item = new BattleUnit_Item(this.baseBattle);
            item.eventDipatcher.AddListener<BattleUnit_Item>(BattleEvent.ITEM_DIED, this.AddDeadList);
            int itemId = this.mapComponent.levelInfo.bigLevelID * 100 + mapGrid.state.itemID;
            item.LoadInfo(this.baseBattle.GetUid(), this.itemConfigReader.getSingleItemConfig(itemId), this.GetPosition(mapGrid), mapGrid.state.itemID);
            item.Init();
            item.InitComponents();
            item.LoadInfo1();
            this.battleItemList.Add(item);
            this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, BattleUnitType.ITEM, item);
        }

        private Fix64Vector2 GetPosition(BattleMapGrid mapGrid)
        {
            Fix64Vector2 tran = new Fix64Vector2(mapGrid.realX, mapGrid.realY);
            if (mapGrid.state.itemID <= 2)
            {
                tran += new Fix64Vector2(BattleConfig.MAP_RATIO, -BattleConfig.MAP_RATIO) / Fix64.Two;
            }
            else if (mapGrid.state.itemID <= 4)
            {
                tran += new Fix64Vector2(BattleConfig.MAP_RATIO, 0) / Fix64.Two;
            }
            return tran;
        }

        private void AddDeadList(BattleUnit_Item item)
        {
            this.deadItemList.Add(item);
        }

        public override void OnTick(Fix64 time)
        {
            base.OnTick(time);
            this.UpdateItemState();
        }

        public void UpdateItemState()
        {
            if (this.deadItemList.Count != 0)
            {
                for (int i = 0; i < this.deadItemList.Count; i++)
                {
                    this.CheckSingleItemState(this.deadItemList[i]);
                }
                this.deadItemList.Clear();
            }
        }

        public void CheckSingleItemState(BattleUnit_Item item)
        {
            if (item.IsDead() == true)
            {
                this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, BattleUnitType.ITEM, item);
                this.baseBattle.eventDispatcher.DispatchEvent<int>(BattleEvent.COIN_CHANGE, (int)item.birthParam["money"]);
                //先从其他组件上除去，再从视图移除，最后再自己移除，确保顺序
                item.ClearInfo();
                this.battleItemList.Remove(item);
            }
        }

        public override void ClearInfo()
        {
            base.ClearInfo();
            this.deadItemList.Clear();
            for (int i = 0; i <= this.battleItemList.Count - 1; i++)
            {
                this.battleItemList[i].eventDipatcher.RemoveListener<BattleUnit_Item>(BattleEvent.ITEM_DIED, this.AddDeadList);
                this.battleItemList[i].Dispose();
            }
            this.battleItemList.Clear();
        }

        public override void Dispose()
        {
            this.ClearInfo();
            base.Dispose();
        }
    }
}
