using System.Net.WebSockets;
using CarrotFantasyServer;
using CarrotFantasyServer.PostLogin;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss ");

// 每个 WebSocket 连接独立实例，便于保存登录后的 userId 等会话状态
builder.Services.AddTransient<GameConnectionHandler>();

// 登录成功后在 LoginResponse 之外按顺序主动下发的帧（可增删 IPostLoginPushContributor 实现）
builder.Services.AddSingleton<IPostLoginPushContributor, UserMapPostLoginPushContributor>();
builder.Services.AddSingleton<PostLoginPushPipeline>();

var app = builder.Build();

app.UseWebSockets();

app.MapGet("/", () => Results.Text(
    """
    CarrotFantasyServer — WebSocket: /ws
    帧格式：2 字节小端 ushort 协议号 + 负载（整条 WebSocket Binary 即一帧）。
    负载内容因协议号而异：部分为原始字节/UTF-8/自定义结构；登录 200→201、拉取地图 202→203、保存单关 210→211 的负载为 Protobuf（CfNet.*）。
    登录成功：先发 201，再按 PostLoginPushPipeline 配置主动推送若干帧（如 203 地图快照）。
    内置示例：Ping→Pong；EchoUtf8；结构化 100→101；登录 200→201。
    
    """,
    "text/plain; charset=utf-8"));

app.Map("/ws", async (HttpContext context, GameConnectionHandler handler, CancellationToken ct) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using WebSocket ws = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
    await handler.HandleAsync(ws, ct).ConfigureAwait(false);
});

app.Run();
