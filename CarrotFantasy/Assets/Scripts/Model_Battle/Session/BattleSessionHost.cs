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
        /// 若已有 Session 先 <see cref="DestroySession"/>，再创建新 Session 并同步执行 Run。
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
                this.session.DestroySession();
                this.session = null;
            }

            BattleFlowLog.Step(
                "BeginSession",
                "level=" + launchParams.BigLevelId + "-" + launchParams.LevelId);

            this.session = new BattleSession(launchParams, viewHost);
            this.session.Run();
        }

        /// <summary>离战斗场景：保留 ViewHost 壳，释放 AB 与 Model。</summary>
        public void EndRound()
        {
            if (this.session == null)
            {
                return;
            }

            this.session.EndRound();
            this.session = null;
        }

        /// <summary>完全销毁 Session（换关、联机回收等）。</summary>
        public void DestroySession()
        {
            if (this.session == null)
            {
                return;
            }

            this.session.DestroySession();
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
