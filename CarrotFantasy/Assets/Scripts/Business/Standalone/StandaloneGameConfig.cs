namespace CarrotFantasy
{
    /// <summary>
    /// 单机模式开关。开启后不连接服务端，由 <see cref="StandaloneBackendMock"/> 提供默认登录与地图数据。
    /// </summary>
    public static class StandaloneGameConfig
    {
#if UNITY_EDITOR
        /// <summary>Editor 下默认开启单机，便于本地调试。</summary>
        public static bool EnableStandaloneMode = true;
#else
        public static bool EnableStandaloneMode = false;
#endif
    }
}
