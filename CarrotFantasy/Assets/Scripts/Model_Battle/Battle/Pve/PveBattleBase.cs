namespace CarrotFantasy
{
    /// <summary>PVE 战斗公共基类：组件组合由 <see cref="PveBattleComponentSetup"/> 统一注册。</summary>
    public abstract class PveBattleBase : BaseBattle
    {
        protected abstract PveBattleComponentSetup.Layout ComponentLayout { get; }

        public override void Init()
        {
            PveBattleComponentSetup.Register(this, this.ComponentLayout);
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
            PveBattleComponentSetup.InitAll(this, this.ComponentLayout);
        }
    }
}
