namespace CarrotFantasy
{
    /// <summary>
    /// 与 CarrotFantasyServer <c>SimpleOpcodes</c> 对齐的简单协议号（ushort）。
    /// </summary>
    public static class SimpleBinaryOpcodes
    {
        public const ushort Ping = 1;
        public const ushort Pong = 2;
        public const ushort EchoUtf8 = 3;
        public const ushort EchoUtf8Reply = 4;
        public const ushort DemoStructuredRequest = 100;
        public const ushort DemoStructuredReply = 101;

        /// <summary>登录请求 C→S，负载为 Protobuf <c>CfNet.LoginRequest</c>。</summary>
        public const ushort LoginRequest = 200;

        /// <summary>登录响应 S→C，负载为 Protobuf <c>CfNet.LoginResponse</c>。</summary>
        public const ushort LoginResponse = 201;
    }
}
