using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class SetPanel : BaseView
    {
        private static SetPanel _instance;
        public static SetPanel Instance => _instance ?? (_instance = new SetPanel());

        private SetPanel() { }

        private GameObject optionPageGo;
        private GameObject producerPageGo;
        private bool playBGMusic = true;
        private bool playEffectMusic = true;
        public Sprite[] btnSpritesList;
        private Image Img_Btn_EffectAudio;
        private Image Img_Btn_BGAudio;

        private Button btnEffectAudio;
        private Button btnBGAudio;

        private Button btnOptionPage;
        private Button btnProducePage;
        private Button btnReturn;

        private int stateId;
        private Vector3 fadePosition = new Vector3(0, 3000, 0);
        private Vector3 showPosition = Vector3.zero;

        public override void InitData()
        {
            viewName = "SetPanel";
            layer = UILayer.Normal;
            this.btnSpritesList = new Sprite[4];
            SetUILoadInfo(0, UiViewAbPaths.SettingViewPrefab, "SetPanel");
        }

        protected override void LoadCallBack()
        {
            this.stateId = 1;

            this.optionPageGo = transform.Find("OptionPage").gameObject;
            this.producerPageGo = transform.Find("ProducerPage").gameObject;
            this.Img_Btn_EffectAudio = optionPageGo.transform.Find("Btn_EffectAudio").GetComponent<Image>();
            this.Img_Btn_BGAudio = optionPageGo.transform.Find("Btn_BGAudio").GetComponent<Image>();

            this.btnBGAudio = this.optionPageGo.transform.Find("Btn_BGAudio").GetComponent<Button>();
            this.btnEffectAudio = this.optionPageGo.transform.Find("Btn_EffectAudio").GetComponent<Button>();

            this.btnOptionPage = this.transform.Find("node_top/Btn_Option").GetComponent<Button>();
            this.btnProducePage = this.transform.Find("node_top/Btn_Producer").GetComponent<Button>();
            this.btnReturn = this.transform.Find("node_top/Btn_Return").GetComponent<Button>();

            this.loadResource();
            this.AddListener();

            this.Img_Btn_BGAudio.sprite = UIServer.Instance.audioManager.musicEnable == true ? this.btnSpritesList[2] : this.btnSpritesList[3];
            this.Img_Btn_EffectAudio.sprite = UIServer.Instance.audioManager.effectEnable == true ? this.btnSpritesList[0] : this.btnSpritesList[1];

            this.updatePagePosition();
        }

        private void updatePagePosition()
        {
            this.optionPageGo.transform.localPosition = this.stateId == 1 ? this.showPosition : this.fadePosition;
            this.producerPageGo.transform.localPosition = this.stateId == 2 ? this.showPosition : this.fadePosition;
        }

        private void AddListener()
        {
            this.btnBGAudio.onClick.AddListener(this.updateMusicState);
            this.btnEffectAudio.onClick.AddListener(this.updateEffectState);

            this.btnOptionPage.onClick.AddListener(this.showOptionPage);
            this.btnProducePage.onClick.AddListener(this.showProducePage);

            this.btnReturn.onClick.AddListener(this.returnToLastPanel);
        }

        private void showOptionPage()
        {
            this.stateId = 1;
            this.updatePagePosition();
            UIServer.Instance.PlayButtonEffect();
        }

        private void showProducePage()
        {
            this.stateId = 2;
            this.updatePagePosition();
            UIServer.Instance.PlayButtonEffect();
        }

        private void returnToLastPanel()
        {
            UIServer.Instance.PlayButtonEffect();
            this.Close();
        }

        private void updateMusicState()
        {
            if (UIServer.Instance.audioManager.musicEnable == true)
            {
                this.Img_Btn_BGAudio.sprite = this.btnSpritesList[3];
                UIServer.Instance.audioManager.setMusicEnable(false);
            }
            else
            {
                this.Img_Btn_BGAudio.sprite = this.btnSpritesList[2];
                UIServer.Instance.audioManager.setMusicEnable(true);
            }
        }

        private void updateEffectState()
        {
            if (UIServer.Instance.audioManager.effectEnable == true)
            {
                this.Img_Btn_EffectAudio.sprite = this.btnSpritesList[1];
                UIServer.Instance.audioManager.setEffectEnable(false);
            }
            else
            {
                this.Img_Btn_EffectAudio.sprite = this.btnSpritesList[0];
                UIServer.Instance.audioManager.setEffectEnable(true);
            }
        }

        private void loadResource()
        {
            this.btnSpritesList[0] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/Main/SetPanel/OptionPage/setting02-hd_15");
            this.btnSpritesList[1] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/Main/SetPanel/OptionPage/setting02-hd_21");
            this.btnSpritesList[2] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/Main/SetPanel/OptionPage/setting02-hd_6");
            this.btnSpritesList[3] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/Main/SetPanel/OptionPage/setting02-hd_11");
        }

        protected override void ReleaseCallBack()
        {
        }
    }
}
