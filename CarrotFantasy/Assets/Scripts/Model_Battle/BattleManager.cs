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
            UIServer.Instance.audioManager.PlayMusic("AudioClips/NormalMordel/BGMusic");
            if (BattleParamServer.Instance.isPVE == true)
            {
                this.baseBattle = new PveBattle();
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
        }

        private void RemoveListener()
        {
            this.baseBattle.eventDispatcher.RemoveListener(BattleEvent.REPLAY_THE_GAME, this.RestartGame);
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

            this.baseBattleView.Dispose();
            this.baseBattle.Dispose();

            this.baseBattleView = null;
            this.baseBattle = null;

            UIServer.Instance.ShowLoadingPanel();
        }
    }
}
