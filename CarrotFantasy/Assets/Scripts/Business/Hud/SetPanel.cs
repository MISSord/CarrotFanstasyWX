using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class SetPanel : BaseView
    {

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

            this.optionPageGo = this.nameTableDic["OptionPage"];
            this.producerPageGo = this.nameTableDic["ProducerPage"];
            this.Img_Btn_EffectAudio = this.nameTableDic["Btn_EffectAudio"].GetComponent<Image>();
            this.Img_Btn_BGAudio = this.nameTableDic["Btn_BGAudio"].GetComponent<Image>();

            this.btnBGAudio = this.nameTableDic["Btn_BGAudio"].GetComponent<Button>();
            this.btnEffectAudio = this.nameTableDic["Btn_EffectAudio"].GetComponent<Button>();

            this.btnOptionPage = this.nameTableDic["Btn_Option"].GetComponent<Button>();
            this.btnProducePage = this.nameTableDic["Btn_Producer"].GetComponent<Button>();
            this.btnReturn = this.nameTableDic["Btn_Return"].GetComponent<Button>();

            this.LoadResource();
            this.AddListener();

            this.Img_Btn_BGAudio.sprite = UIServer.Instance.audioManager.musicEnable == true ? this.btnSpritesList[2] : this.btnSpritesList[3];
            this.Img_Btn_EffectAudio.sprite = UIServer.Instance.audioManager.effectEnable == true ? this.btnSpritesList[0] : this.btnSpritesList[1];

            this.UpdatePagePosition();
        }

        private void UpdatePagePosition()
        {
            this.optionPageGo.transform.localPosition = this.stateId == 1 ? this.showPosition : this.fadePosition;
            this.producerPageGo.transform.localPosition = this.stateId == 2 ? this.showPosition : this.fadePosition;
        }

        private void AddListener()
        {
            this.btnBGAudio.onClick.AddListener(this.UpdateMusicState);
            this.btnEffectAudio.onClick.AddListener(this.UpdateEffectState);

            this.btnOptionPage.onClick.AddListener(this.ShowOptionPage);
            this.btnProducePage.onClick.AddListener(this.ShowProducePage);

            this.btnReturn.onClick.AddListener(this.ReturnToLastPanel);
        }

        private void ShowOptionPage()
        {
            this.stateId = 1;
            this.UpdatePagePosition();
            UIServer.Instance.PlayButtonEffect();
        }

        private void ShowProducePage()
        {
            this.stateId = 2;
            this.UpdatePagePosition();
            UIServer.Instance.PlayButtonEffect();
        }

        private void ReturnToLastPanel()
        {
            UIServer.Instance.PlayButtonEffect();
            this.Close();
        }

        private void UpdateMusicState()
        {
            if (UIServer.Instance.audioManager.musicEnable == true)
            {
                this.Img_Btn_BGAudio.sprite = this.btnSpritesList[3];
                UIServer.Instance.audioManager.SetMusicEnable(false);
            }
            else
            {
                this.Img_Btn_BGAudio.sprite = this.btnSpritesList[2];
                UIServer.Instance.audioManager.SetMusicEnable(true);
            }
        }

        private void UpdateEffectState()
        {
            if (UIServer.Instance.audioManager.effectEnable == true)
            {
                this.Img_Btn_EffectAudio.sprite = this.btnSpritesList[1];
                UIServer.Instance.audioManager.SetEffectEnable(false);
            }
            else
            {
                this.Img_Btn_EffectAudio.sprite = this.btnSpritesList[0];
                UIServer.Instance.audioManager.SetEffectEnable(true);
            }
        }

        private void LoadResource()
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
