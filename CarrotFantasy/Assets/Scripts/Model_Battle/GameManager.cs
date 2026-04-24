using UnityEngine;

namespace CarrotFantasy
{
    public class GameManager : MonoBehaviour //驱动
    {
        private static GameManager instance;

        private NormalModelPanel panel;

        public static GameManager Instance
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

        public void init()
        {
            UIServer.Instance.audioManager.playMusic("AudioClips/NormalMordel/BGMusic");
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
            this.addLitener();
        }

        private void addLitener()
        {
            this.baseBattle.eventDispatcher.AddListener(BattleEvent.REPLAY_THE_GAME, this.restartGame);
        }

        private void RemoveListener()
        {
            this.baseBattle.eventDispatcher.RemoveListener(BattleEvent.REPLAY_THE_GAME, this.restartGame);
        }

        private void restartGame()
        {
            UIServer.Instance.showLoadingPanel();
            this.baseBattleView.clearGameInfo();
            this.baseBattle.clearGameInfo();


            if (this.panel != null)
            {
                this.panel.Finish();
                this.panel = null;
            }

            this.initBattle();
            Sche.delayExeOnceTimes(this.startGame, 2.0f);
        }

        public void initBattle()
        {
            this.baseBattle.init();
            this.baseBattle.initComponent();
            this.baseBattleView.init();
            this.baseBattleView.initComponents();

            this.panel = new NormalModelPanel(null);
            ServerProvision.panelServer.ShowPanel(this.panel);
        }

        public void startGame()
        {
            this.baseBattle.startGame();
            this.baseBattleView.startGame();
            UIServer.Instance.fadeLoadingPanel();
        }

        public void Update()
        {
            this.baseBattle.tick(new Fix64(Time.deltaTime));
            this.baseBattleView.onTick(Time.deltaTime);
        }

        public void Dispose()
        {
            this.RemoveListener();
            this.baseBattleView.Dispose();
            this.baseBattle.Dispose();

            this.baseBattleView = null;
            this.baseBattle = null;

            UIServer.Instance.showLoadingPanel();
        }
    }
}
