using System;

namespace CarrotFantasy
{
    public class CommonEventType
    {
        public const String ACCOUNT_LOGIN_SUCCESS = "Account_login_success";

        public const String RETURN_TO_MAIN_SCENE = "Return_To_Main_Scene";

        /// <summary>进入肉鸽大地图（Scene.unity）。无参时由 <see cref="RoguelikeMapServer"/> 进最近选关或 1-1。</summary>
        public const String ENTER_ROGUELIKE_MAP = "Enter_Roguelike_Map";

        public const String GAME_QUIT = "Game_Quit";
    }
}
