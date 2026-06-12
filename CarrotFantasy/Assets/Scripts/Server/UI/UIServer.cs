using System;
using UnityEngine;

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
        public Vector2 curSetScreenSize = new Vector2(1920, 1440);

        public override void LoadModule()
        {
            base.LoadModule();
            this.InitGlobalCanvas();
            this.InitAudioManager();

            this.AddTipPanel();
            this.AddLoadingPanel();
        }

        private void AddLoadingPanel()
        {

        }

        public void ShowTip(String tip)
        {
            this.tipPanel.RefreshTip(tip);
        }

        public void ShowTipLong(String tip)
        {
            this.tipPanel.ShowTip(tip);
        }

        public void FadeTipLong()
        {
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
            //AssetBundleManager.Instance.LoadAsset<GameObject>("ui/view/tipview_prefab", "TipPanel", (GameObject obj) =>
            //{
            //    GameObject pan = GameObject.Instantiate(obj);
            //    pan.transform.SetParent(this.nodeObject.transform, false);
            //    this.tipPanel = new TipView(pan);
            //});
        }

        private void InitGlobalCanvas()
        {

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
            GameObject.Destroy(this.nodeObject);
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
