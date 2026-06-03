using System;

namespace CarrotFantasy
{
    public class CommonEventType
    {
        public const String ACCOUNT_LOGIN_SUCCESS = "Account_login_success";
        public const String READY_START_PVE_GAME = "Ready_Start_PVE_Game";
        public const String READY_START_PVP_GAME = "Ready_Start_PVP_Game";

        public const String RETURN_TO_MAIN_SCENE = "Return_To_Main_Scene";

        /// <summary>进入肉鸽大地图（Scene.unity）。</summary>
        public const String ENTER_ROGUELIKE_MAP = "Enter_Roguelike_Map";

        public const String GAME_QUIT = "Game_Quit";
    }
}
