using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>战斗失败结算（独立 BaseView）。</summary>
    public class GameOverView : BaseView
    {
        public GameOverView() { }

        private BattleDataComponent dataComponent;

        private Text txtResultShow;
        private Text txtLevelShow;
        private Button btnReplay;
        private Button btnChooseLevel;

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
            txtResultShow = nameTableDic["txt_result_show"].GetComponent<Text>();
            txtLevelShow = nameTableDic["txt_level_show"].GetComponent<Text>();
            btnReplay = nameTableDic["btn_replay"].GetComponent<Button>();
            btnChooseLevel = nameTableDic["btn_choose_level"].GetComponent<Button>();

            btnReplay.onClick.AddListener(OnReplay);
            btnChooseLevel.onClick.AddListener(OnChooseOtherLevel);
        }

        protected override void ReleaseCallBack()
        {
            btnReplay?.onClick.RemoveAllListeners();
            btnChooseLevel?.onClick.RemoveAllListeners();
            dataComponent = null;
            txtResultShow = null;
            txtLevelShow = null;
            btnReplay = null;
            btnChooseLevel = null;
        }

        private void ShowGameOver()
        {
            Open(0);
            UIServer.Instance.audioManager.PlayEffect("AudioClips/NormalMordel/Lose");

            if (dataComponent == null)
            {
                TryHookBattleEvents();
            }

            int waves = dataComponent.curWaves;
            txtResultShow.text = LanguageUtil.Instance.GetFormatString(
                1002,
                (waves / 10).ToString(),
                (waves % 10).ToString(),
                dataComponent.totalWaves.ToString());

            txtLevelShow.text = LanguageUtil.Instance.GetFormatString(
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

