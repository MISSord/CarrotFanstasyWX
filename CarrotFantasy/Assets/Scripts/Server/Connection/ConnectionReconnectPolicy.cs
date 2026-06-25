namespace CarrotFantasy
{
    /// <summary>联机断线重连与心跳参数（可在运行时调整）。</summary>
    public static class ConnectionReconnectPolicy
    {
        public const int MaxReconnectAttempts = 6;
        public const float TotalReconnectWindowSeconds = 90f;
        public const float PingIntervalSeconds = 20f;
        public const float PongTimeoutSeconds = 35f;
        public const float TransportConnectTimeoutSeconds = 8f;
        public const float ReloginWaitTimeoutSeconds = 10f;

        /// <summary>每次重连前的退避（秒），不足时沿用最后一档。</summary>
        public static readonly float[] BackoffSeconds = { 1f, 2f, 4f, 8f, 15f, 15f };

        public static float GetBackoffSeconds(int attemptIndex)
        {
            if (attemptIndex < 0)
            {
                return BackoffSeconds[0];
            }

            if (attemptIndex >= BackoffSeconds.Length)
            {
                return BackoffSeconds[BackoffSeconds.Length - 1];
            }

            return BackoffSeconds[attemptIndex];
        }
    }
}
