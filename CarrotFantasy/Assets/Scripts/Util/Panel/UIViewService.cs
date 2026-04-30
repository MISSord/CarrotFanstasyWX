using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary> 统一用 BaseView + AssetBundle 打开。 </summary>
    public static class UIViewService
    {
        private static void OpenPrepare(string logicalPanelName)
        {
            if (ViewManager.Instance == null || ViewManager.Instance.eventDispatcher == null) return;
            var msg = new Dictionary<String, System.Object>
            {
                { "panelName", logicalPanelName },
                { "enableShow", true },
                { "reason", "" }
            };
            ViewManager.Instance.eventDispatcher.DispatchEvent(PanelEventType.OPEN_PANEL_PREPARE, msg);
        }

        //public static void OpenMainPanel()
        //{
        //    OpenPrepare("MainPanel");
        //    MainPanel.Instance.RegisterData();
        //    MainPanel.Instance.Open(0);
        //}

        public static void OpenMapBigLevelPanel()
        {
            OpenPrepare("MapBigLevelPanel");
            MapBigLevelPanel.Instance.RegisterData();
            MapBigLevelPanel.Instance.Open(0);
        }

        public static void OpenMapNormalLevelPanel(int bigLevelId)
        {
            OpenPrepare("MapNormalLevelPanel");
            var v = MapNormalLevelPanel.Instance;
            v.SetBigLevel(bigLevelId);
            v.RegisterData();
            v.Open(0);
        }

        public static void OpenRoomPanel()
        {
            OpenPrepare("RoomPanel");
            RoomPanel.Instance.RegisterData();
            RoomPanel.Instance.Open(0);
        }

        public static void OpenLoginPanel()
        {
            OpenPrepare("LoginPanel");
            LoginPanel.Instance.RegisterData();
            LoginPanel.Instance.Open(0);
        }

        public static void CloseAllViews()
        {
            if (ViewManager.Instance != null)
            {
                ViewManager.Instance.CloseAllOpenViews();
            }
        }
    }
}
