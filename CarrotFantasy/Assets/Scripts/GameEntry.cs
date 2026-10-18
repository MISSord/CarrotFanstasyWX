using System.Collections;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 游戏业务入口：承接资源更新完成后的业务初始化与主循环 Tick。
    /// </summary>
    public sealed class GameEntry : IGameEntry
    {
        private GameMain gameMain;
        private GameStateMachine gameStateMachine;

        public bool IsQuitRequested =>
            BusinessProvision.Instance != null && BusinessProvision.Instance.IsGameQuit;

        public void Start(GameObject host)
        {
            if (host == null)
            {
                Debug.LogError("[GameEntry] host 为空");
                return;
            }

            this.gameMain = host.GetComponent<GameMain>();
            if (this.gameMain == null)
            {
                Debug.LogError("[GameEntry] host 上缺少 GameMain");
                return;
            }

            this.gameStateMachine = new GameStateMachine();
            AssetBundleLoaders.Init();

            ServerProvision.Instance.Init();
            BusinessProvision.Instance.Init();
            BusinessProvision.Instance.LoadBusiness();

#if CF_DEV_TOOLS || UNITY_EDITOR
            RuntimeGmConsole.EnsureInstalled();
#endif

            if (host.GetComponent<GameModeSelectGui>() == null)
            {
                host.AddComponent<GameModeSelectGui>();
            }

            this.gameMain.RunCoroutine(this.BootstrapMainScene());
        }

        public void Tick(float deltaTime)
        {
            ViewManager.Instance?.Update();
            AssetBundleLoaders.Tick();
            this.gameStateMachine?.Update(deltaTime);
            Sche.Tick(new Fix64(deltaTime));
            ServerProvision.battleSessionHost?.Tick(deltaTime);
            ServerProvision.connectionLifecycle?.Tick(deltaTime);
        }

        public void ChangeState(GameState state)
        {
            this.gameStateMachine?.ChangeState(state);
        }

        private IEnumerator BootstrapMainScene()
        {
            bool loaded = false;
            bool success = false;
            ServerProvision.sceneServer.LoadScene(
                BaseSceneType.MainScene,
                null,
                ok =>
                {
                    success = ok;
                    loaded = true;
                });

            while (!loaded)
            {
                yield return null;
            }

            if (!success)
            {
                Debug.LogError("[GameEntry] MainScene 加载失败，游戏流程未启动。");
                yield break;
            }

            this.gameStateMachine.Init(this.gameMain);
        }
    }
}
