using System;
using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Text;
using CarrotFantasyServer.Protocol;
using CfNet;
using Google.Protobuf;

namespace CarrotFantasyServer;

/// <summary>单连接：Binary 帧 = 2 字节小端 opcode + 负载。</summary>
internal sealed class GameConnectionHandler
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly ILogger<GameConnectionHandler> _logger;

    public GameConnectionHandler(ILogger<GameConnectionHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        var buffer = new byte[256 * 1024];
        while (webSocket.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult receiveResult;
            do
            {
                receiveResult = await webSocket
                    .ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                    .ConfigureAwait(false);

                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket
                        .CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                if (receiveResult.MessageType != WebSocketMessageType.Binary)
                {
                    _logger.LogWarning("忽略非 Binary 帧: {Type}", receiveResult.MessageType);
                    continue;
                }

                ms.Write(buffer, 0, receiveResult.Count);
            }
            while (!receiveResult.EndOfMessage);

            byte[] packet = ms.ToArray();
            if (!BinaryFrame.TryDecode(packet, out ushort opcode, out byte[] body))
            {
                _logger.LogWarning("包过短，无法读取 opcode");
                continue;
            }

            byte[]? response = TryHandleMessage(opcode, body);
            if (response == null)
            {
                continue;
            }

            await webSocket
                .SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Binary, true, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private byte[]? TryHandleMessage(ushort opcode, byte[] body)
    {
        try
        {
            switch (opcode)
            {
                case SimpleOpcodes.Ping:
                    _logger.LogDebug("Ping");
                    return BinaryFrame.Encode(SimpleOpcodes.Pong);

                case SimpleOpcodes.EchoUtf8:
                {
                    string text;
                    try
                    {
                        text = body.Length == 0 ? string.Empty : Utf8.GetString(body);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "EchoUtf8 负载不是合法 UTF-8");
                        return null;
                    }

                    _logger.LogInformation("EchoUtf8: {Text}", text);
                    byte[] replyPayload = Utf8.GetBytes("echo:" + text);
                    return BinaryFrame.Encode(SimpleOpcodes.EchoUtf8Reply, replyPayload);
                }

                case SimpleOpcodes.DemoStructuredRequest:
                    return HandleDemoStructured(body);

                case SimpleOpcodes.LoginRequest:
                    return HandleLoginProtobuf(body);

                default:
                    _logger.LogWarning("未实现 opcode={Opcode}, bodyLen={Len}", opcode, body.Length);
                    return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理 opcode {Opcode} 异常", opcode);
            return null;
        }
    }

    /// <summary>
    /// 演示协议号 100：负载布局 [int32LE userId][uint16LE nameLen][name bytes UTF-8]。
    /// </summary>
    private byte[]? HandleDemoStructured(byte[] body)
    {
        var reader = new BinaryPayloadReader(body);
        if (!reader.TryReadInt32LittleEndian(out int userId))
        {
            _logger.LogWarning("Demo100: 缺少 userId");
            return null;
        }

        if (!reader.TryReadUInt16LittleEndian(out ushort nameLen))
        {
            _logger.LogWarning("Demo100: 缺少 nameLen");
            return null;
        }

        if (!reader.TryReadBytes(nameLen, out ReadOnlyMemory<byte> nameBytes))
        {
            _logger.LogWarning("Demo100: name 长度不足");
            return null;
        }

        if (reader.Remaining != 0)
        {
            _logger.LogWarning("Demo100: 负载尾部多余 {Remaining} 字节", reader.Remaining);
        }

        string name = Utf8.GetString(nameBytes.Span);
        _logger.LogInformation("Demo100 userId={UserId} name={Name}", userId, name);

        byte[] msg = Utf8.GetBytes($"ok,{userId},{name}");
        var responsePayload = new byte[1 + msg.Length];
        responsePayload[0] = 1;
        Buffer.BlockCopy(msg, 0, responsePayload, 1, msg.Length);
        return BinaryFrame.Encode(SimpleOpcodes.DemoStructuredReply, responsePayload);
    }

    /// <summary>登录：负载为 Protobuf <see cref="LoginRequest"/>。演示规则账号==密码且非空。</summary>
    private byte[]? HandleLoginProtobuf(byte[] body)
    {
        LoginRequest req;
        try
        {
            req = LoginRequest.Parser.ParseFrom(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoginRequest Protobuf 解析失败");
            return BuildLoginResponseProto(1, 0, "请求格式错误");
        }

        string account = req.Account ?? string.Empty;
        string password = req.Password ?? string.Empty;

        if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
        {
            return BuildLoginResponseProto(2, 0, "账号或密码错误");
        }

        if (!string.Equals(account, password, StringComparison.Ordinal))
        {
            _logger.LogInformation("Login 失败 account={Account}", account);
            return BuildLoginResponseProto(2, 0, "账号或密码错误");
        }

        long userId = StableUserIdFromString(account);
        _logger.LogInformation("Login 成功 account={Account} userId={UserId}", account, userId);
        return BuildLoginResponseProto(0, userId, "登录成功");
    }

    private static byte[] BuildLoginResponseProto(int result, long userId, string message)
    {
        var resp = new LoginResponse
        {
            Result = result,
            UserId = userId,
            Message = message ?? string.Empty,
        };

        return BinaryFrame.Encode(SimpleOpcodes.LoginResponse, resp.ToByteArray());
    }

    private static long StableUserIdFromString(string account)
    {
        byte[] utf8 = Utf8.GetBytes(account);
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        for (int i = 0; i < utf8.Length; i++)
        {
            hash ^= utf8[i];
            hash *= prime;
        }

        long id = (long)(hash & 0x7FFF_FFFF_FFFF_FFFFL);
        return id == 0 ? 1L : id;
    }
}
