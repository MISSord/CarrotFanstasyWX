using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class SetPanel : BaseView
    {
        private const string SpriteEffectOn = "setting02-hd_15";
        private const string SpriteEffectOff = "setting02-hd_21";
        private const string SpriteMusicOn = "setting02-hd_6";
        private const string SpriteMusicOff = "setting02-hd_11";

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
            SetUILoadInfo(0, UiViewAbPaths.SettingViewPrefab, "SetPanel");
        }

        protected override void LoadCallBack()
        {
            this.stateId = 1;
            this.LoadResource();
            this.AddListener();
            this.BindDisplaySettingsUi();
            this.UpdatePagePosition();
        }

        private void UpdatePagePosition()
        {

        }

        private void AddListener()
        {
            XUI.AddButtonListener(this.nameTableDic["Btn_BGAudio"].GetComponent<Button>(), this.UpdateMusicState);
            XUI.AddButtonListener(this.nameTableDic["Btn_EffectAudio"].GetComponent<Button>(), this.UpdateEffectState);
            XUI.AddButtonListener(this.nameTableDic["Btn_Option"].GetComponent<Button>(), this.ShowOptionPage);
            XUI.AddButtonListener(this.nameTableDic["Btn_Producer"].GetComponent<Button>(), this.ShowProducePage);
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
            AudioManager.Instance.SetMusicEnable(!AudioManager.Instance.musicEnable);
            this.ApplyMusicButtonSprite();
        }

        private void UpdateEffectState()
        {
            AudioManager.Instance.SetEffectEnable(!AudioManager.Instance.effectEnable);
            this.ApplyEffectButtonSprite();
        }

        /// <summary>通过 UIImageLoader 加载设置页音效按钮图。</summary>
        private void LoadResource()
        {
            this.ApplyMusicButtonSprite();
            this.ApplyEffectButtonSprite();
        }

        private void ApplyMusicButtonSprite()
        {
            Image bgAudioImg = this.nameTableDic["Btn_BGAudio"].GetComponent<Image>();
            if (bgAudioImg == null)
            {
                return;
            }

            string asset = AudioManager.Instance.musicEnable ? SpriteMusicOn : SpriteMusicOff;
            bgAudioImg.SetSprite(ResPath.GetSettingViewImagePath(), asset);
        }

        private void ApplyEffectButtonSprite()
        {
            Image effectImg = this.nameTableDic["Btn_EffectAudio"].GetComponent<Image>();
            if (effectImg == null)
            {
                return;
            }

            string asset = AudioManager.Instance.effectEnable ? SpriteEffectOn : SpriteEffectOff;
            effectImg.SetSprite(ResPath.GetSettingViewImagePath(), asset);
        }

        protected override void ReleaseCallBack()
        {
        }
    }
}
