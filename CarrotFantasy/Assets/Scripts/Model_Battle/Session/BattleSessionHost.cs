namespace CarrotFantasy
{
    /// <summary>
    /// DDOL 战斗会话宿主：持有 Session、转发 Tick；不依赖 MonoBehaviour 生命周期。
    /// <para>离关 teardown 单链路：SceneServer.LoadScene/TeardownCurrentScene → BattleScene.Dispose → <see cref="Shutdown"/>。</para>
    /// <para>同关重开：<see cref="BattleSession.Restart"/>（仅关叠层 UI，不 Shutdown）。</para>
    /// <para><see cref="BeginSession"/> 内若残留 Session 会先 Shutdown（换关兜底，正常切场景已在 Dispose 清掉）。</para>
    /// </summary>
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

        /// <summary>离关 teardown；由 <see cref="BattleScene.Dispose"/> 或 <see cref="BeginSession"/> 换关兜底调用。</summary>
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
