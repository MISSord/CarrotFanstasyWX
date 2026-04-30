using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{

    public class PanelCloseReasonType
    {
        public const int DEFAULT = 0;
        public const int SCENE_CHANGE = 1;
        public const int OTHER = 2;
    }
    public class UIUtil
    {
        private static UIUtil uiUtil;
        public Vector2 currentScreenSize;
        public Vector2 curSetScreenSize { get; set; }
        private Vector2 REAL_REFERENCE_RESOLUTION = Vector2.zero;
        public float SCREEN_RADIO;

        public static UIUtil Instance
        {
            get
            {
                if (uiUtil == null)
                {
                    uiUtil = new UIUtil();
                }
                return uiUtil;
            }
        }

        public void Init()
        {
            SCREEN_RADIO = (float)UnityEngine.Screen.width / (float)UnityEngine.Screen.height;
            currentScreenSize = new Vector2(UnityEngine.Screen.width, UnityEngine.Screen.height);
        }

        public void InitCanvasScale(CanvasScaler canvasScale) //初始化屏幕
        {
            canvasScale.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScale.matchWidthOrHeight = GetMatchWidthOrHeighRatio();
            canvasScale.referenceResolution = GameConfig.DEVELOPMENT_SCREEN_SIZE;
        }

        public int GetMatchWidthOrHeighRatio()
        {
            if (SCREEN_RADIO > 1.75)
            {
                return 0;
            }
            else
            {
                return 1;
            }
        }

        public Vector2 GetReferenceResolution()
        {
            if (REAL_REFERENCE_RESOLUTION == Vector2.zero)
            {
                int radio = this.GetMatchWidthOrHeighRatio();
                if (radio == 0)
                {
                    REAL_REFERENCE_RESOLUTION = new Vector2(GameConfig.DEVELOPMENT_SCREEN_SIZE.x, GameConfig.DEVELOPMENT_SCREEN_SIZE.x * SCREEN_RADIO);
                }
                else
                {
                    REAL_REFERENCE_RESOLUTION = new Vector2(GameConfig.DEVELOPMENT_SCREEN_SIZE.y * SCREEN_RADIO, GameConfig.DEVELOPMENT_SCREEN_SIZE.y * SCREEN_RADIO);
                }
            }
            return REAL_REFERENCE_RESOLUTION;
        }

    }
}
