using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CarrotFantasy
{
    public class PanelCloseReasonType
    {
        public const int DEFAULT = 0;
        public const int SCENE_CHANGE = 1;
        public const int OTHER = 2;
    }

    public class UIServer : BaseServer<UIServer>
    {
        private GameObject nodeObject;
        private GameObject loadingPanelObject;
        private TipView tipPanel;
        private bool tipPanelLoadRequested;
        private string pendingTip;
        private int tipPrefabHandle = PrefabResourceManager.InvalidHandle;
        public Vector2 curSetScreenSize = new Vector2(1920, 1440);

        public override void LoadModule()
        {
            base.LoadModule();
            this.InitGlobalCanvas();
            this.InitAudioManager();
        }

        /// <summary>清单就绪后再加载 Tip 等全局 AB UI（避免早于 SetAssetBundleItem）。</summary>
        public void TryLoadDeferredAbUi()
        {
            if (this.tipPanel != null || this.tipPanelLoadRequested)
            {
                return;
            }

            if (AssetBundleManager.Instance == null || !AssetBundleManager.Instance.HasManifest)
            {
                return;
            }

            this.tipPanelLoadRequested = true;
            this.AddTipPanel();
        }

        private void AddLoadingPanel()
        {

        }

        public void ShowTip(String tip)
        {
            if (this.tipPanel == null)
            {
                this.pendingTip = tip;
                return;
            }

            this.tipPanel.RefreshTip(tip);
        }

        public void ShowTipLong(String tip)
        {
            if (this.tipPanel == null)
            {
                this.pendingTip = tip;
                return;
            }

            this.tipPanel.ShowTip(tip);
        }

        public void FadeTipLong()
        {
            if (this.tipPanel == null)
            {
                return;
            }

            this.tipPanel.FadeTip();
        }

        public void ShowLoadingPanel()
        {
            //this.loadingPanelObject.SetActive(true);
        }

        public void FadeLoadingPanel()
        {
            //this.loadingPanelObject.SetActive(false);
        }

        private void AddTipPanel()
        {
            this.tipPrefabHandle = PrefabResourceManager.Instance.Load(
                UiViewAbPaths.TipViewPrefab,
                UiViewAbPaths.TipPanelAsset,
                this.OnTipPanelLoaded,
                LoadPriority.High);
        }

        void OnTipPanelLoaded(GameObject template)
        {
            if (template == null)
            {
                Debug.LogError("[UIServer] TipPanel 加载失败");
                if (this.tipPrefabHandle != PrefabResourceManager.InvalidHandle)
                {
                    PrefabResourceManager.Instance.Unload(this.tipPrefabHandle);
                    this.tipPrefabHandle = PrefabResourceManager.InvalidHandle;
                }

                this.tipPanelLoadRequested = false;
                return;
            }

            if (this.nodeObject == null)
            {
                this.InitGlobalCanvas();
            }

            GameObject pan = GameObject.Instantiate(template);
            pan.transform.SetParent(this.nodeObject.transform, false);
            pan.transform.SetAsLastSibling();
            this.tipPanel = new TipView(pan);

            if (!string.IsNullOrEmpty(this.pendingTip))
            {
                this.tipPanel.RefreshTip(this.pendingTip);
                this.pendingTip = null;
            }
        }

        private void InitGlobalCanvas()
        {
            GameObject uiRoot = ViewManager.Instance != null ? ViewManager.Instance.GetUIRoot() : null;
            if (uiRoot == null)
            {
                uiRoot = UIPresentationPersistence.EnsureGlobalUiLayer();
            }

            this.nodeObject = new GameObject("GlobalUiNode");
            this.nodeObject.transform.SetParent(uiRoot.transform, false);
        }

        private void AddToGlobalUI(GameObject res)
        {
            res.transform.SetParent(this.nodeObject.transform, false);
        }

        private void InitAudioManager()
        {
            //audioManager = new AudioManager();
            //audioManager.Init();
            //this.AddToGlobalUI(audioManager.nodeObject);
        }


        public override void Dispose()
        {
            base.Dispose();
            if (this.nodeObject != null)
            {
                GameObject.Destroy(this.nodeObject);
                this.nodeObject = null;
            }

            if (this.tipPrefabHandle != PrefabResourceManager.InvalidHandle)
            {
                PrefabResourceManager.Instance.Unload(this.tipPrefabHandle);
                this.tipPrefabHandle = PrefabResourceManager.InvalidHandle;
            }

            this.tipPanel = null;
            this.pendingTip = null;
            this.tipPanelLoadRequested = false;
        }

        public void PlayMainBg()
        {
            //AudioManager.Instance.PlayMusic("AudioClips/Main/BGMusic");
        }

        public void PlayButtonEffect()
        {
            //AudioManager.Instance.PlayEffect("AudioClips/Main/Button");
        }

        public void PlayPagingEffect()
        {
            //AudioManager.Instance.PlayEffect("AudioClips/Main/Paging");
        }
    }
}
