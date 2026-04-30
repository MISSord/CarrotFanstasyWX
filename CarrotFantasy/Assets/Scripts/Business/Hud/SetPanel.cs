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

            this.LoadResource();
            this.AddListener();

            this.nameTableDic["Btn_BGAudio"].GetComponent<Image>().sprite =
                AudioManager.Instance.musicEnable == true ? this.btnSpritesList[2] : this.btnSpritesList[3];
            this.nameTableDic["Btn_EffectAudio"].GetComponent<Image>().sprite =
                AudioManager.Instance.effectEnable == true ? this.btnSpritesList[0] : this.btnSpritesList[1];

            this.UpdatePagePosition();
        }

        private void UpdatePagePosition()
        {
            this.optionPageGo.transform.localPosition = this.stateId == 1 ? this.showPosition : this.fadePosition;
            this.producerPageGo.transform.localPosition = this.stateId == 2 ? this.showPosition : this.fadePosition;
        }

        private void AddListener()
        {
            XUI.AddButtonListener(this.nameTableDic["Btn_BGAudio"].GetComponent<Button>(), this.UpdateMusicState);
            XUI.AddButtonListener(this.nameTableDic["Btn_EffectAudio"].GetComponent<Button>(), this.UpdateEffectState);

            XUI.AddButtonListener(this.nameTableDic["Btn_Option"].GetComponent<Button>(), this.ShowOptionPage);
            XUI.AddButtonListener(this.nameTableDic["Btn_Producer"].GetComponent<Button>(), this.ShowProducePage);

            XUI.AddButtonListener(this.nameTableDic["Btn_Return"].GetComponent<Button>(), this.ReturnToLastPanel);
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
            Image bgAudioImg = this.nameTableDic["Btn_BGAudio"].GetComponent<Image>();
            if (AudioManager.Instance.musicEnable == true)
            {
                bgAudioImg.sprite = this.btnSpritesList[3];
                AudioManager.Instance.SetMusicEnable(false);
            }
            else
            {
                bgAudioImg.sprite = this.btnSpritesList[2];
                AudioManager.Instance.SetMusicEnable(true);
            }
        }

        private void UpdateEffectState()
        {
            Image effectImg = this.nameTableDic["Btn_EffectAudio"].GetComponent<Image>();
            if (AudioManager.Instance.effectEnable == true)
            {
                effectImg.sprite = this.btnSpritesList[1];
                AudioManager.Instance.SetEffectEnable(false);
            }
            else
            {
                effectImg.sprite = this.btnSpritesList[0];
                AudioManager.Instance.SetEffectEnable(true);
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
