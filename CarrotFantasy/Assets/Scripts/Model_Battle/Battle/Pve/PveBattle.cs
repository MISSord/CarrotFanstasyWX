using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    public class PveBattle : BaseBattle
    {
        public PveBattle() : base()
        {

        }

        public override void Init()
        {
            this.stateMachine = new PveStateMachine(this);
            this.AddComponent(new BattlePVEDataComponent(this));
            this.AddComponent(new BattleSimpleHitTestComponent(this));

            this.AddComponent(new BattlePVEMapComponent(this)); //依赖 data
            this.AddComponent(new BattleItemComponent(this)); //依赖map
            this.AddComponent(new BattleTowerComponent(this)); //依赖map data
            this.AddComponent(new BattlePVEMonsterComponent(this)); //依赖map
            this.AddComponent(new BattleBulletComponent(this)); //依赖tower
            this.AddComponent(new BattleInputComponent(this)); //依赖map tower
            this.AddComponent(new BattleSchedulerComponent(this));

            this.AddListener();
        }

        protected override void AddListener()
        {
            this.eventDispatcher.AddListener(BattleEvent.PAUSE_THE_GAME, this.PauseTheGame);
            this.eventDispatcher.AddListener(BattleEvent.GO_ON_GAME, this.GoOnTheGame);
        }

        protected override void RemoveListener()
        {
            this.eventDispatcher.RemoveListener(BattleEvent.PAUSE_THE_GAME, this.PauseTheGame);
            this.eventDispatcher.RemoveListener(BattleEvent.GO_ON_GAME, this.GoOnTheGame);
        }

        public override void ClearGameInfo()
        {
            base.ClearGameInfo();
            this.RemoveListener();
        }

        public override void InitComponent()
        {
            this.GetComponent(BattleComponentType.DataComponent).Init();
            this.GetComponent(BattleComponentType.HitTestComponent).Init();
            this.GetComponent(BattleComponentType.MapComponent).Init();
            this.GetComponent(BattleComponentType.ItemComponent).Init();
            this.GetComponent(BattleComponentType.TowerComponent).Init();
            this.GetComponent(BattleComponentType.MonsterComponent).Init();
            this.GetComponent(BattleComponentType.BulletComponent).Init();
            this.GetComponent(BattleComponentType.InputComponent).Init();
            this.GetComponent(BattleComponentType.SchedulerComponent).Init();
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
