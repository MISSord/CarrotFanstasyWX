using UnityEngine;

namespace CarrotFantasy
{
    public class BattleManager : MonoBehaviour //驱动
    {
        private static BattleManager instance;

        private NormalModelPanel panel;
        private MenuView menuView;
        private GameWinView gameWinView;
        private GameOverView gameOverView;

        public static BattleManager Instance
        {
            get
            {
                return instance;
            }
        }

        public BaseBattle baseBattle { get; private set; }
        public BattleView_base baseBattleView { get; private set; }

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
                this.baseBattle = new PveBattle();
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
            InitBattleViews();
        }

        private void InitBattleViews()
        {           
            // 战斗 UI：集中创建与注册（避免单例）
            panel = new NormalModelPanel();
            panel.RegisterData();

            if (menuView == null)
            {
                menuView = new MenuView();
                menuView.RegisterData();
            }
            if (gameWinView == null)
            {
                gameWinView = new GameWinView();
                gameWinView.RegisterData();
            }
            if (gameOverView == null)
            {
                gameOverView = new GameOverView();
                gameOverView.RegisterData();
            }
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
            this.baseBattleView.ClearGameInfo();
            this.baseBattle.ClearGameInfo();

            // 关闭战斗 UI
            this.panel?.Close();
            ViewManager.Instance?.CloseAllOpenViews();

            InitBattleViews();

            this.InitBattle();
            Sche.DelayExeOnceTimes(this.StartGame, 2.0f);
        }

        public void InitBattle()
        {
            this.baseBattle.Init();
            this.baseBattle.InitComponent();
            this.baseBattleView.Init();
            this.baseBattleView.InitComponents();

            this.panel.Open();
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
