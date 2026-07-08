using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>战斗内暂停菜单（独立 BaseView）。</summary>
    public class MenuView : BattleBoundView
    {
        public override void InitData()
        {
            viewName = "MenuView";
            layer = UILayer.Hight;
            SetUILoadInfo(0, UiViewAbPaths.NormalMordelPrefab, "MenuPage");
        }

        protected override void LoadCallBack()
        {
            this.BindMenuButtons();
            this.NotifyBattleUiReady();
        }

        protected override void RefreshBattleBinding()
        {
            this.BindMenuButtons();
        }

        protected override void OnBeforeClearBattleBinding()
        {
            this.RemoveMenuButtonListeners();
        }

        void BindMenuButtons()
        {
            if (!this.IsBattleBound || !this.GetIsLoadedIndex(0))
            {
                return;
            }

            this.RemoveMenuButtonListeners();
            XUI.AddButtonListener(nameTableDic["btn_go_on"].GetComponent<Button>(), OnGoOn);
            XUI.AddButtonListener(nameTableDic["btn_replay"].GetComponent<Button>(), OnReplay);
            XUI.AddButtonListener(nameTableDic["btn_choose_level"].GetComponent<Button>(), OnChooseOtherLevel);
        }

        void RemoveMenuButtonListeners()
        {
            if (!this.GetIsLoadedIndex(0))
            {
                return;
            }

            nameTableDic["btn_go_on"].GetComponent<Button>().onClick.RemoveAllListeners();
            nameTableDic["btn_replay"].GetComponent<Button>().onClick.RemoveAllListeners();
            nameTableDic["btn_choose_level"].GetComponent<Button>().onClick.RemoveAllListeners();
        }

        protected override void ReleaseCallBack()
        {
            this.RemoveMenuButtonListeners();
            this.ClearBattleBinding();
        }

        private void OnGoOn()
        {
            this.battle.eventDispatcher.DispatchEvent(BattleEvent.GO_ON_GAME);
            Close();
        }

        private void OnReplay()
        {
            this.battle.eventDispatcher.DispatchEvent(BattleEvent.REPLAY_THE_GAME);
            Close();
        }

        private void OnChooseOtherLevel()
        {
            BusinessProvision.Instance.eventDispatcher.DispatchEvent(CommonEventType.RETURN_TO_MAIN_SCENE);
            Close();
        }
    }
}
