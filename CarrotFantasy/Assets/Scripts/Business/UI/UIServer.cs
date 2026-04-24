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
        private String loadingPanelUrl = "Prefabs/Util/LoadingPanel";
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
            this.initGlobalCanvas();
            this.initAudioManager();
            this.showFpsNode();
            this.initCustomizeShow();
            this.initResolution();

            this.addTipPanel();
            this.addLoadingPanel();
        }

        private void addLoadingPanel()
        {
            GameObject panel = ResourceLoader.Instance.getGameObject(loadingPanelUrl);
            this.loadingPanelObject = GameObject.Instantiate(panel);
            this.loadingPanelObject.transform.SetParent(this.nodeObject.transform, false);
            this.loadingPanelObject.SetActive(false);
        }

        public void showTip(String tip)
        {
            this.tipPanel.refreshTip(tip);
        }

        public void showTipLong(String tip)
        {
            this.tipPanel.showTip(tip);
        }

        public void fadeTipLong()
        {
            this.tipPanel.fadeTip();
        }

        public void showLoadingPanel()
        {
            this.loadingPanelObject.SetActive(true);
        }

        public void fadeLoadingPanel()
        {
            this.loadingPanelObject.SetActive(false);
        }

        private void addTipPanel()
        {
            GameObject panel = ResourceLoader.Instance.getGameObject(tipPanelUrl);
            GameObject pan = GameObject.Instantiate(panel);
            pan.transform.SetParent(this.nodeObject.transform, false);
            this.tipPanel = new TipUI(pan);
        }

        private void initGlobalCanvas()
        {
            this.nodeObject = new GameObject("global_canvas");
            this.nodeObject.layer = SceneLayerData.layerType[1];
            Canvas canvas = this.nodeObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = ServerProvision.sceneServer.GetUICamera();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;

            CanvasScaler canvasScaler = nodeObject.AddComponent<CanvasScaler>();
            UIUtil.Instance.initCanvasScale(canvasScaler);

            GraphicRaycaster graphic = nodeObject.AddComponent<GraphicRaycaster>();
        }

        private void addToGlobalUI(GameObject res)
        {
            res.transform.SetParent(this.nodeObject.transform, false);
        }

        private void initAudioManager()
        {
            audioManager = new AudioManager();
            audioManager.init();
            this.addToGlobalUI(audioManager.nodeObject);
        }

        private void showFpsNode()
        {
            fpsNode = new FpsNode();
        }

        private void initCustomizeShow()
        {

        }

        private void initResolution()
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

        public void playMainBg()
        {
            this.audioManager.playMusic("AudioClips/Main/BGMusic");
        }

        public void playButtonEffect()
        {
            this.audioManager.playEffect("AudioClips/Main/Button");
        }

        public void playPagingEffect()
        {
            this.audioManager.playEffect("AudioClips/Main/Paging");
        }
    }
}
