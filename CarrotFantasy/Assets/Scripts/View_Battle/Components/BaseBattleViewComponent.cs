using System;

namespace CarrotFantasy
{
    public abstract class BaseBattleViewComponent
    {
        public BattleView_base battleView;
        public BaseBattle battle;
        public String componentType { get; protected set; }
        public EventDispatcher eventDispatcher;

        public BaseBattleViewComponent(BattleView_base battleView)
        {
            this.battleView = battleView;
            this.battle = battleView.battle;
            this.eventDispatcher = this.battle.eventDispatcher;
        }

        public abstract void Init();
        public virtual void Start() { } //开始游戏调用
        public virtual void OnTick(float time) { }
        public virtual void ClearGameInfo() { } //重新开始游戏前调用
        public virtual void Dispose() { }
    }
}
