using System;
using UnityEngine;
using UnityEngine.UI;
using XLua;

namespace CarrotFantasy
{
    /// <summary>
    /// SetPanel 的 Lua 版：交互流程在 <c>panels/set_panel.lua</c>，
    /// 这里只保留「设置 API + 文本/图片刷新」这些不能放进 Lua 的能力。
    /// <para>
    /// 两类职责的边界：
    /// </para>
    /// <list type="bullet">
    ///   <item>Lua：按钮绑定、页面切换、音效开关的点击流程；</item>
    ///   <item>C#（本类）：分辨率/显示模式枚举与持久化、AudioManager 开关、按钮图集刷新、多语言文本。</item>
    /// </list>
    /// <para>
    /// Lua 通过 <c>self.host</c> 调用 C#：host 里都是委托，
    /// 委托类型已登记在 <c>XluaGenConfig.CSharpCallLua</c>，IL2CPP 下同样可用。
    /// </para>
    /// </summary>
    public sealed class LuaSetPanelView : LuaView
    {
        private const string SpriteEffectOn = "setting02-hd_15";
        private const string SpriteEffectOff = "setting02-hd_21";
        private const string SpriteMusicOn = "setting02-hd_6";
        private const string SpriteMusicOff = "setting02-hd_11";

        private Resolution[] resolutionOptions;
        private FullScreenMode[] displayModeOptions;
        private int draftResolutionIndex;
        private int draftModeIndex;
        private bool displayUiBound;

        private Text txtResolution;
        private Text txtDisplayMode;

        protected override string LuaModule
        {
            get { return "panels.set_panel"; }
        }

        protected override string LuaBundle
        {
            get { return UiViewAbPaths.SettingViewPrefab; }
        }

        protected override string LuaAsset
        {
            get { return "SetPanel"; }
        }

        /// <summary>沿用 C# SetPanel 的名字，既有 <c>OpenView&lt;SetPanel&gt;()</c> 才能命中。</summary>
        protected override string LuaViewName
        {
            get { return "SetPanel"; }
        }

        protected override void LoadCallBack()
        {
            // 先刷新静态内容（按钮图、显示设置），再交给 Lua 跑 OnLoad 绑按钮。
            this.PrepareDisplaySettings();
            this.ApplyMusicButtonSprite();
            this.ApplyEffectButtonSprite();
            base.LoadCallBack();
        }

        protected override LuaTable BuildLuaHost()
        {
            LuaTable host = LuaManager.NewTable();
            if (host == null)
            {
                return null;
            }

            host.Set("ToggleMusic", new Action(this.ToggleMusic));
            host.Set("ToggleEffect", new Action(this.ToggleEffect));
            host.Set("IsDisplaySupported", new Func<bool>(this.IsDisplaySupported));
            host.Set("MoveResolution", new Action<int>(this.MoveResolution));
            host.Set("MoveMode", new Action<int>(this.MoveMode));
            host.Set("ApplyDisplay", new Action(this.ApplyDisplay));
            return host;
        }

        // ---------------------------------------------------------------- 音频

        private void ToggleMusic()
        {
            AudioManager.Instance.SetMusicEnable(!AudioManager.Instance.musicEnable);
            this.ApplyMusicButtonSprite();
        }

        private void ToggleEffect()
        {
            AudioManager.Instance.SetEffectEnable(!AudioManager.Instance.effectEnable);
            this.ApplyEffectButtonSprite();
        }

        private void ApplyMusicButtonSprite()
        {
            Image image = this.nameTableDic != null
                ? this.nameTableDic.GetComponentSafely<Image>("Btn_BGAudio")
                : null;
            if (image == null)
            {
                return;
            }

            string asset = AudioManager.Instance.musicEnable ? SpriteMusicOn : SpriteMusicOff;
            image.SetAtlasSprite(ResPath.GetSettingViewImagePath(), asset);
        }

