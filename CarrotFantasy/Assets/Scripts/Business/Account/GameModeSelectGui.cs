using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 进游戏前的单机/联机选择（IMGUI，无 Prefab）。
    /// 由 <see cref="SelectGameModeState"/> 唤起，完成后切到 <see cref="GameState.EnterGame"/>。
    /// </summary>
    public class GameModeSelectGui : MonoBehaviour
    {
        private enum Phase
        {
            SelectMode,
            OnlineLogin,
            Done,
        }

        private Phase phase = Phase.Done;
        private bool visible;
        private bool allowBackToModeSelect = true;
        private string loginPrompt = string.Empty;
        private string account = "test";
        private string password = "123";
        private GameMain gameMain;

        private void Awake()
        {
            this.gameMain = GetComponent<GameMain>();
        }

        public void Show()
        {
            this.visible = true;
            this.phase = Phase.SelectMode;
            this.allowBackToModeSelect = true;
            this.loginPrompt = string.Empty;
        }

        /// <summary>会话失效后仅展示联机登录页。</summary>
        public void ShowOnlineLoginOnly(string message)
        {
            StandaloneGameConfig.EnableStandaloneMode = false;
            this.visible = true;
            this.phase = Phase.OnlineLogin;
            this.allowBackToModeSelect = false;
            this.loginPrompt = message ?? string.Empty;
            this.RemoveLoginListener();
            UIServer.Instance?.ShowTip(this.loginPrompt);
        }

        public void Hide()
        {
            this.visible = false;
            this.phase = Phase.Done;
            this.loginPrompt = string.Empty;
            this.RemoveLoginListener();
        }

        private void OnDestroy()
        {
            this.RemoveLoginListener();
        }

        private void OnGUI()
        {
            if (!this.visible || this.phase == Phase.Done)
            {
                return;
            }

            float panelWidth = 360f;
            float panelHeight = this.phase == Phase.SelectMode ? 190f : (string.IsNullOrEmpty(this.loginPrompt) ? 260f : 300f);
            float x = (Screen.width - panelWidth) * 0.5f;
            float y = (Screen.height - panelHeight) * 0.5f;

            GUILayout.BeginArea(new Rect(x, y, panelWidth, panelHeight), GUI.skin.box);

            if (this.phase == Phase.SelectMode)
            {
                GUILayout.Label("选择游玩方式");
                GUILayout.Space(8f);
                this.DrawSelectMode();
            }
            else if (this.phase == Phase.OnlineLogin)
            {
                GUILayout.Label("联机登录");
                if (!string.IsNullOrEmpty(this.loginPrompt))
                {
                    GUILayout.Label(this.loginPrompt);
                }

                GUILayout.Space(8f);
                this.DrawOnlineLogin();
            }

            GUILayout.EndArea();
        }

        private void DrawSelectMode()
        {
            if (GUILayout.Button("单机游玩", GUILayout.Height(44f)))
            {
                this.EnterStandalone();
            }

            GUILayout.Space(6f);

            if (GUILayout.Button("联机游玩", GUILayout.Height(44f)))
            {
                StandaloneGameConfig.EnableStandaloneMode = false;
                ServerProvision.connectionLifecycle?.StopMonitoring();
                this.phase = Phase.OnlineLogin;
                this.allowBackToModeSelect = true;
                this.loginPrompt = string.Empty;
            }
        }

        private void DrawOnlineLogin()
        {
            GUILayout.Label("账号");
            this.account = GUILayout.TextField(this.account ?? string.Empty);

            GUILayout.Label("密码");
            this.password = GUILayout.PasswordField(this.password ?? string.Empty, '*');

            GUILayout.Space(8f);

            if (GUILayout.Button("进入游戏", GUILayout.Height(40f)))
            {
                this.TryOnlineLogin();
            }

            if (this.allowBackToModeSelect && GUILayout.Button("返回"))
            {
                this.RemoveLoginListener();
                this.phase = Phase.SelectMode;
            }
        }

        private void EnterStandalone()
        {
            ServerProvision.connectionLifecycle?.StopMonitoring();
            StandaloneGameConfig.EnableStandaloneMode = true;
            StandaloneBackendMock.BootstrapDefaultSession();
            this.FinishAndEnterGame();
        }

        private void TryOnlineLogin()
        {
            if (string.IsNullOrEmpty(this.account) || string.IsNullOrEmpty(this.password))
            {
                UIServer.Instance.ShowTip("账号或密码不能为空");
                return;
            }

            this.AddLoginListener();
            ServerProvision.connectionServer.Start();
            AccountServer.Instance.LoginAccount(this.account, this.password);
        }

        private void AddLoginListener()
        {
            AccountServer.Instance.eventDispatcher.RemoveListener(AccountServer.LOGIN_SUCCESS, this.OnLoginSuccess);
            AccountServer.Instance.eventDispatcher.AddListener(AccountServer.LOGIN_SUCCESS, this.OnLoginSuccess);
        }

        private void RemoveLoginListener()
        {
            if (AccountServer.Instance == null)
            {
                return;
            }

            AccountServer.Instance.eventDispatcher.RemoveListener(AccountServer.LOGIN_SUCCESS, this.OnLoginSuccess);
        }

        private void OnLoginSuccess()
        {
            this.RemoveLoginListener();
            ServerProvision.connectionLifecycle?.BeginOnlineSession();
            this.FinishAndEnterGame();
        }

        private void FinishAndEnterGame()
        {
            this.Hide();
            if (this.gameMain != null)
            {
                this.gameMain.ChangeMachineState(GameState.EnterGame);
            }
        }
    }
}
