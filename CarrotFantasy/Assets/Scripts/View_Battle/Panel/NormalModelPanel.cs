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
            this.topPage.BindNode(this.nodeTopPage.transform);

            this.nodeMenuPage = transform.Find("MenuPage").gameObject;
            this.menuPage = new MenuPage(this.nodeMenuPage.transform);
            this.menuPage.BindNode(this.nodeMenuPage.transform);

            this.nodeGameOverPage = transform.Find("GameOverPage").gameObject;
            this.gameOverPage = new GameOverPage(this.nodeGameOverPage.transform);
            this.gameOverPage.BindNode(this.nodeGameOverPage.transform);

            this.nodeGameWinPage = transform.Find("GameWinPage").gameObject;
            this.gameWinPage = new GameWinPage(this.nodeGameWinPage.transform);
            this.gameWinPage.BindNode(this.nodeGameWinPage.transform);

            this.nodeStartUI = transform.Find("StartUI").gameObject;
            this.btnMenuPage = this.transform.Find("node_TopPage/node_btn_container/Btn_Menu").GetComponent<Button>();
            this.AddListener();
        }

        private void InitPages()
        {
            this.topPage.OpenPage();
            this.menuPage.OpenPage();
            this.gameOverPage.OpenPage();
            this.gameWinPage.OpenPage();

            this.nodeTopPage.SetActive(true);
            this.menuPage.ClosePage();
            this.gameOverPage.ClosePage();
            this.gameWinPage.ClosePage();
        }

        private void ShowMenu()
        {
            UIServer.Instance.PlayButtonEffect();
            this.nodeMenuPage.SetActive(true);
            BattleManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.PAUSE_THE_GAME);
        }

        private void AddListener()
        {
            BattleManager.Instance.baseBattle.eventDispatcher.AddListener(BattleEvent.START_GAME, this.ShowStartUI);
            this.btnMenuPage.onClick.AddListener(this.ShowMenu);
        }

        private void RemoveListener()
        {
            BattleManager.Instance.baseBattle.eventDispatcher.RemoveListener(BattleEvent.START_GAME, this.ShowStartUI);
            this.btnMenuPage.onClick.RemoveAllListeners();
        }

        private void ShowStartUI()
        {
            this.InitPages();
            this.nodeStartUI.SetActive(true);
            BattleSchedulerComponent sche = (BattleSchedulerComponent)BattleManager.Instance.baseBattle.GetComponent(BattleComponentType.SchedulerComponent);
            this.schId = sche.DelayExeOnceTimes(() =>
            {
                this.nodeStartUI.SetActive(false);
                UIServer.Instance.audioManager.PlayEffect("AudioClips/NormalMordel/Go");
            }, 3.0f);
            this.schId_startGame = sche.DelayExeMultipleTimes(() =>
            {
                UIServer.Instance.audioManager.PlayEffect("AudioClips/NormalMordel/CountDown");
            }, 1.0f);
            sche.DelayExeOnceTimes(() =>
            {
                sche.SilenceSingleSche(this.schId_startGame);
            }, 3.5f);
        }

        protected override void ReleaseCallBack()
        {
            this.gameWinPage?.Release();
            this.gameOverPage?.Release();
            this.menuPage?.Release();
            this.topPage?.Release();
            this.RemoveListener();
        }
    }
}
