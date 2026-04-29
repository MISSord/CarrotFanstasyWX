using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>
    /// 游戏失败结束页面
    /// </summary>
    public class GameOverPage : BaseView
    {
        private BattleDataComponent dataComponent;

        private Text txtResultShow;
        private Text txtLevelShow;

        private Button btnReplay;
        private Button btnChooseLevel;
        private bool isLoaded;

        public GameOverPage(Transform node)
        {
            this.transform = node;
            this.dataComponent = (BattleDataComponent)BattleManager.Instance.baseBattle.GetComponent(BattleComponentType.DataComponent);
        }

        public override void InitData()
        {
            viewName = "GameOverPage";
            layer = UILayer.Normal;
        }

        public void BindNode(Transform node)
        {
            this.transform = node;
        }

        public void OpenPage()
        {
            if (!isLoaded)
            {
                LoadCallBack();
                isLoaded = true;
            }
        }

        public void ClosePage()
        {
            if (this.transform != null)
            {
                this.transform.gameObject.SetActive(false);
            }
        }

        protected override void LoadCallBack()
        {
            if (dataComponent == null)
            {
                dataComponent = (BattleDataComponent)BattleManager.Instance.baseBattle.GetComponent(BattleComponentType.DataComponent);
            }
            this.txtResultShow = this.transform.Find("txt_result_show").GetComponent<Text>();
            this.txtLevelShow = this.transform.Find("txt_level_show").GetComponent<Text>();

            this.btnReplay = this.transform.Find("btn_replay").GetComponent<Button>();
            this.btnChooseLevel = this.transform.Find("btn_choose_level").GetComponent<Button>();

            this.AddListener();
        }

        private void AddListener()
        {
            this.btnReplay.onClick.AddListener(this.BtnEvenReplay);
            this.btnChooseLevel.onClick.AddListener(this.BtnEvenChooseOtherLevel);

            this.dataComponent.eventDispatcher.AddListener(BattleEvent.SHOW_GAME_OVER_PAGE, this.ShowGameOverPage);
        }

        private void RemoveListener()
        {
            this.btnReplay.onClick.RemoveAllListeners();
            this.btnChooseLevel.onClick.RemoveAllListeners();
            this.dataComponent.eventDispatcher.RemoveListener(BattleEvent.SHOW_GAME_OVER_PAGE, this.ShowGameOverPage);
        }

        public void ShowGameOverPage()
        {
            this.transform.gameObject.SetActive(true);
            UIServer.Instance.audioManager.PlayEffect("AudioClips/NormalMordel/Lose");
            int waves = dataComponent.curWaves;
            this.txtResultShow.text = LanguageUtil.Instance.GetFormatString(1002, (waves / 10).ToString(), (waves % 10).ToString(), dataComponent.totalWaves.ToString());
            this.txtLevelShow.text = LanguageUtil.Instance.GetFormatString(1003, dataComponent.bigLevel.ToString(), dataComponent.level.ToString());
        }

        public void BtnEvenReplay()
        {
            BattleManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.REPLAY_THE_GAME);
        }

        public void BtnEvenChooseOtherLevel()
        {
            BusinessProvision.Instance.eventDispatcher.DispatchEvent(CommonEventType.RETURN_TO_MAIN_SCENE);
        }

        protected override void ReleaseCallBack()
        {
            this.RemoveListener();
            isLoaded = false;
        }
    }
}

