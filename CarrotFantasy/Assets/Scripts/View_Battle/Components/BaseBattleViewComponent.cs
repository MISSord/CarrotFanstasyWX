using System;

namespace CarrotFantasy
{
    /// <summary>
    /// 战斗视图组件生命周期：
    /// Build(<see cref="Init"/>) → 重开 <see cref="ResetRound"/> → 离场景 <see cref="ClearGameInfo"/>。
    /// </summary>
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

        public virtual bool IsBuilt { get; protected set; }

        public abstract void Init();

        /// <summary>同关重开；由 <see cref="BattleView_base.ResetRound"/> 分两阶段调用。</summary>
        public virtual void ResetRound(BattleViewResetPass pass) { }

        public virtual void Start() { }
        public virtual void OnTick(float time) { }
        public virtual void ClearGameInfo() { }
        public virtual void Dispose() { }

        protected void RefreshBattleBindings()
        {
            if (this.battleView != null)
            {
                this.battle = this.battleView.battle;
            }

            if (this.battle != null)
            {
                this.eventDispatcher = this.battle.eventDispatcher;
            }
        }

        protected void RebindBattleListeners(Action removeListener, Action addListener)
        {
            this.RefreshBattleBindings();
            removeListener();
            addListener();
        }
    }
}
