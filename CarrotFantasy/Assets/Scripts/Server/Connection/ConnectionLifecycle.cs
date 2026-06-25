using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 联机会话：应用层心跳（Ping/Pong）、断线退避重连、重登；多次失败后退回登录界面。
    /// 单机模式不启用。
    /// </summary>
    public sealed class ConnectionLifecycle
    {
        private enum ReconnectPhase
        {
            None,
            WaitingBackoff,
            WaitingTransport,
            WaitingLogin,
        }

        private ConnectionServer connectionServer;
        private bool monitoring;
        private bool authenticated;
        private float pingTimer;
        private float lastPongRealtime;
        private int reconnectAttempt;
        private float reconnectWindowStartRealtime;
        private ReconnectPhase reconnectPhase;
        private float phaseTimer;
        private bool showedReconnectTip;

        public bool IsMonitoring => this.monitoring;
        public bool IsReconnecting => this.monitoring && this.reconnectPhase != ReconnectPhase.None;

        public void Init(ConnectionServer server)
        {
            this.connectionServer = server;
            if (this.connectionServer == null)
            {
                return;
            }

            this.connectionServer.AddListener(SimpleBinaryOpcodes.Pong, this.OnPong);
            this.connectionServer.TransportConnected += this.OnTransportConnected;
            this.connectionServer.TransportDisconnected += this.OnTransportDisconnected;

            AccountServer.Instance.eventDispatcher.AddListener(AccountServer.LOGIN_SUCCESS, this.OnLoginSuccess);
            AccountServer.Instance.eventDispatcher.AddListener(AccountServer.LOGIN_FAILED, this.OnLoginFailed);
        }

        public void Dispose()
        {
            this.StopMonitoring();

            if (this.connectionServer != null)
            {
                this.connectionServer.RemoveListener(SimpleBinaryOpcodes.Pong, this.OnPong);
                this.connectionServer.TransportConnected -= this.OnTransportConnected;
                this.connectionServer.TransportDisconnected -= this.OnTransportDisconnected;
            }

            if (AccountServer.Instance != null)
            {
                AccountServer.Instance.eventDispatcher.RemoveListener(AccountServer.LOGIN_SUCCESS, this.OnLoginSuccess);
                AccountServer.Instance.eventDispatcher.RemoveListener(AccountServer.LOGIN_FAILED, this.OnLoginFailed);
            }
        }

        /// <summary>联机登录成功后调用，开始心跳与断线检测。</summary>
        public void BeginOnlineSession()
        {
            if (StandaloneGameConfig.EnableStandaloneMode)
            {
                return;
            }

            this.monitoring = true;
            this.authenticated = true;
            this.reconnectPhase = ReconnectPhase.None;
            this.reconnectAttempt = 0;
            this.reconnectWindowStartRealtime = 0f;
            this.showedReconnectTip = false;
            this.pingTimer = 0f;
            this.lastPongRealtime = Time.realtimeSinceStartup;
            Debug.Log("[ConnectionLifecycle] 联机会话监控已启动。");
        }

        public void StopMonitoring()
        {
            this.monitoring = false;
            this.authenticated = false;
            this.reconnectPhase = ReconnectPhase.None;
            this.phaseTimer = 0f;
            this.showedReconnectTip = false;
        }

        public void Tick(float deltaSeconds)
        {
            if (!this.monitoring || StandaloneGameConfig.EnableStandaloneMode)
            {
                return;
            }

            if (this.reconnectPhase != ReconnectPhase.None)
            {
                this.TickReconnect(deltaSeconds);
                return;
            }

            if (!this.authenticated)
            {
                return;
            }

            this.pingTimer += deltaSeconds;
            if (this.pingTimer >= ConnectionReconnectPolicy.PingIntervalSeconds)
            {
                this.pingTimer = 0f;
                this.SendPing();
            }

            if (Time.realtimeSinceStartup - this.lastPongRealtime > ConnectionReconnectPolicy.PongTimeoutSeconds)
            {
                Debug.LogWarning("[ConnectionLifecycle] 心跳超时，判定连接断开。");
                this.BeginReconnect("心跳超时");
            }
        }

        private void TickReconnect(float deltaSeconds)
        {
            this.phaseTimer -= deltaSeconds;
            if (this.phaseTimer > 0f)
            {
                if (this.reconnectPhase == ReconnectPhase.WaitingTransport
                    && this.connectionServer != null
                    && this.connectionServer.IsTransportConnected)
                {
                    this.BeginReloginWait();
                }

                return;
            }

            switch (this.reconnectPhase)
            {
                case ReconnectPhase.WaitingBackoff:
                    this.StartReconnectTransport();
                    break;
                case ReconnectPhase.WaitingTransport:
                    Debug.LogWarning("[ConnectionLifecycle] 重连传输层超时。");
                    this.OnReconnectAttemptFailed();
                    break;
                case ReconnectPhase.WaitingLogin:
                    Debug.LogWarning("[ConnectionLifecycle] 重登超时。");
                    this.OnReconnectAttemptFailed();
                    break;
            }
        }

        private void SendPing()
        {
            if (this.connectionServer == null || !this.connectionServer.IsTransportConnected)
            {
                return;
            }

            this.connectionServer.Send(SimpleBinaryOpcodes.Ping);
        }

        private void OnPong(byte[] payload)
        {
            this.lastPongRealtime = Time.realtimeSinceStartup;
        }

        private void OnTransportConnected()
        {
            if (!this.monitoring || this.reconnectPhase != ReconnectPhase.WaitingTransport)
            {
                return;
            }

            this.BeginReloginWait();
        }

        private void OnTransportDisconnected()
        {
            if (!this.monitoring || !this.authenticated || this.reconnectPhase != ReconnectPhase.None)
            {
                return;
            }

            this.BeginReconnect("连接断开");
        }

        private void BeginReconnect(string reason)
        {
            if (!this.monitoring || this.reconnectPhase != ReconnectPhase.None)
            {
                return;
            }

            this.authenticated = false;
            this.reconnectAttempt = 0;
            this.reconnectWindowStartRealtime = Time.realtimeSinceStartup;
            this.reconnectPhase = ReconnectPhase.WaitingBackoff;
            this.phaseTimer = 0f;

            if (!this.showedReconnectTip)
            {
                this.showedReconnectTip = true;
                UIServer.Instance?.ShowTip("网络异常，正在重连…");
            }

            Debug.LogWarning("[ConnectionLifecycle] 开始重连: " + reason);
            this.ScheduleBackoffAndReconnect();
        }

        private void ScheduleBackoffAndReconnect()
        {
            if (this.ShouldGiveUp())
            {
                this.GiveUpAndReturnToLogin("连接失败，请重新登录");
                return;
            }

            float backoff = ConnectionReconnectPolicy.GetBackoffSeconds(this.reconnectAttempt);
            this.reconnectPhase = ReconnectPhase.WaitingBackoff;
            this.phaseTimer = backoff;
            Debug.Log(string.Format("[ConnectionLifecycle] 第 {0} 次重连将在 {1:F1}s 后开始", this.reconnectAttempt + 1, backoff));
        }

        private void StartReconnectTransport()
        {
            this.reconnectAttempt++;

            if (this.connectionServer != null)
            {
                this.connectionServer.StopConnection();
                this.connectionServer.Start();
            }

            if (this.connectionServer != null && this.connectionServer.IsTransportConnected)
            {
                this.BeginReloginWait();
                return;
            }

            this.reconnectPhase = ReconnectPhase.WaitingTransport;
            this.phaseTimer = ConnectionReconnectPolicy.TransportConnectTimeoutSeconds;
        }

        private void BeginReloginWait()
        {
            if (!AccountServer.Instance.TryReloginWithCachedCredentials())
            {
                Debug.LogWarning("[ConnectionLifecycle] 无缓存凭证，无法重登。");
                this.GiveUpAndReturnToLogin("登录信息已失效，请重新登录");
                return;
            }

            this.reconnectPhase = ReconnectPhase.WaitingLogin;
            this.phaseTimer = ConnectionReconnectPolicy.ReloginWaitTimeoutSeconds;
        }

        private void OnReconnectAttemptFailed()
        {
            if (this.ShouldGiveUp())
            {
                this.GiveUpAndReturnToLogin("多次重连失败，请重新登录");
                return;
            }

            this.ScheduleBackoffAndReconnect();
        }

        private bool ShouldGiveUp()
        {
            if (this.reconnectAttempt >= ConnectionReconnectPolicy.MaxReconnectAttempts)
            {
                return true;
            }

            if (this.reconnectWindowStartRealtime > 0f
                && Time.realtimeSinceStartup - this.reconnectWindowStartRealtime
                    > ConnectionReconnectPolicy.TotalReconnectWindowSeconds)
            {
                return true;
            }

            return false;
        }

        private void OnLoginSuccess()
        {
            if (!this.monitoring)
            {
                return;
            }

            if (this.reconnectPhase == ReconnectPhase.WaitingLogin)
            {
                this.reconnectPhase = ReconnectPhase.None;
                this.authenticated = true;
                this.reconnectAttempt = 0;
                this.reconnectWindowStartRealtime = 0f;
                this.showedReconnectTip = false;
                this.lastPongRealtime = Time.realtimeSinceStartup;
                this.pingTimer = 0f;
                UIServer.Instance?.ShowTip("重连成功");
                Debug.Log("[ConnectionLifecycle] 重连并重登成功。");
                return;
            }

            if (this.reconnectPhase == ReconnectPhase.None && !this.authenticated)
            {
                this.authenticated = true;
                this.lastPongRealtime = Time.realtimeSinceStartup;
            }
        }

        private void OnLoginFailed()
        {
            if (!this.monitoring || this.reconnectPhase != ReconnectPhase.WaitingLogin)
            {
                return;
            }

            Debug.LogWarning("[ConnectionLifecycle] 重登失败。");
            this.OnReconnectAttemptFailed();
        }

        private void GiveUpAndReturnToLogin(string message)
        {
            Debug.LogWarning("[ConnectionLifecycle] 放弃重连: " + message);
            this.StopMonitoring();
            AccountServer.Instance.ForceLogout(message);
        }
    }
}
