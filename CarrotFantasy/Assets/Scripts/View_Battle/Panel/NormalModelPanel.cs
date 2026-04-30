using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class NormalModelPanel : BaseView
    {
        private GameObject nodeTopPage;
        private GameObject nodeStartUI;
        private Button btnMenuPage;
        private Button btnPause;
        private Text txtCoin;
        private Text txtWaveInfo;
        private Image imgBtnPause;
        private GameObject nodePause;
        private GameObject nodePlayingText;
        private Sprite[] btnPauseSprites;
        private BattleDataComponent dataComponent;
        private BaseBattle battle;
        private bool isPause;
        private int schId;
        private int schId_startGame;

        public override void InitData()
        {
            viewName = "NormalModelPanel";
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.ViewRootPrefab, "NormalModelPanel");
        }

        protected override void LoadCallBack()
        {
            this.nodeTopPage = this.nameTableDic["node_TopPage"];
            this.nodeStartUI = this.nameTableDic["StartUI"];
            this.txtCoin = this.nameTableDic["txt_coin"].GetComponent<Text>();
            this.txtWaveInfo = this.nameTableDic["txt_waves_info"].GetComponent<Text>();
            this.btnMenuPage = this.nameTableDic["Btn_Menu"].GetComponent<Button>();
            this.btnPause = this.nameTableDic["Btn_Pause"].GetComponent<Button>();
            this.imgBtnPause = this.btnPause.GetComponent<Image>();
            this.nodePause = this.nameTableDic["node_pause"];
            this.nodePlayingText = this.nameTableDic["node_playing_text"];
            this.LoadTopResources();

            this.AddListener();
        }

        private void InitPages()
        {
            this.battle = BattleManager.Instance.baseBattle;
            this.dataComponent = (BattleDataComponent)this.battle.GetComponent(BattleComponentType.DataComponent);
            this.dataComponent.eventDispatcher.RemoveListener<int>(BattleEvent.COIN_CHANGE, this.UpdateCoinText);
            this.dataComponent.eventDispatcher.RemoveListener<int>(BattleEvent.WAVES_NUMBER_ADD, this.UpdateRoundText);
            this.dataComponent.eventDispatcher.AddListener<int>(BattleEvent.COIN_CHANGE, this.UpdateCoinText);
            this.dataComponent.eventDispatcher.AddListener<int>(BattleEvent.WAVES_NUMBER_ADD, this.UpdateRoundText);
            this.isPause = this.battle.isPause;
            this.UpdateCoinText(0);
            this.UpdateRoundText(0);
            this.UpdateBtnPause();
            this.nodePause.SetActive(this.isPause);
            this.nodePlayingText.SetActive(!this.isPause);

            this.nodeTopPage.SetActive(true);
        }

        private void ShowMenu()
        {
            UIServer.Instance.PlayButtonEffect();
            ViewManager.Instance.OpenView<MenuView>();
            BattleManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.PAUSE_THE_GAME);
        }

        private void AddListener()
        {
            BattleManager.Instance.baseBattle.eventDispatcher.AddListener(BattleEvent.START_GAME, this.ShowStartUI);
            BattleManager.Instance.baseBattle.eventDispatcher.AddListener<bool>(BattleEvent.GAME_STATE_CHANGE, this.PauseGame);
            this.btnPause.onClick.AddListener(this.BtnPauseGame);
            this.btnMenuPage.onClick.AddListener(this.ShowMenu);
        }

        private void RemoveListener()
        {
            BattleManager.Instance.baseBattle.eventDispatcher.RemoveListener(BattleEvent.START_GAME, this.ShowStartUI);
            if (this.dataComponent != null)
            {
                this.dataComponent.eventDispatcher.RemoveListener<int>(BattleEvent.COIN_CHANGE, this.UpdateCoinText);
                this.dataComponent.eventDispatcher.RemoveListener<int>(BattleEvent.WAVES_NUMBER_ADD, this.UpdateRoundText);
            }
            if (BattleManager.Instance?.baseBattle != null)
            {
                BattleManager.Instance.baseBattle.eventDispatcher.RemoveListener<bool>(BattleEvent.GAME_STATE_CHANGE, this.PauseGame);
            }
            this.btnPause.onClick.RemoveAllListeners();
            this.btnMenuPage.onClick.RemoveAllListeners();
        }

        private void ShowStartUI()
        {
            this.InitPages();
            this.nodeStartUI.SetActive(true);
            BattleSchedulerComponent sche = (BattleSchedulerComponent)BattleManager.Instance.baseBattle.GetComponent(BattleComponentType.SchedulerComponent);
            this.schId = sche.DelayExeOnceTimes(() =>
            {
                this.nodeStartUI.SetActive(false);
                UIServer.Instance.audioManager.PlayEffect("AudioClips/NormalMordel/Go");
            }, 3.0f);
            this.schId_startGame = sche.DelayExeMultipleTimes(() =>
            {
                UIServer.Instance.audioManager.PlayEffect("AudioClips/NormalMordel/CountDown");
            }, 1.0f);
            sche.DelayExeOnceTimes(() =>
            {
                sche.SilenceSingleSche(this.schId_startGame);
            }, 3.5f);
        }

        protected override void ReleaseCallBack()
        {
            this.btnPauseSprites = null;
            this.RemoveListener();
        }

        private void LoadTopResources()
        {
            this.btnPauseSprites = new Sprite[2];
            this.btnPauseSprites[0] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/pause_1");
            this.btnPauseSprites[1] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/pause_3");
        }

        private void UpdateCoinText(int coin)
        {
            this.txtCoin.text = this.dataComponent.CoinCount.ToString();
        }

        private void UpdateRoundText(int i)
        {
            int waves = this.dataComponent.curWaves;
            this.txtWaveInfo.text = LanguageUtil.Instance.GetFormatString(1001, (waves / 10).ToString(), (waves % 10).ToString(), this.dataComponent.totalWaves.ToString());
        }

        private void UpdateBtnPause()
        {
            this.imgBtnPause.sprite = this.btnPauseSprites[this.isPause ? 1 : 0];
        }

        private void BtnPauseGame()
        {
            UIServer.Instance.PlayButtonEffect();
            if (this.isPause)
            {
                BattleManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.GO_ON_GAME);
            }
            else
            {
                BattleManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.PAUSE_THE_GAME);
            }
        }

        private void PauseGame(bool pauseState)
        {
            this.isPause = pauseState;
            this.UpdateBtnPause();
            this.nodePause.SetActive(pauseState);
            this.nodePlayingText.SetActive(!pauseState);
        }
    }
}
