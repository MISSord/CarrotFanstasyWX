using System.Net.WebSockets;
using CarrotFantasyServer;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss ");

builder.Services.AddSingleton<GameConnectionHandler>();

var app = builder.Build();

app.UseWebSockets();

app.MapGet("/", () => Results.Text(
    """
    CarrotFantasyServer — WebSocket: /ws
    帧格式：2 字节小端 ushort 协议号 + 负载（整条 WebSocket Binary 即一帧）。
    负载内容因协议号而异：部分为原始字节/UTF-8/自定义结构；登录 200→201 的负载为 Protobuf（CfNet.LoginRequest / LoginResponse）。
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
