using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class NormalModelPanel : BaseView
    {
        private static NormalModelPanel _instance;
        public static NormalModelPanel Instance => _instance ?? (_instance = new NormalModelPanel());

        private NormalModelPanel() { }

        private GameObject nodeTopPage;
        private GameObject nodeMenuPage;
        private GameObject nodeGameOverPage;
        private GameObject nodeGameWinPage;
        private GameObject nodeStartUI;
        private TopPage topPage;
        private GameWinPage gameWinPage;
        private MenuPage menuPage;
        private GameOverPage gameOverPage;
        private Button btnMenuPage;
        private int schId;
        private int schId_startGame;

        public override void InitData()
        {
            viewName = "NormalModelPanel";
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.ViewRootPrefab, "NormalModelPanel");
        }

        protected override void LoadCallBack()
        {
            this.nodeTopPage = transform.Find("node_TopPage").gameObject;
            this.topPage = new TopPage(this.nodeTopPage.transform);

            this.nodeMenuPage = transform.Find("MenuPage").gameObject;
            this.menuPage = new MenuPage(this.nodeMenuPage.transform);

            this.nodeGameOverPage = transform.Find("GameOverPage").gameObject;
            this.gameOverPage = new GameOverPage(this.nodeGameOverPage.transform);

            this.nodeGameWinPage = transform.Find("GameWinPage").gameObject;
            this.gameWinPage = new GameWinPage(this.nodeGameWinPage.transform);

            this.nodeStartUI = transform.Find("StartUI").gameObject;
            this.btnMenuPage = this.transform.Find("node_TopPage/node_btn_container/Btn_Menu").GetComponent<Button>();
            this.AddListener();
        }

        private void initPages()
        {
            this.topPage.Init();
            this.menuPage.Init();
            this.gameOverPage.Init();
            this.gameWinPage.Init();

            this.nodeTopPage.SetActive(true);
            this.nodeMenuPage.SetActive(false);
            this.nodeGameOverPage.SetActive(false);
            this.nodeGameWinPage.SetActive(false);
        }

        private void showMenu()
        {
            UIServer.Instance.playButtonEffect();
            this.nodeMenuPage.SetActive(true);
            GameManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.PAUSE_THE_GAME);
        }

        private void AddListener()
        {
            GameManager.Instance.baseBattle.eventDispatcher.AddListener(BattleEvent.START_GAME, this.showStartUI);
            this.btnMenuPage.onClick.AddListener(this.showMenu);
        }

        private void RemoveListener()
        {
            GameManager.Instance.baseBattle.eventDispatcher.RemoveListener(BattleEvent.START_GAME, this.showStartUI);
            this.btnMenuPage.onClick.RemoveAllListeners();
        }

        private void showStartUI()
        {
            this.initPages();
            this.nodeStartUI.SetActive(true);
            BattleSchedulerComponent sche = (BattleSchedulerComponent)GameManager.Instance.baseBattle.GetComponent(BattleComponentType.SchedulerComponent);
            this.schId = sche.delayExeOnceTimes(() =>
            {
                this.nodeStartUI.SetActive(false);
                UIServer.Instance.audioManager.playEffect("AudioClips/NormalMordel/Go");
            }, 3.0f);
            this.schId_startGame = sche.delayExeMultipleTimes(() =>
            {
                UIServer.Instance.audioManager.playEffect("AudioClips/NormalMordel/CountDown");
            }, 1.0f);
            sche.delayExeOnceTimes(() =>
            {
                sche.silenceSingleSche(this.schId_startGame);
            }, 3.5f);
        }

        protected override void ReleaseCallBack()
        {
            this.gameWinPage.Dispose();
            this.gameOverPage.Dispose();
            this.topPage.Dispose();
            this.RemoveListener();
        }
    }
}
