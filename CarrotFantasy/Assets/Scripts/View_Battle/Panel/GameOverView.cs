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
            this.RefreshSettlementContent();
            this.NotifyBattleUiReady();
        }

        protected override void RefreshBattleBinding()
        {
            this.RefreshSettlementContent();
        }

        protected override void OnBeforeClearBattleBinding()
        {
            this.RemoveSettlementButtonListeners();
        }

        void RefreshSettlementContent()
        {
            if (!this.IsBattleBound || !this.GetIsLoadedIndex(0) || this.pveDataComponent == null)
            {
                return;
            }

            this.RemoveSettlementButtonListeners();
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

        void RemoveSettlementButtonListeners()
        {
            if (!this.GetIsLoadedIndex(0))
            {
                return;
            }

            nameTableDic["btn_replay"].GetComponent<Button>().onClick.RemoveAllListeners();
            nameTableDic["btn_choose_level"].GetComponent<Button>().onClick.RemoveAllListeners();
        }

        protected override void ReleaseCallBack()
        {
            this.RemoveSettlementButtonListeners();
            this.ClearBattleBinding();
        }

        private void OnReplay()
        {
            Close();
            this.battle.eventDispatcher.DispatchEvent(BattleEvent.REPLAY_THE_GAME);
        }

        private void OnChooseOtherLevel()
        {
            Close();
            BusinessProvision.Instance.eventDispatcher.DispatchEvent(CommonEventType.RETURN_TO_MAIN_SCENE);
        }
    }
}
