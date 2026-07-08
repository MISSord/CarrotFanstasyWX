namespace CarrotFantasy
{
    /// <summary>DDOL 战斗会话宿主：持有 Session、转发 Tick；不依赖 MonoBehaviour 生命周期。</summary>
    public sealed class BattleSessionHost
    {
        BattleSession session;

        public BaseBattle baseBattle
        {
            get { return this.session != null ? this.session.Battle : null; }
        }

        public BattleView_base baseBattleView
        {
            get { return this.session != null ? this.session.View : null; }
        }

        public BattleSessionPhase SessionPhase
        {
            get { return this.session != null ? this.session.Phase : BattleSessionPhase.None; }
        }

        public bool HasActiveSession
        {
            get { return this.session != null && this.session.Phase != BattleSessionPhase.Disposed; }
        }

        /// <summary>
        /// 由 <see cref="BattleScene.TryBeginSession"/> 调用。
        /// 若已有 Session 先 <see cref="Shutdown"/>，再创建新 Session 并同步执行 Run。
        /// </summary>
        public void BeginSession(PveModelBattleParams launchParams, BattleViewHost viewHost)
        {
            if (launchParams == null)
            {
                BattleFlowLog.Abort("BeginSession", "launchParams=null");
                return;
            }

            if (viewHost == null || !viewHost.IsReady)
            {
                BattleFlowLog.Abort("BeginSession", "BattleViewHost 无效");
                return;
            }

            if (this.session != null)
            {
                this.Shutdown();
            }

            BattleFlowLog.Step(
                "BeginSession",
                "level=" + launchParams.BigLevelId + "-" + launchParams.LevelId);

            this.session = new BattleSession(launchParams, viewHost);
            this.session.Run();
        }

        /// <summary>离关统一 teardown，与场景切换 / 换关 / 联机回收共用。</summary>
        public void Shutdown()
        {
            if (this.session == null)
            {
                return;
            }

            this.session.Shutdown();
            this.session = null;
        }

        /// <summary>由 <see cref="GameMain.Update"/> 每帧驱动；仅 Running 阶段推进逻辑帧与视图 Tick。</summary>
        public void Tick(float deltaSeconds)
        {
            if (this.session == null)
            {
                return;
            }

            this.session.Tick(deltaSeconds);
        }
    }
}
