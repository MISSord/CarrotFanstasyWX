using UnityEngine;

namespace CarrotFantasy
{
    public class BattleManager : MonoBehaviour //驱动
    {
        private static BattleManager instance;
        public static BattleManager Instance
        {
            get
            {
                return instance;
            }
        }
        public BaseBattle baseBattle { get; private set; }
        public BattleView_base baseBattleView { get; private set; }

        /// <summary>全局 Sche 上预约的延时开局任务；重开或 Dispose 前需静默，避免多条回调叠加。</summary>
        private int _pendingStartGameSchId;

        /// <summary>本局战斗随机种子；重开复用，保证与首局相同序列。</summary>
        int _battleRandomSeed;

        private void Awake()
        {
            instance = this;
        }

        public void Init()
        {
            AudioClipPreloader.RunBattleDefaults(null);
            AudioManager.Instance.PlayMusicByResources("AudioClips/NormalMordel/BGMusic");
            if (BattleParamServer.Instance.isPVE == true)
            {
                if (BattleParamServer.Instance.useHitTestBenchmarkBattle)
                {
                    this.baseBattle = new HitTestBenchmarkBattle();
                }
                else if (BattleParamServer.Instance.useRoguelikePveBattleMode)
                {
                    this.baseBattle = new RoguelikePveBattle();
                }
                else if (BattleParamServer.Instance.useSurvivalPveBattleMode)
                {
                    this.baseBattle = new SurvivalPveBattle();
                }
                else
                {
                    this.baseBattle = new PveBattle();
                }

                this.baseBattle.SetHostBridge(new UnityBattleHostBridge());
                this.baseBattleView = new PveBattleView(this.baseBattle);
            }
            else
            {
                //this.baseBattle = new PvpBattle();
                //this.baseBattleView = new PveBattleView(this.baseBattle);
            }
            this.baseBattleView.rootGameObject = this.transform.gameObject;
            this.AddLitener();
        }

        private void AddLitener()
        {
            this.baseBattle.eventDispatcher.AddListener(BattleEvent.REPLAY_THE_GAME, this.RestartGame);
            this.baseBattle.eventDispatcher.AddListener<PveMatchSettlement>(BattleCoreEvent.PVE_MATCH_SETTLED, this.OnPveMatchSettled);
        }

        private void RemoveListener()
        {
            if (this.baseBattle == null) return;
            this.baseBattle.eventDispatcher.RemoveListener(BattleEvent.REPLAY_THE_GAME, this.RestartGame);
            this.baseBattle.eventDispatcher.RemoveListener<PveMatchSettlement>(BattleCoreEvent.PVE_MATCH_SETTLED, this.OnPveMatchSettled);
        }

        private void OnPveMatchSettled(PveMatchSettlement settlement)
        {
            if (settlement == null) return;

            if (BattleParamServer.Instance != null &&
                BattleParamServer.Instance.useRoguelikePveBattleMode &&
                RoguelikeRunManager.Instance != null)
            {
                RoguelikeRunManager.Instance.HandlePveMatchSettled(settlement);
                return;
            }

            if (settlement.IsVictory && settlement.VictoryProgress != null && this.baseBattle.HostBridge != null)
            {
                this.baseBattle.HostBridge.SubmitVictoryMapProgress(settlement.VictoryProgress);
            }
            if (settlement.IsVictory)
            {
                ShowGameWin();
            }
            else
            {
                ShowGameOver();
            }
        }

        private void RestartGame()
        {
            UIServer.Instance.ShowLoadingPanel();

            // 关闭战斗 UI
            ViewManager.Instance?.CloseAllOpenViews();

            this.baseBattleView.ClearGameInfo();
            this.baseBattle.ClearGameInfo();
            // _battleRandomSeed 不变 → InitBattle 内 ResetRandomSession，序列与首局一致

            this.InitBattle();
            this.ScheduleDelayedStartGame(2.0f);
        }

        private void RunDelayedStartGame()
        {
            this._pendingStartGameSchId = 0;
            this.StartGame();
        }

        private void CancelPendingDelayedStartGame()
        {
            if (this._pendingStartGameSchId != 0)
            {
                Sche.SilenceSingleSche(this._pendingStartGameSchId);
                this._pendingStartGameSchId = 0;
            }
        }

        public void ScheduleDelayedStartGame(float delaySeconds)
        {
            this.CancelPendingDelayedStartGame();
            this._pendingStartGameSchId = Sche.DelayExeOnceTimes(this.RunDelayedStartGame, delaySeconds);
        }

        public void InitBattle()
        {
            ApplyBattleRandomSession();
            this.baseBattle.Init();
            this.baseBattle.InitComponent();

            this.baseBattleView.Init();
            this.baseBattleView.InitComponents();

            ViewManager.Instance.OpenView<NormalModelPanel>();
        }

        /// <summary>
        /// 设置本局种子（场景入口可经 <see cref="BattleLaunchParams.BattleRandomSeed"/> 传入）。
        /// 为 0 且尚未有过种子时，按当前关卡合成。
        /// </summary>
        public void SetBattleRandomSeed (int seed)
        {
            _battleRandomSeed = seed == 0 ? 1 : seed;
        }

        void ApplyBattleRandomSession ()
        {
            if (_battleRandomSeed == 0) {
                _battleRandomSeed = ResolveDefaultBattleRandomSeed();
            }

            if (this.baseBattle.RandomSession == null ||
                this.baseBattle.RandomSession.RootSeed != _battleRandomSeed) {
                this.baseBattle.SetRandomSession(
                    new DeterministicRandomSession(_battleRandomSeed)
                );
            }
            else {
                this.baseBattle.ResetRandomSession();
            }
        }

        static int ResolveDefaultBattleRandomSeed ()
        {
            if (BattleParamServer.Instance == null) {
                return 1;
            }

            return DeterministicSeed.ForClassicLevel(
                BattleParamServer.Instance.curBigLevel,
                BattleParamServer.Instance.curLevel
            );
        }

        public void StartGame()
        {
            this.baseBattle.StartGame();
            this.baseBattleView.StartGame();
            UIServer.Instance.FadeLoadingPanel();
        }

        public void Update()
        {
            this.baseBattle.Tick(new Fix64(Time.deltaTime));
            this.baseBattleView.OnTick(Time.deltaTime);
        }

        public void Dispose()
        {
            this.CancelPendingDelayedStartGame();
            this.RemoveListener();

            if (this.baseBattleView != null)
                this.baseBattleView.Dispose();

            if (this.baseBattle != null)
                this.baseBattle.Dispose();

            this.baseBattleView = null;
            this.baseBattle = null;

            UIServer.Instance.ShowLoadingPanel();
        }

        private void ShowGameWin()
        {
            ViewManager.Instance.OpenView<GameWinView>();
            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Perfect");
        }

        private void ShowGameOver()
        {
            ViewManager.Instance.OpenView<GameOverView>();
            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Lose");
        }
    }
}
