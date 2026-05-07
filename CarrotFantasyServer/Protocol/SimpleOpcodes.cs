namespace CarrotFantasyServer.Protocol;

/// <summary>
/// 与客户端自行约定的协议号（ ushort ）。可按业务扩展，两端枚举保持一致即可。
/// </summary>
public static class SimpleOpcodes
{
    /// <summary>心跳请求，负载可为空。</summary>
    public const ushort Ping = 1;

    /// <summary>心跳响应，负载可为空。</summary>
    public const ushort Pong = 2;

    /// <summary>演示：负载为 UTF-8 文本。</summary>
    public const ushort EchoUtf8 = 3;

    /// <summary>演示：EchoUtf8 的响应，负载为 UTF-8。</summary>
    public const ushort EchoUtf8Reply = 4;

    /// <summary>演示：结构化负载请求（见服务端 HandleDemoStructured）。</summary>
    public const ushort DemoStructuredRequest = 100;

    /// <summary>演示：结构化负载响应。</summary>
    public const ushort DemoStructuredReply = 101;

    /// <summary>登录请求 C→S。</summary>
    public const ushort LoginRequest = 200;

    /// <summary>登录响应 S→C。</summary>
    public const ushort LoginResponse = 201;
}
