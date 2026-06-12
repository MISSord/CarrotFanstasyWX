using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>战斗失败结算（独立 BaseView）。</summary>
    public class GameOverView : BattleBoundView
    {
        public override void InitData()
        {
            viewName = "GameOverView";
            layer = UILayer.Hight;
            SetUILoadInfo(0, UiViewAbPaths.NormalMordelPrefab, "GameOverPage");
        }

        protected override void LoadCallBack()
        {
            if (!this.IsBattleBound || this.pveDataComponent == null)
            {
                return;
            }

            XUI.AddButtonListener(nameTableDic["btn_replay"].GetComponent<Button>(), OnReplay);
            XUI.AddButtonListener(nameTableDic["btn_choose_level"].GetComponent<Button>(), OnChooseOtherLevel);

            int waves = this.pveDataComponent.curWaves;
            nameTableDic["txt_result_show"].GetComponent<Text>().text = LanguageUtil.Instance.GetFormatString(
                1002,
                (waves / 10).ToString(),
                (waves % 10).ToString(),
                this.pveDataComponent.totalWaves.ToString());

            nameTableDic["txt_level_show"].GetComponent<Text>().text = LanguageUtil.Instance.GetFormatString(
                1003,
                this.pveDataComponent.bigLevel.ToString(),
                this.pveDataComponent.level.ToString());
        }

        protected override void ReleaseCallBack()
        {
            nameTableDic["btn_replay"].GetComponent<Button>().onClick.RemoveAllListeners();
            nameTableDic["btn_choose_level"].GetComponent<Button>().onClick.RemoveAllListeners();
            this.ClearBattleBinding();
        }

        private void OnReplay()
        {
            UIServer.Instance.PlayButtonEffect();
            Close();
            this.battle.eventDispatcher.DispatchEvent(BattleEvent.REPLAY_THE_GAME);
        }

        private void OnChooseOtherLevel()
        {
            UIServer.Instance.PlayButtonEffect();
            Close();
            BusinessProvision.Instance.eventDispatcher.DispatchEvent(CommonEventType.RETURN_TO_MAIN_SCENE);
        }
    }
}
