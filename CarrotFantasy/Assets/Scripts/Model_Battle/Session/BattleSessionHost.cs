using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>DDOL 战斗会话宿主：编排 Session、Tick、结算；不依赖 MonoBehaviour 生命周期。</summary>
    public sealed class BattleSessionHost
    {
        BattleSession session;
        BattleSceneContext sceneContext;

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

        public void BeginSession(BattleSessionConfig config, BattleSceneContext context)
        {
            if (config == null)
            {
                BattleFlowLog.Abort("BeginSession", "config=null");
                return;
            }

            if (context == null || !context.IsValid)
            {
                BattleFlowLog.Abort("BeginSession", "BattleSceneContext 无效");
                return;
            }

            if (this.session != null)
            {
                BattleFlowLog.Step(
                    "BeginSession 替换旧会话",
                    "oldPhase=" + this.session.Phase);
                this.session.TearDown(destroyViewHierarchy: true);
                this.session = null;
            }

            this.sceneContext = context;

            BattleFlowLog.Step(
                "BeginSession",
                "BattleRoot#" + context.BattleRoot.GetInstanceID() +
                " ViewHost#" + context.ViewHost.GetInstanceID() +
                " level=" + config.Params.BigLevelId + "-" + config.Params.LevelId);

            this.session = new BattleSession(config, context, this);
            this.session.Run();
        }

        public void EndSession(bool clearLaunchParams = true, bool destroyViewHierarchy = true)
        {
            BattleFlowLog.Step(
                "EndSession",
                "destroyViewHierarchy=" + destroyViewHierarchy +
                " phase=" + (this.session != null ? this.session.Phase.ToString() : "null"));

            if (this.session != null)
            {
                this.session.TearDown(destroyViewHierarchy);
                this.session = null;
            }

            this.sceneContext = null;

            if (clearLaunchParams && BattleParamServer.Instance != null)
            {
                BattleParamServer.Instance.ClearPveParams();
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (this.session == null)
            {
                return;
            }

            this.session.Tick(deltaSeconds);
        }

        internal void HandlePveMatchSettled(PveMatchSettlement settlement)
        {
            if (settlement == null)
            {
                return;
            }

            if (BattleParamAccess.CurrentMode == BattlePveMode.Roguelike &&
                RoguelikeRunManager.Instance != null)
            {
                RoguelikeRunManager.Instance.HandlePveMatchSettled(settlement);
                return;
            }

            if (settlement.IsVictory && settlement.VictoryProgress != null && this.baseBattle != null &&
                this.baseBattle.HostBridge != null)
            {
                this.baseBattle.HostBridge.SubmitVictoryMapProgress(settlement.VictoryProgress);
            }

            if (settlement.IsVictory)
            {
                this.ShowGameWin();
            }
            else
            {
                this.ShowGameOver();
            }
        }

        void ShowGameWin()
        {
            if (this.baseBattle != null)
            {
                BattleViewOpener.Open<GameWinView>(this.baseBattle);
            }

            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Perfect");
        }

        void ShowGameOver()
        {
            if (this.baseBattle != null)
            {
                BattleViewOpener.Open<GameOverView>(this.baseBattle);
            }

            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Lose");
        }
    }
}
