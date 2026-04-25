using UnityEngine;
using UnityEngine.UI;


namespace CarrotFantasy
{
    /// <summary>
    /// 顶部UI显示页面
    /// </summary>
    public class TopPage
    {

        private Transform transform;
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

        public TopPage(Transform node)
        {
            this.transform = node;
            this.btn_gameSpeedSprites = new Sprite[2];
            this.btn_pauseSprites = new Sprite[2];
        }

        private void loadResource()
        {
            this.btn_gameSpeedSprites[0] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/speed_1");
            this.btn_gameSpeedSprites[1] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/speed_2");

            this.btn_pauseSprites[0] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/pause_1");
            this.btn_pauseSprites[1] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/pause_3");
        }

        private void loadTransform()
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

            this.dataComponent.eventDispatcher.AddListener<int>(BattleEvent.COIN_CHANGE, this.updateCoinText);
            this.dataComponent.eventDispatcher.AddListener<int>(BattleEvent.WAVES_NUMBER_ADD, this.updateRoundText);

            this.battle.eventDispatcher.AddListener<bool>(BattleEvent.GAME_STATE_CHANGE, this.PauseGame);
        }

        private void RemoveListener()
        {
            this.dataComponent.eventDispatcher.RemoveListener<int>(BattleEvent.COIN_CHANGE, this.updateCoinText);
            this.dataComponent.eventDispatcher.RemoveListener<int>(BattleEvent.WAVES_NUMBER_ADD, this.updateRoundText);

            this.battle.eventDispatcher.RemoveListener<bool>(BattleEvent.GAME_STATE_CHANGE, this.PauseGame);
        }

        public void Init()
        {
            this.dataComponent = (BattleDataComponent)GameManager.Instance.baseBattle.GetComponent(BattleComponentType.DataComponent);
            this.battle = GameManager.Instance.baseBattle;
            this.loadResource();
            this.loadTransform();

            this.AddListener();

            this.updateCoinText(0);
            this.updateRoundText(0);

            this.isPause = this.battle.isPause;
            isNormalSpeed = true;
            this.updateBtnPause();
            this.updateBtnSpeed();

            this.node_pause.SetActive(this.isPause);
            this.node_playingText.SetActive(!this.isPause);
        }

        //更新UI文本
        private void updateCoinText(int coin)
        {
            this.txtCoin.text = dataComponent.CoinCount.ToString();
        }

        private void updateRoundText(int i)
        {
            int waves = dataComponent.curWaves;
            this.txtWaveInfo.text = LanguageUtil.Instance.getFormatString(1001, (waves / 10).ToString(), (waves % 10).ToString(), dataComponent.totalWaves.ToString());
        }

        private void updateBtnPause()
        {
            img_Btn_Pause.sprite = btn_pauseSprites[isPause ? 1 : 0];
        }

        private void updateBtnSpeed()
        {
            //img_Btn_GameSpeed.sprite = btn_gameSpeedSprites[isNormalSpeed ? 0 : 1];
        }

        //改变游戏速度
        public void ChangeGameSpeed()
        {
            UIServer.Instance.playButtonEffect();
            isNormalSpeed = !isNormalSpeed;
            this.updateBtnSpeed();
        }

        public void BtnPauseGame()
        {
            UIServer.Instance.playButtonEffect();
            if (this.isPause == true)
            {
                GameManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.GO_ON_GAME);
            }
            else
            {
                GameManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.PAUSE_THE_GAME);
            }
        }

        //游戏暂停
        public void PauseGame(bool isPause)
        {
            this.isPause = isPause;
            this.updateBtnPause();
            this.node_pause.SetActive(isPause);
            this.node_playingText.SetActive(!isPause);
        }

        public void Dispose()
        {
            this.RemoveListener();
        }
    }
}


