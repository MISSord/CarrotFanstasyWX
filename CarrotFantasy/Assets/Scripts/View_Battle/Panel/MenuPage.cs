using UnityEngine;
using UnityEngine.UI;


namespace CarrotFantasy
{
    /// <summary>
    /// 菜单页面
    /// </summary>
    public class MenuPage : BaseView
    {
        private Button btnGoOn;
        private Button btnReplay;
        private Button btnChooseLevel;
        private bool isLoaded;

        public MenuPage(Transform node)
        {
            this.transform = node;
        }

        public override void InitData()
        {
            viewName = "MenuPage";
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
            if (this.transform != null)
            {
                this.transform.gameObject.SetActive(true);
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
            this.btnGoOn = this.transform.Find("btn_go_on").GetComponent<Button>();
            this.btnReplay = this.transform.Find("btn_replay").GetComponent<Button>();
            this.btnChooseLevel = this.transform.Find("btn_choose_level").GetComponent<Button>();
            this.btnGoOn.onClick.AddListener(this.BtnEvenGoOn);
            this.btnReplay.onClick.AddListener(this.BtnEvenReplay);
            this.btnChooseLevel.onClick.AddListener(this.BtnEvenChooseOtherLevel);
        }

        protected override void ReleaseCallBack()
        {
            this.btnGoOn?.onClick.RemoveAllListeners();
            this.btnReplay?.onClick.RemoveAllListeners();
            this.btnChooseLevel?.onClick.RemoveAllListeners();
            isLoaded = false;
        }

        public void BtnEvenGoOn()
        {
            UIServer.Instance.PlayButtonEffect();
            this.transform.gameObject.SetActive(false);
            BattleManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.GO_ON_GAME);
        }

        public void BtnEvenReplay()
        {
            UIServer.Instance.PlayButtonEffect();
            this.transform.gameObject.SetActive(false);
            BattleManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.REPLAY_THE_GAME);
        }

        public void BtnEvenChooseOtherLevel()
        {
            UIServer.Instance.PlayButtonEffect();
            this.transform.gameObject.SetActive(false);
            BusinessProvision.Instance.eventDispatcher.DispatchEvent(CommonEventType.RETURN_TO_MAIN_SCENE);
        }
    }
}

