using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>战斗胜利结算（独立 BaseView）。</summary>
    public class GameWinView : BattleBoundView
    {
        private Sprite[] carrotSprites; // 0 铜 1 银 2 金

        public override void InitData()
        {
            viewName = "GameWinView";
            layer = UILayer.Hight;
            SetUILoadInfo(0, UiViewAbPaths.NormalMordelPrefab, "GameWinPage");
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

            if (this.carrotSprites == null)
            {
                this.carrotSprites = new Sprite[3];
                for (int i = 0; i < 3; i++)
                {
                    this.carrotSprites[i] = ResourceLoader.Instance.loadRes<Sprite>(
                        "Pictures/GameOption/Normal/Level/Carrot_" + (i + 1));
                }
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

            int trophy = Mathf.Clamp(this.pveDataComponent.CarrotTropyLevel(), 1, 3);
            nameTableDic["Img_Carrot"].GetComponent<Image>().sprite = this.carrotSprites[trophy - 1];
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
            this.carrotSprites = null;
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
