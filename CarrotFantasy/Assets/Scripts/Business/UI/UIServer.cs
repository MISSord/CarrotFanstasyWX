using System;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class UIServer : BaseServer
    {
        private GameObject nodeObject;
        public AudioManager audioManager;
        public FpsNode fpsNode;

        private GameObject loadingPanelObject;

        private TipUI tipPanel;
        //private String loadingPanelUrl = "Prefabs/Util/LoadingPanel";
        private String tipPanelUrl = "Prefabs/Util/TipPanel";

        private Vector3 fadePosition = new Vector3(2000, 0, 0);
        private Vector3 showPosition = Vector3.zero;

        public static UIServer uiServer;

        public static UIServer Instance
        {
            get
            {
                if (uiServer == null)
                {
                    uiServer = new UIServer();
                }
                return uiServer;
            }
        }

        public override void LoadModule()
        {
            base.LoadModule();
            this.InitGlobalCanvas();
            this.InitAudioManager();
            this.ShowFpsNode();
            this.InitCustomizeShow();
            this.InitResolution();

            this.AddTipPanel();
            this.AddLoadingPanel();
        }

        private void AddLoadingPanel()
        {
            AssetBundleManager.Instance.LoadAsset<GameObject>("ui/view/loadingview_prefab", "LoadingPanel", (GameObject obj) =>
            {
                this.loadingPanelObject = GameObject.Instantiate(obj);
                this.loadingPanelObject.transform.SetParent(this.nodeObject.transform, false);
                this.loadingPanelObject.SetActive(false);
            } );
        }

        public void ShowTip(String tip)
        {
            this.tipPanel.refreshTip(tip);
        }

        public void ShowTipLong(String tip)
        {
            this.tipPanel.showTip(tip);
        }

        public void FadeTipLong()
        {
            this.tipPanel.fadeTip();
        }

        public void ShowLoadingPanel()
        {
            this.loadingPanelObject.SetActive(true);
        }

        public void FadeLoadingPanel()
        {
            this.loadingPanelObject.SetActive(false);
        }

        private void AddTipPanel()
        {
            AssetBundleManager.Instance.LoadAsset<GameObject>("ui/view/tipview_prefab", "TipPanel", (GameObject obj) =>
            {
                GameObject pan = GameObject.Instantiate(obj);
                pan.transform.SetParent(this.nodeObject.transform, false);
                this.tipPanel = new TipUI(pan);
            });
        }

        private void InitGlobalCanvas()
        {
            this.nodeObject = new GameObject("global_canvas");
            this.nodeObject.layer = SceneLayerData.layerType[1];
            Canvas canvas = this.nodeObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = ServerProvision.sceneServer.GetUICamera();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;

            CanvasScaler canvasScaler = nodeObject.AddComponent<CanvasScaler>();
            UIUtil.Instance.InitCanvasScale(canvasScaler);

            GraphicRaycaster graphic = nodeObject.AddComponent<GraphicRaycaster>();
        }

        private void AddToGlobalUI(GameObject res)
        {
            res.transform.SetParent(this.nodeObject.transform, false);
        }

        private void InitAudioManager()
        {
            audioManager = new AudioManager();
            audioManager.Init();
            this.AddToGlobalUI(audioManager.nodeObject);
        }

        private void ShowFpsNode()
        {
            fpsNode = new FpsNode();
        }

        private void InitCustomizeShow()
        {

        }

        private void InitResolution()
        {
            /*
            int height = UnityEngine.Screen.height;
            if(height >= 1440)
            {
                height = 1440;
            }
            else if(height >= 1080)
            {
                height = 1080;
            }
            else
            {
                height = 720;
            }
            //UnityEngine.Screen.SetResolution((int)(UIUtil.Instance.SCREEN_RADIO * height), height, false);
            UIUtil.Instance.curSetScreenSize = new Vector2((int)(GameConfig.DEVELOPMENT_SCREEN_PROP * (float)height), height);
            UnityEngine.Screen.SetResolution((int)(GameConfig.DEVELOPMENT_SCREEN_PROP * (float)height), height, false);
            */
            UIUtil.Instance.curSetScreenSize = new Vector2(1920, 1440);
            UnityEngine.Screen.SetResolution(1920, 1440, false);
        }

        public override void Dispose()
        {
            base.Dispose();
            if (this.audioManager != null)
            {
                this.audioManager.dipose();
            }
            GameObject.Destroy(this.nodeObject);
        }

        public void PlayMainBg()
        {
            this.audioManager.playMusic("AudioClips/Main/BGMusic");
        }

        public void PlayButtonEffect()
        {
            this.audioManager.playEffect("AudioClips/Main/Button");
        }

        public void PlayPagingEffect()
        {
            this.audioManager.playEffect("AudioClips/Main/Paging");
        }
    }
}
