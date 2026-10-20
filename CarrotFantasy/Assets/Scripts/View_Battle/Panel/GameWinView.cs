using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>战斗胜利结算（独立 BaseView）。</summary>
    public class GameWinView : BattleBoundView
    {
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

            // 走 Image 扩展：不用关心 Img_Carrot 上有没有 UIImageLoader，
            // 缺失时扩展会自动补一个，加载、换图与引用释放都由它负责。
            // （原先用 Resources.Load("Pictures/...Carrot_N") 是已失效的旧路径，
            //   Resources 下没有 Pictures 目录，只会把 prefab 上已绑好的图覆盖成 null。）
            Image carrotImage = nameTableDic["Img_Carrot"].GetComponent<Image>();
            if (carrotImage != null)
            {
                carrotImage.SetAtlasSprite(ResPath.GetGameOptionImagePath(), "carrot_" + trophy);
            }
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
