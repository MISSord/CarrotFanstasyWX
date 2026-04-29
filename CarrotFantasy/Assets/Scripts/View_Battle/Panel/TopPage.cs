using UnityEngine;
using UnityEngine.UI;


namespace CarrotFantasy
{
    /// <summary>
    /// 顶部UI显示页面
    /// </summary>
    public class TopPage : BaseView
    {
        //引用
        private Text txtCoin;
        private Text txtWaveInfo;

        private Image img_Btn_GameSpeed;
        private Image img_Btn_Pause;

        private GameObject node_pause;
        private GameObject node_playingText;

        private GameObject nodeBtnPause;
        private GameObject nodeBtnGameSpeed;
        private Button btnMenu;

        private BattleDataComponent dataComponent;

        //按钮图片切换资源
        public Sprite[] btn_gameSpeedSprites;
        public Sprite[] btn_pauseSprites;

        public BaseBattle battle;

        //开关
        private bool isNormalSpeed;
        private bool isPause;
        private bool isLoaded;

        public TopPage(Transform node)
        {
            this.transform = node;
            this.btn_gameSpeedSprites = new Sprite[2];
            this.btn_pauseSprites = new Sprite[2];
        }

        public override void InitData()
        {
            viewName = "TopPage";
            layer = UILayer.Normal;
        }

        public void BindNode(Transform node)
        {
            this.transform = node;
        }

        public void OpenPage()
        {
            if (!isLoaded)
            {
                LoadCallBack();
                isLoaded = true;
            }
            if (this.transform != null)
            {
                this.transform.gameObject.SetActive(true);
            }
        }

        public void ClosePage()
        {
            if (this.transform != null)
            {
                this.transform.gameObject.SetActive(false);
            }
        }

        private void LoadResource()
        {
            this.btn_gameSpeedSprites[0] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/speed_1");
            this.btn_gameSpeedSprites[1] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/speed_2");

            this.btn_pauseSprites[0] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/pause_1");
            this.btn_pauseSprites[1] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/pause_3");
        }

        private void LoadTransform()
        {
            this.txtCoin = this.transform.Find("txt_coin").GetComponent<Text>();
            this.txtWaveInfo = this.transform.Find("node_playing_text/txt_waves_info").GetComponent<Text>();

            //this.nodeBtnGameSpeed = this.transform.Find("node_btn_container/Btn_GameSpeed").gameObject;
            this.nodeBtnPause = this.transform.Find("node_btn_container/Btn_Pause").gameObject;

            //this.img_Btn_GameSpeed = this.nodeBtnGameSpeed.transform.GetComponent<Image>();
            this.img_Btn_Pause = this.nodeBtnPause.transform.GetComponent<Image>();

            this.node_pause = this.transform.Find("node_pause").gameObject;
            this.node_playingText = this.transform.Find("node_playing_text").gameObject;
        }

        private void AddListener()
        {
            this.nodeBtnPause.transform.GetComponent<Button>().onClick.AddListener(this.BtnPauseGame);
            //this.nodeBtnGameSpeed.transform.GetComponent<Button>().onClick.AddListener(this.changeGameSpeed);

            this.dataComponent.eventDispatcher.AddListener<int>(BattleEvent.COIN_CHANGE, this.UpdateCoinText);
            this.dataComponent.eventDispatcher.AddListener<int>(BattleEvent.WAVES_NUMBER_ADD, this.UpdateRoundText);

            this.battle.eventDispatcher.AddListener<bool>(BattleEvent.GAME_STATE_CHANGE, this.PauseGame);
        }

        private void RemoveListener()
        {
            this.dataComponent.eventDispatcher.RemoveListener<int>(BattleEvent.COIN_CHANGE, this.UpdateCoinText);
            this.dataComponent.eventDispatcher.RemoveListener<int>(BattleEvent.WAVES_NUMBER_ADD, this.UpdateRoundText);

            this.battle.eventDispatcher.RemoveListener<bool>(BattleEvent.GAME_STATE_CHANGE, this.PauseGame);
        }

        protected override void LoadCallBack()
        {
            this.dataComponent = (BattleDataComponent)BattleManager.Instance.baseBattle.GetComponent(BattleComponentType.DataComponent);
            this.battle = BattleManager.Instance.baseBattle;
            this.LoadResource();
            this.LoadTransform();

            this.AddListener();

            this.UpdateCoinText(0);
            this.UpdateRoundText(0);

            this.isPause = this.battle.isPause;
            isNormalSpeed = true;
            this.UpdateBtnPause();
            this.UpdateBtnSpeed();

            this.node_pause.SetActive(this.isPause);
            this.node_playingText.SetActive(!this.isPause);
        }

        //更新UI文本
        private void UpdateCoinText(int coin)
        {
            this.txtCoin.text = dataComponent.CoinCount.ToString();
        }

        private void UpdateRoundText(int i)
        {
            int waves = dataComponent.curWaves;
            this.txtWaveInfo.text = LanguageUtil.Instance.GetFormatString(1001, (waves / 10).ToString(), (waves % 10).ToString(), dataComponent.totalWaves.ToString());
        }

        private void UpdateBtnPause()
        {
            img_Btn_Pause.sprite = btn_pauseSprites[isPause ? 1 : 0];
        }

        private void UpdateBtnSpeed()
        {
            //img_Btn_GameSpeed.sprite = btn_gameSpeedSprites[isNormalSpeed ? 0 : 1];
        }

        //改变游戏速度
        public void ChangeGameSpeed()
        {
            UIServer.Instance.PlayButtonEffect();
            isNormalSpeed = !isNormalSpeed;
            this.UpdateBtnSpeed();
        }

        public void BtnPauseGame()
        {
            UIServer.Instance.PlayButtonEffect();
            if (this.isPause == true)
            {
                BattleManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.GO_ON_GAME);
            }
            else
            {
                BattleManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.PAUSE_THE_GAME);
            }
        }

        //游戏暂停
        public void PauseGame(bool isPause)
        {
            this.isPause = isPause;
            this.UpdateBtnPause();
            this.node_pause.SetActive(isPause);
            this.node_playingText.SetActive(!isPause);
        }

        protected override void ReleaseCallBack()
        {
            this.RemoveListener();
            isLoaded = false;
        }
    }
}


