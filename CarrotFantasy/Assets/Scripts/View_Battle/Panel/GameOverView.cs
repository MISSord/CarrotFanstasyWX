using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>战斗失败结算（独立 BaseView）。</summary>
    public class GameOverView : BaseView
    {
        public GameOverView() { }

        private BattleDataComponent dataComponent;

        public override void InitData()
        {
            viewName = "GameOverView";
            layer = UILayer.Hight;
            SetUILoadInfo(0, UiViewAbPaths.NormalMordelPrefab, "GameOverPage");

            // 不依赖 prefab 加载即可监听事件
            TryHookBattleEvents();
        }

        private void TryHookBattleEvents()
        {
            if (BattleManager.Instance?.baseBattle == null) return;
            dataComponent = (BattleDataComponent)BattleManager.Instance.baseBattle.GetComponent(BattleComponentType.DataComponent);
            dataComponent?.eventDispatcher.AddListener(BattleEvent.SHOW_GAME_OVER_PAGE, ShowGameOver);
        }

        protected override void LoadCallBack()
        {
            XUI.AddButtonListener(nameTableDic["btn_replay"].GetComponent<Button>(), OnReplay);
            XUI.AddButtonListener(nameTableDic["btn_choose_level"].GetComponent<Button>(), OnChooseOtherLevel);
        }

        protected override void ReleaseCallBack()
        {
            nameTableDic["btn_replay"].GetComponent<Button>().onClick.RemoveAllListeners();
            nameTableDic["btn_choose_level"].GetComponent<Button>().onClick.RemoveAllListeners();
            dataComponent = null;
        }

        private void ShowGameOver()
        {
            Open(0);
            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Lose");

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