        private void ApplyEffectButtonSprite()
        {
            Image image = this.nameTableDic != null
                ? this.nameTableDic.GetComponentSafely<Image>("Btn_EffectAudio")
                : null;
            if (image == null)
            {
                return;
            }

            string asset = AudioManager.Instance.effectEnable ? SpriteEffectOn : SpriteEffectOff;
            image.SetAtlasSprite(ResPath.GetSettingViewImagePath(), asset);
        }

        // ---------------------------------------------------------------- 显示设置

        private bool IsDisplaySupported()
        {
            return this.displayUiBound;
        }

        /// <summary>
        /// 绑定显示设置：节点缺失、平台不支持时安全跳过
        /// （节点名与 C# 版 SetPanel 保持一致，预制体不用改）。
        /// </summary>
        private void PrepareDisplaySettings()
        {
            this.displayUiBound = false;
            if (this.nameTableDic == null || !this.nameTableDic.Has("node_DisplaySettings"))
            {
                return;
            }

            GameObject displayRoot = this.nameTableDic.GetGameObjectSafely("node_DisplaySettings");

            if (!DisplaySettings.IsSupported)
            {
                if (displayRoot != null)
                {
                    displayRoot.SetActive(false);
                }

                return;
            }

            if (displayRoot == null)
            {
                return;
            }

            displayRoot.SetActive(true);

            this.txtResolution = this.nameTableDic.GetComponentSafely<Text>("Txt_Resolution");
            this.txtDisplayMode = this.nameTableDic.GetComponentSafely<Text>("Txt_DisplayMode");
            if (this.txtResolution == null || this.txtDisplayMode == null)
            {
                Debug.LogWarning("[LuaSetPanelView] 显示设置文本节点缺失，跳过显示设置。");
                return;
            }

            this.resolutionOptions = DisplaySettings.GetAvailableResolutions();
            this.displayModeOptions = DisplaySettings.GetAvailableModes();

            DisplaySettings.Snapshot current = DisplaySettings.LoadOrCreateDefault();
            this.draftResolutionIndex = DisplaySettings.FindResolutionIndex(current.Width, current.Height);
            this.draftModeIndex = DisplaySettings.FindModeIndex(current.Mode);

            this.displayUiBound = true;
            this.RefreshDisplayLabels();
        }

        private void MoveResolution(int delta)
        {
            if (!this.displayUiBound || this.resolutionOptions == null || this.resolutionOptions.Length == 0)
            {
                return;
            }

            int count = this.resolutionOptions.Length;
            this.draftResolutionIndex = ((this.draftResolutionIndex + delta) % count + count) % count;
            this.RefreshDisplayLabels();
        }

        private void MoveMode(int delta)
        {
            if (!this.displayUiBound || this.displayModeOptions == null || this.displayModeOptions.Length == 0)
            {
                return;
            }

            int count = this.displayModeOptions.Length;
            this.draftModeIndex = ((this.draftModeIndex + delta) % count + count) % count;
            this.RefreshDisplayLabels();
        }

        private void ApplyDisplay()
        {
            if (!this.displayUiBound)
            {
                return;
            }

            Resolution resolution = this.resolutionOptions[this.draftResolutionIndex];
            FullScreenMode mode = this.displayModeOptions[this.draftModeIndex];
            DisplaySettings.Apply(
                new DisplaySettings.Snapshot
                {
                    Width = resolution.width,
                    Height = resolution.height,
                    Mode = mode,
                },
                persist: true);
        }

        private void RefreshDisplayLabels()
        {
            if (!this.displayUiBound)
            {
                return;
            }

            Resolution resolution = this.resolutionOptions[this.draftResolutionIndex];
            this.txtResolution.text = string.Format(
                LanguageUtil.Instance.GetString(2001),
                DisplaySettings.FormatResolution(resolution.width, resolution.height));
            this.txtDisplayMode.text = string.Format(
                LanguageUtil.Instance.GetString(2002),
                DisplaySettings.GetModeDisplayName(this.displayModeOptions[this.draftModeIndex]));
        }
    }
}
