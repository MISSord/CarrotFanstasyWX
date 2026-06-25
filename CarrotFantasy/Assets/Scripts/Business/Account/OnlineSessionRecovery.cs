using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>联机会话失效后：结束战斗、回主场景、关 UI、打开登录页。</summary>
    public static class OnlineSessionRecovery
    {
        public static void ReturnToOnlineLogin(string message)
        {
            ServerProvision.battleSessionHost?.EndSession(clearLaunchParams: true, destroyViewHierarchy: true);

            MapServer mapServer = MapServer.Instance;
            if (mapServer != null)
            {
                mapServer.ClearPendingProgressOnSessionExpired();
            }

            SceneServer sceneServer = ServerProvision.sceneServer;
            if (sceneServer != null
                && sceneServer.GetCurScene() != null
                && sceneServer.GetCurScene().sceneType != BaseSceneType.MainScene)
            {
                sceneServer.LoadScene(BaseSceneType.MainScene, null, _ => ShowLoginGui(message));
                return;
            }

            ViewManager.Instance?.CloseAllOpenViews();
            ShowLoginGui(message);
        }

        static void ShowLoginGui(string message)
        {
            ViewManager.Instance?.CloseAllOpenViews();

            GameMain main = Object.FindObjectOfType<GameMain>();
            if (main == null)
            {
                return;
            }

            GameModeSelectGui gui = main.GetComponent<GameModeSelectGui>();
            if (gui == null)
            {
                gui = main.gameObject.AddComponent<GameModeSelectGui>();
            }

            gui.ShowOnlineLoginOnly(message);
        }
    }
}
