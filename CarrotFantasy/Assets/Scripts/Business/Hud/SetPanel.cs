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

        // 显示设置草稿（点「应用」后才写入并生效）
        private GameObject displayRootGo;
        private Text txtResolution;
        private Text txtDisplayMode;
        private Resolution[] resolutionOptions;
        private FullScreenMode[] displayModeOptions;
        private int draftResolutionIndex;
        private int draftModeIndex;
        private bool displayUiBound;

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
            this.BindDisplaySettingsUi();

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

        /// <summary>
        /// 绑定 PC 显示设置控件。预制体未就绪时安全跳过（节点缺失只打 Warning）。
        /// 需要的 NameTable 名称见各 GetGameObjectSafely 调用。
        /// </summary>
        private void BindDisplaySettingsUi()
        {
            this.displayUiBound = false;
            if (!this.nameTableDic.Has("node_DisplaySettings"))
            {
                return;
            }

            this.displayRootGo = this.nameTableDic.GetGameObjectSafely("node_DisplaySettings");

            if (!DisplaySettings.IsSupported)
            {
                if (this.displayRootGo != null)
                {
                    this.displayRootGo.SetActive(false);
                }

                return;
            }

            if (this.displayRootGo == null)
            {
                return;
            }

            this.displayRootGo.SetActive(true);

            this.txtResolution = this.nameTableDic.GetComponentSafely<Text>("Txt_Resolution");
            this.txtDisplayMode = this.nameTableDic.GetComponentSafely<Text>("Txt_DisplayMode");

            Button btnResPrev = this.nameTableDic.GetComponentSafely<Button>("Btn_ResolutionPrev");
            Button btnResNext = this.nameTableDic.GetComponentSafely<Button>("Btn_ResolutionNext");
            Button btnModePrev = this.nameTableDic.GetComponentSafely<Button>("Btn_DisplayModePrev");
            Button btnModeNext = this.nameTableDic.GetComponentSafely<Button>("Btn_DisplayModeNext");
            Button btnApply = this.nameTableDic.GetComponentSafely<Button>("Btn_ApplyDisplay");

            if (this.txtResolution == null || this.txtDisplayMode == null
                || btnResPrev == null || btnResNext == null
                || btnModePrev == null || btnModeNext == null
                || btnApply == null)
            {
                Debug.LogWarning("[SetPanel] 显示设置节点不完整，已跳过绑定。请按文档补齐预制体 NameTable。");
                return;
            }

            this.resolutionOptions = DisplaySettings.GetAvailableResolutions();
            this.displayModeOptions = DisplaySettings.GetAvailableModes();

            DisplaySettings.Snapshot current = DisplaySettings.LoadOrCreateDefault();
            this.draftResolutionIndex = DisplaySettings.FindResolutionIndex(current.Width, current.Height);
            this.draftModeIndex = DisplaySettings.FindModeIndex(current.Mode);

            XUI.AddButtonListener(btnResPrev, this.OnResolutionPrev);
            XUI.AddButtonListener(btnResNext, this.OnResolutionNext);
            XUI.AddButtonListener(btnModePrev, this.OnDisplayModePrev);
            XUI.AddButtonListener(btnModeNext, this.OnDisplayModeNext);
            XUI.AddButtonListener(btnApply, this.OnApplyDisplay);

            this.displayUiBound = true;
            this.RefreshDisplayDraftLabels();
        }

        private void OnResolutionPrev()
        {
            if (!this.displayUiBound || this.resolutionOptions.Length == 0)
            {
                return;
            }

            this.draftResolutionIndex =
                (this.draftResolutionIndex - 1 + this.resolutionOptions.Length) % this.resolutionOptions.Length;
            this.RefreshDisplayDraftLabels();
        }

        private void OnResolutionNext()
        {
            if (!this.displayUiBound || this.resolutionOptions.Length == 0)
            {
                return;
            }

            this.draftResolutionIndex = (this.draftResolutionIndex + 1) % this.resolutionOptions.Length;
            this.RefreshDisplayDraftLabels();
        }

        private void OnDisplayModePrev()
        {
            if (!this.displayUiBound || this.displayModeOptions.Length == 0)
            {
                return;
            }

            this.draftModeIndex =
                (this.draftModeIndex - 1 + this.displayModeOptions.Length) % this.displayModeOptions.Length;
            this.RefreshDisplayDraftLabels();
        }

        private void OnDisplayModeNext()
        {
            if (!this.displayUiBound || this.displayModeOptions.Length == 0)
            {
                return;
            }

            this.draftModeIndex = (this.draftModeIndex + 1) % this.displayModeOptions.Length;
            this.RefreshDisplayDraftLabels();
        }

        private void OnApplyDisplay()
        {
            if (!this.displayUiBound)
            {
                return;
            }

            Resolution res = this.resolutionOptions[this.draftResolutionIndex];
            FullScreenMode mode = this.displayModeOptions[this.draftModeIndex];
            DisplaySettings.Apply(
                new DisplaySettings.Snapshot
                {
                    Width = res.width,
                    Height = res.height,
                    Mode = mode,
                },
                persist: true);
        }

        private void RefreshDisplayDraftLabels()
        {
            if (!this.displayUiBound)
            {
                return;
            }

            Resolution res = this.resolutionOptions[this.draftResolutionIndex];
            this.txtResolution.text = string.Format(LanguageUtil.Instance.GetString(2001), DisplaySettings.FormatResolution(res.width, res.height));
            this.txtDisplayMode.text = string.Format(LanguageUtil.Instance.GetString(2002), DisplaySettings.GetModeDisplayName(this.displayModeOptions[this.draftModeIndex]));
        }

        private void ShowOptionPage()
        {
            this.stateId = 1;
            this.UpdatePagePosition();
        }

        private void ShowProducePage()
        {
            this.stateId = 2;
            this.UpdatePagePosition();
        }

        private void ReturnToLastPanel()
        {
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
