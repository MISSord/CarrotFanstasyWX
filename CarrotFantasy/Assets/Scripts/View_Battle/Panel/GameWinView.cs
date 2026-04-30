using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>战斗胜利结算（独立 BaseView）。</summary>
    public class GameWinView : BaseView
    {
        public GameWinView() { }

        private BattleDataComponent dataComponent;

        private Sprite[] carrotSprites; // 0 铜 1 银 2 金

        public override void InitData()
        {
            viewName = "GameWinView";
            layer = UILayer.Hight;
            SetUILoadInfo(0, UiViewAbPaths.NormalMordelPrefab, "GameWinPage");

            TryHookBattleEvents();
        }

        private void TryHookBattleEvents()
        {
            if (BattleManager.Instance?.baseBattle == null) return;
            dataComponent = (BattleDataComponent)BattleManager.Instance.baseBattle.GetComponent(BattleComponentType.DataComponent);
            dataComponent?.eventDispatcher.AddListener(BattleEvent.SHOW_GAME_FINISH_PAGE, ShowGameWin);
        }

        protected override void LoadCallBack()
        {
            carrotSprites = new Sprite[3];
            for (int i = 0; i < 3; i++)
            {
                carrotSprites[i] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/GameOption/Normal/Level/Carrot_" + (i + 1));
            }

            XUI.AddButtonListener(nameTableDic["btn_replay"].GetComponent<Button>(), OnReplay);
            XUI.AddButtonListener(nameTableDic["btn_choose_level"].GetComponent<Button>(), OnChooseOtherLevel);
        }

        protected override void ReleaseCallBack()
        {
            nameTableDic["btn_replay"].GetComponent<Button>().onClick.RemoveAllListeners();
            nameTableDic["btn_choose_level"].GetComponent<Button>().onClick.RemoveAllListeners();
            dataComponent = null;
            carrotSprites = null;
        }

        private void ShowGameWin()
        {
            Open(0);
            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Perfect");

            if (dataComponent == null)
            {
                TryHookBattleEvents();
            }

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

            int trophy = Mathf.Clamp(dataComponent.CarrotTropyLevel(), 1, 3);
            nameTableDic["Img_Carrot"].GetComponent<Image>().sprite = carrotSprites[trophy - 1];
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

