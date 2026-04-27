using System;

namespace CarrotFantasy
{
    public class PanelEventType
    {
        public const String OPEN_PANEL_PREPARE = "open_panel_prepare";
        public const String OPEN_PANEL_OPENING = "";
        public const String OPEN_PANEL_FINISH = "";

        public const String CLOSE_PANEL_PREPARE = "open_panel_prepare";
        public const String CLOSE_PANEL_FINISH = "";
        //public const String ON_OPEN_PANEL = "on_open_panel";
        //public const String ON_OPEN_PANEL = "on_open_panel";
    }

    public class PanelCloseReasonType
    {
        public const int DEFAULT = 0;
        public const int SCENE_CHANGE = 1;
        public const int OTHER = 2;
    }
}
