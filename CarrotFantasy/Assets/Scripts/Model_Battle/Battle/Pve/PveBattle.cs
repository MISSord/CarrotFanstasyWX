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
            this.AddComponent(new BattleDataComponent(this));
            this.AddComponent(new BattleSimpleHitTestComponent(this));
            this.AddComponent(new BattleMapComponent(this)); //依赖 data
            this.AddComponent(new BattleItemComponent(this)); //依赖map
            this.AddComponent(new BattleTowerComponent(this)); //依赖map data
            this.AddComponent(new BattleMonsterComponent(this)); //依赖map
            this.AddComponent(new BattleBulletComponent(this)); //依赖tower
            this.AddComponent(new BattleInputComponent(this)); //依赖map tower
            this.AddComponent(new BattleSchedulerComponent(this));

            this.AddListener();
        }

        protected override void AddListener()
        {
            this.eventDispatcher.AddListener(BattleEvent.PAUSE_THE_GAME, this.pauseTheGame);
            this.eventDispatcher.AddListener(BattleEvent.GO_ON_GAME, this.goOnTheGame);
        }

        protected override void RemoveListener()
        {
            this.eventDispatcher.RemoveListener(BattleEvent.PAUSE_THE_GAME, this.pauseTheGame);
            this.eventDispatcher.RemoveListener(BattleEvent.GO_ON_GAME, this.goOnTheGame);
        }

        public override void ClearGameInfo()
        {
            base.ClearGameInfo();
            this.RemoveListener();
        }

        public override void initComponent()
        {
            foreach (KeyValuePair<String, BaseBattleComponent> info in this.componentDic)
            {
                info.Value.Init();
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
