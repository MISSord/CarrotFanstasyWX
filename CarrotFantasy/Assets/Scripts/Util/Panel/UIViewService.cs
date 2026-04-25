using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary> 替代 PanelServer.ShowPanel，统一用 BaseView + AssetBundle 打开。 </summary>
    public static class UIViewService
    {
        private static void OpenPrepare(string logicalPanelName)
        {
            if (ServerProvision.panelServer == null) return;
            var msg = new Dictionary<String, System.Object>
            {
                { "panelName", logicalPanelName },
                { "enableShow", true },
                { "reason", "" }
            };
            ServerProvision.panelServer.eventDispatcher.DispatchEvent(PanelEventType.OPEN_PANEL_PREPARE, msg);
        }

        public static void OpenMainPanel()
        {
            OpenPrepare("MainPanel");
            MainPanel.Instance.RegisterData();
            MainPanel.Instance.Open(0);
        }

        public static void OpenSetPanel()
        {
            OpenPrepare("SetPanel");
            SetPanel.Instance.RegisterData();
            SetPanel.Instance.Open(0);
        }

        public static void OpenHelpPanel()
        {
            OpenPrepare("HelpPanel");
            HelpPanel.Instance.RegisterData();
            HelpPanel.Instance.Open(0);
        }

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

        public static void OpenStartLoadPanel()
        {
            OpenPrepare("StartLoadPanel");
            StartLoadPanel.Instance.RegisterData();
            StartLoadPanel.Instance.Open(0);
        }

        public static void OpenNormalModelPanel()
        {
            OpenPrepare("NormalModelPanel");
            NormalModelPanel.Instance.RegisterData();
            NormalModelPanel.Instance.Open(0);
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
