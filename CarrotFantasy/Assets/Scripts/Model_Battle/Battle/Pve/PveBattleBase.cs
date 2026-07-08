namespace CarrotFantasy
{
    /// <summary>PVE 战斗公共基类：组件组合由 <see cref="PveBattleComponentSetup"/> 统一注册。</summary>
    public abstract class PveBattleBase : BaseBattle
    {
        bool componentsRegistered;

        protected abstract PveBattleComponentSetup.Layout ComponentLayout { get; }

        public override void RegisterComponents()
        {
            if (this.componentsRegistered)
            {
                return;
            }

            PveBattleComponentSetup.Register(this, this.ComponentLayout);
            this.componentsRegistered = true;
        }

        public override void Init()
        {
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

        public override void ResetForNewRound()
        {
            this.RemoveListener();
            base.ResetForNewRound();
        }

        public override void InitComponent()
        {
            PveBattleComponentSetup.InitAll(this, this.ComponentLayout);
        }
    }
}
