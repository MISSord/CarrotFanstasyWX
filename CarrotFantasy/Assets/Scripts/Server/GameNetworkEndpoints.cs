namespace CarrotFantasy
{
    /// <summary>
    /// 与 CarrotFantasyServer 联调时的默认入口；发布前可改为配置表或 CDN 下发。
    /// </summary>
    public static class GameNetworkEndpoints
    {
        /// <summary>须包含路径（例如 /ws），与 ASP.NET Map("/ws") 一致。</summary>
        public static string WebSocketUrl { get; set; } = "ws://localhost:5003/ws";
    }
}
