using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>战斗内暂停菜单（独立 BaseView）。</summary>
    public class MenuView : BaseView
    {
        public override void InitData()
        {
            viewName = "MenuView";
            layer = UILayer.Hight;
            SetUILoadInfo(0, UiViewAbPaths.NormalMordelPrefab, "MenuPage");
        }

        protected override void LoadCallBack()
        {
            XUI.AddButtonListener(nameTableDic["btn_go_on"].GetComponent<Button>(), OnGoOn);
            XUI.AddButtonListener(nameTableDic["btn_replay"].GetComponent<Button>(), OnReplay);
            XUI.AddButtonListener(nameTableDic["btn_choose_level"].GetComponent<Button>(), OnChooseOtherLevel);
        }

        protected override void ReleaseCallBack()
        {
            nameTableDic["btn_go_on"].GetComponent<Button>().onClick.RemoveAllListeners();
            nameTableDic["btn_replay"].GetComponent<Button>().onClick.RemoveAllListeners();
            nameTableDic["btn_choose_level"].GetComponent<Button>().onClick.RemoveAllListeners();
        }

        private void OnGoOn()
        {
            // 关闭时顺便恢复游戏，避免残留暂停
            if (BattleManager.Instance?.baseBattle != null)
            {
                BattleManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.GO_ON_GAME);
            }
            Close();
        }

        private void OnReplay()
        {
            BattleManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.REPLAY_THE_GAME);
            Close();
        }

        private void OnChooseOtherLevel()
        {
            BusinessProvision.Instance.eventDispatcher.DispatchEvent(CommonEventType.RETURN_TO_MAIN_SCENE);
            Close();
        }
    }
}

