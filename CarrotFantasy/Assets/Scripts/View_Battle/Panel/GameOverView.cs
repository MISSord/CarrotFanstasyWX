using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>战斗失败结算（独立 BaseView）。</summary>
    public class GameOverView : BaseView
    {
        public override void InitData()
        {
            viewName = "GameOverView";
            layer = UILayer.Hight;
            SetUILoadInfo(0, UiViewAbPaths.NormalMordelPrefab, "GameOverPage");
        }

        protected override void LoadCallBack()
        {
            XUI.AddButtonListener(nameTableDic["btn_replay"].GetComponent<Button>(), OnReplay);
            XUI.AddButtonListener(nameTableDic["btn_choose_level"].GetComponent<Button>(), OnChooseOtherLevel);

            BattleDataComponent dataComponent = (BattleDataComponent)BattleManager.Instance.baseBattle.GetComponent(BattleComponentType.DataComponent);
            int waves = dataComponent.curWaves;
            nameTableDic["txt_result_show"].GetComponent<Text>().text = LanguageUtil.Instance.GetFormatString(
                1002,
                (waves / 10).ToString(),
                (waves % 10).ToString(),
                dataComponent.totalWaves.ToString());

            nameTableDic["txt_level_show"].GetComponent<Text>().text = LanguageUtil.Instance.GetFormatString(
                1003,
                dataComponent.bigLevel.ToString(),
                dataComponent.level.ToString());
        }

        protected override void ReleaseCallBack()
        {
            nameTableDic["btn_replay"].GetComponent<Button>().onClick.RemoveAllListeners();
            nameTableDic["btn_choose_level"].GetComponent<Button>().onClick.RemoveAllListeners();
        }

        private void OnReplay()
        {
            UIServer.Instance.PlayButtonEffect();
            Close();
            BattleManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.REPLAY_THE_GAME);
        }

        private void OnChooseOtherLevel()
        {
            UIServer.Instance.PlayButtonEffect();
            Close();
            BusinessProvision.Instance.eventDispatcher.DispatchEvent(CommonEventType.RETURN_TO_MAIN_SCENE);
        }
    }
}

