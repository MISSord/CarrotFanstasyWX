using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class NormalModelPanel : BattleBoundView
    {
        private GameObject nodeTopPage;
        private GameObject nodeStartUI;
        private Text txtCoin;
        private Text txtWaveInfo;
        private GameObject nodePause;
        private GameObject nodePlayingText;
        private Sprite[] btnPauseSprites;
        private bool isPause;

        public override void InitData()
        {
            viewName = "NormalModelPanel";
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.NormalMordelPrefab, "NormalModelPanel");
        }

        protected override void LoadCallBack()
        {
            if (!this.IsBattleBound)
            {
                Debug.LogError("[NormalModelPanel] LoadCallBack 失败：未 BindBattle，请经 BattleViewOpener 打开。");
                return;
            }

            if (this.dataComponent == null || this.pveDataComponent == null)
            {
                Debug.LogError("[NormalModelPanel] LoadCallBack 失败：缺少 DataComponent 或 PVEDataComponent。");
                return;
            }

            if (!this.TryBindUiReferences(logError: true))
            {
                return;
            }

            this.EnsureUiBoundToBattle();
            this.NotifyBattleUiReady();
        }

        protected override void OnBeforeClearBattleBinding()
        {
            this.RemoveListener();
        }

        protected override void RefreshBattleBinding()
        {
            this.EnsureUiBoundToBattle();
        }

        bool TryBindUiReferences(bool logError)
        {
            if (!this.GetIsLoadedIndex(0))
            {
                return false;
            }

            this.nodeTopPage = this.nameTableDic.GetGameObjectSafely("node_TopPage");
            this.nodeStartUI = this.nameTableDic.GetGameObjectSafely("StartUI");
            this.nodePause = this.nameTableDic.GetGameObjectSafely("node_pause");
            this.nodePlayingText = this.nameTableDic.GetGameObjectSafely("node_playing_text");
            this.txtCoin = this.nameTableDic.GetComponentSafely<Text>("txt_coin");
            this.txtWaveInfo = this.nameTableDic.GetComponentSafely<Text>("txt_waves_info");

            if (this.nodeTopPage == null || this.nodeStartUI == null || this.txtCoin == null || this.txtWaveInfo == null)
            {
                if (logError)
                {
                    Debug.LogError("[NormalModelPanel] UI 绑定失败，请检查 NormalModelPanel 预制体与 UINameTable。");
                }

                return false;
            }

            return true;
        }

        protected override void ShowIndexCallBack(int viewIndex)
        {
            if (viewIndex == 0 && this.IsBattleBound)
            {
                this.EnsureUiBoundToBattle();
            }
        }

        /// <summary>首载与缓存复开均需重新绑定当前 battle 的事件与 UI 状态。</summary>
        void EnsureUiBoundToBattle()
        {
            if (!this.IsBattleBound || !this.GetIsLoadedIndex(0))
            {
                return;
            }

            if (this.nodeTopPage == null && !this.TryBindUiReferences(logError: true))
            {
                return;
            }

            if (this.btnPauseSprites == null || this.btnPauseSprites.Length < 2)
            {
                this.LoadTopResources();
            }

            this.RemoveListener();
            this.AddListener();
            this.SyncPauseStateFromBattle();

            if (this.battle.isStart)
            {
                this.ShowStartUI();
            }
        }

        protected override void CloseCallBack()
        {
            this.isPause = false;
        }

        void SyncPauseStateFromBattle()
        {
            if (this.battle == null)
            {
                return;
            }

            this.PauseGame(this.battle.isPause);
        }

        private void InitPages()
        {
            this.SyncPauseStateFromBattle();
            this.UpdateCoinText(0);
            this.UpdateRoundText(0);

            if (this.nodeTopPage != null)
            {
                this.nodeTopPage.SetActive(true);
            }
        }

        private void ShowMenu()
        {
            UIServer.Instance.PlayButtonEffect();
            this.battle.eventDispatcher.DispatchEvent(BattleEvent.PAUSE_THE_GAME);
            BattleViewOpener.Open<MenuView>(this.battle);
        }

        private void AddListener()
        {
            this.dataComponent.eventDispatcher.AddListener<int>(BattleEvent.COIN_CHANGE, this.UpdateCoinText);
            this.dataComponent.eventDispatcher.AddListener<int>(BattleEvent.WAVES_NUMBER_ADD, this.UpdateRoundText);
            this.battle.eventDispatcher.AddListener(BattleEvent.START_GAME, this.ShowStartUI);
            this.battle.eventDispatcher.AddListener(BattleEvent.START_GAME_INTRO_COUNTDOWN, this.PlayIntroCountdown);
            this.battle.eventDispatcher.AddListener(BattleEvent.START_GAME_INTRO_END, this.HideIntroUI);
            this.battle.eventDispatcher.AddListener<bool>(BattleEvent.GAME_STATE_CHANGE, this.PauseGame);
            Button btnPause = this.nameTableDic.GetComponentSafely<Button>("Btn_Pause");
            Button btnMenu = this.nameTableDic.GetComponentSafely<Button>("Btn_Menu");
            if (btnPause != null)
            {
                XUI.AddButtonListener(btnPause, this.BtnPauseGame);
            }

            if (btnMenu != null)
            {
                XUI.AddButtonListener(btnMenu, this.ShowMenu);
            }
        }

        private void RemoveListener()
        {
            if (this.battle != null)
            {
                this.battle.eventDispatcher.RemoveListener(BattleEvent.START_GAME, this.ShowStartUI);
                this.battle.eventDispatcher.RemoveListener(BattleEvent.START_GAME_INTRO_COUNTDOWN, this.PlayIntroCountdown);
                this.battle.eventDispatcher.RemoveListener(BattleEvent.START_GAME_INTRO_END, this.HideIntroUI);
                this.battle.eventDispatcher.RemoveListener<bool>(BattleEvent.GAME_STATE_CHANGE, this.PauseGame);
            }

            if (this.dataComponent != null)
            {
                this.dataComponent.eventDispatcher.RemoveListener<int>(BattleEvent.COIN_CHANGE, this.UpdateCoinText);
                this.dataComponent.eventDispatcher.RemoveListener<int>(BattleEvent.WAVES_NUMBER_ADD, this.UpdateRoundText);
            }
            Button btnPause = this.nameTableDic.GetComponentSafely<Button>("Btn_Pause");
            Button btnMenu = this.nameTableDic.GetComponentSafely<Button>("Btn_Menu");
            if (btnPause != null)
            {
                btnPause.onClick.RemoveAllListeners();
            }

            if (btnMenu != null)
            {
                btnMenu.onClick.RemoveAllListeners();
            }
        }

        private void ShowStartUI()
        {
            if (this.nodeStartUI == null)
            {
                Debug.LogWarning("[NormalModelPanel] ShowStartUI 跳过：StartUI 未绑定。");
                return;
            }

            this.InitPages();
            this.nodeStartUI.SetActive(true);
        }

        private void PlayIntroCountdown()
        {
            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/CountDown");
        }

        private void HideIntroUI()
        {
            if (this.nodeStartUI != null)
            {
                this.nodeStartUI.SetActive(false);
            }

            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/GO");
        }

        protected override void ReleaseCallBack()
        {
            this.btnPauseSprites = null;
            this.txtCoin = null;
            this.txtWaveInfo = null;
            this.nodeTopPage = null;
            this.nodeStartUI = null;
            this.nodePause = null;
            this.nodePlayingText = null;
            this.RemoveListener();
            this.ClearBattleBinding();
        }

        private void LoadTopResources()
        {
            this.btnPauseSprites = new Sprite[2];
            if (!FightViewSpriteAb.TryGetNormalMordel(FightViewSpriteAb.PausePlaying, out this.btnPauseSprites[0]))
            {
                Debug.LogError("[NormalModelPanel] pause_1 未预加载");
            }

            if (!FightViewSpriteAb.TryGetNormalMordel(FightViewSpriteAb.PausePaused, out this.btnPauseSprites[1]))
            {
                Debug.LogError("[NormalModelPanel] pause_3 未预加载");
            }
        }

        private void UpdateCoinText(int coin)
        {
            if (this.dataComponent == null)
            {
                return;
            }

            this.txtCoin.text = this.dataComponent.CoinCount.ToString();
        }

        private void UpdateRoundText(int i)
        {
            if (this.pveDataComponent == null)
            {
                return;
            }

            int waves = this.pveDataComponent.curWaves;
            this.txtWaveInfo.text = LanguageUtil.Instance.GetFormatString(1001, (waves / 10).ToString(), (waves % 10).ToString(), this.pveDataComponent.totalWaves.ToString());
        }

        private void UpdateBtnPause()
        {
            Image btnPauseImage = this.nameTableDic.GetComponentSafely<Image>("Btn_Pause");
            if (btnPauseImage == null || this.btnPauseSprites == null)
            {
                return;
            }

            btnPauseImage.sprite = this.btnPauseSprites[this.isPause ? 1 : 0];
        }

        private void BtnPauseGame()
        {
            UIServer.Instance.PlayButtonEffect();
            if (this.isPause)
            {
                this.battle.eventDispatcher.DispatchEvent(BattleEvent.GO_ON_GAME);
            }
            else
            {
                this.battle.eventDispatcher.DispatchEvent(BattleEvent.PAUSE_THE_GAME);
            }
        }

        private void PauseGame(bool pauseState)
        {
            this.isPause = pauseState;
            this.UpdateBtnPause();

            if (this.nodePause != null)
            {
                this.nodePause.SetActive(pauseState);
            }

            if (this.nodePlayingText != null)
            {
                this.nodePlayingText.SetActive(!pauseState);
            }
        }
    }
}
