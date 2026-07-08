using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>联机会话失效后：结束战斗、回主场景、关 UI、打开登录页。</summary>
    public static class OnlineSessionRecovery
    {
        public static void ReturnToOnlineLogin(string message)
        {
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
                // LoadScene → RemoveScene → BattleScene.Dispose → Shutdown，不在此先行 Shutdown。
                sceneServer.LoadScene(BaseSceneType.MainScene, null, _ => ShowLoginGui(message));
                return;
            }

            ClearStaleBattleSessionIfAny();
            ShowLoginGui(message);
        }

        static void ClearStaleBattleSessionIfAny()
        {
            if (ServerProvision.battleSessionHost?.HasActiveSession == true)
            {
                ServerProvision.battleSessionHost.Shutdown();
            }
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
