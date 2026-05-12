using System.Net.WebSockets;
using System.Text;
using CarrotFantasyServer.PostLogin;
using CarrotFantasyServer.Protocol;
using CfNet;
using Google.Protobuf;

namespace CarrotFantasyServer;

/// <summary>单连接：Binary 帧 = 2 字节小端 opcode + 负载。每 WebSocket 连接一个实例（Transient）。</summary>
internal sealed class GameConnectionHandler
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly ILogger<GameConnectionHandler> _logger;
    private readonly string _dataRoot;
    private readonly PostLoginPushPipeline _postLoginPushPipeline;
    private long? _sessionUserId;

    public GameConnectionHandler(
        ILogger<GameConnectionHandler> logger,
        IWebHostEnvironment env,
        PostLoginPushPipeline postLoginPushPipeline)
    {
        _logger = logger;
        _dataRoot = Path.Combine(env.ContentRootPath, "userdata");
        _postLoginPushPipeline = postLoginPushPipeline;
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

            HandlerOutcome outcome = TryHandleMessage(opcode, body);
            if (!outcome.HasAnything)
            {
                continue;
            }

            if (outcome.Primary != null)
            {
                await SendBinaryFrameAsync(webSocket, outcome.Primary, cancellationToken).ConfigureAwait(false);
            }

            if (outcome.FollowUps is { Count: > 0 })
            {
                for (int i = 0; i < outcome.FollowUps.Count; i++)
                {
                    await SendBinaryFrameAsync(webSocket, outcome.FollowUps[i], cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private static Task SendBinaryFrameAsync(WebSocket webSocket, byte[] frame, CancellationToken cancellationToken)
    {
        return webSocket.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, endOfMessage: true, cancellationToken);
    }

    private readonly struct HandlerOutcome
    {
        public HandlerOutcome(byte[]? primary, IReadOnlyList<byte[]>? followUps = null)
        {
            Primary = primary;
            FollowUps = followUps;
        }

        public byte[]? Primary { get; }

        /// <summary>在 <see cref="Primary"/> 之后按顺序发送的额外帧（如登录后主动推送）。</summary>
        public IReadOnlyList<byte[]>? FollowUps { get; }

        public bool HasAnything => Primary != null || FollowUps is { Count: > 0 };
    }

    private HandlerOutcome TryHandleMessage(ushort opcode, byte[] body)
    {
        try
        {
            switch (opcode)
            {
                case SimpleOpcodes.Ping:
                    _logger.LogDebug("Ping");
                    return new HandlerOutcome(BinaryFrame.Encode(SimpleOpcodes.Pong));

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
                        return default;
                    }

                    _logger.LogInformation("EchoUtf8: {Text}", text);
                    byte[] replyPayload = Utf8.GetBytes("echo:" + text);
                    return new HandlerOutcome(BinaryFrame.Encode(SimpleOpcodes.EchoUtf8Reply, replyPayload));
                }

                case SimpleOpcodes.DemoStructuredRequest:
                {
                    byte[]? r = HandleDemoStructured(body);
                    return r == null ? default : new HandlerOutcome(r);
                }

                case SimpleOpcodes.LoginRequest:
                    return HandleLoginProtobuf(body);

                case SimpleOpcodes.GetUserMapRequest:
                    return new HandlerOutcome(HandleGetUserMapProtobuf(body));

                case SimpleOpcodes.SetSingleMapRequest:
                    return new HandlerOutcome(HandleSetSingleMapProtobuf(body));

                default:
                    _logger.LogWarning("未实现 opcode={Opcode}, bodyLen={Len}", opcode, body.Length);
                    return default;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理 opcode {Opcode} 异常", opcode);
            return default;
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
    private HandlerOutcome HandleLoginProtobuf(byte[] body)
    {
        _sessionUserId = null;

        LoginRequest req;
        try
        {
            req = LoginRequest.Parser.ParseFrom(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoginRequest Protobuf 解析失败");
            return new HandlerOutcome(BuildLoginResponseProto(1, 0, "请求格式错误"));
        }

        string account = req.Account ?? string.Empty;
        string password = req.Password ?? string.Empty;

        if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
        {
            return new HandlerOutcome(BuildLoginResponseProto(2, 0, "账号或密码错误"));
        }

        if (!string.Equals(account, password, StringComparison.Ordinal))
        {
            _logger.LogInformation("Login 失败 account={Account}", account);
            return new HandlerOutcome(BuildLoginResponseProto(2, 0, "账号或密码错误"));
        }

        long userId = StableUserIdFromString(account);
        _sessionUserId = userId;
        _logger.LogInformation("Login 成功 account={Account} userId={UserId}", account, userId);

        byte[] primary = BuildLoginResponseProto(0, userId, "登录成功");
        IReadOnlyList<byte[]> followUps = _postLoginPushPipeline.BuildFrames(userId, _dataRoot);
        return new HandlerOutcome(primary, followUps);
    }

    private byte[] HandleGetUserMapProtobuf(byte[] body)
    {
        if (_sessionUserId is null)
        {
            return BuildGetUserMapResponseProto(401, string.Empty, "请先登录");
        }

        GetUserMapRequest req;
        try
        {
            req = GetUserMapRequest.Parser.ParseFrom(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetUserMapRequest 解析失败");
            return BuildGetUserMapResponseProto(1, string.Empty, "请求格式错误");
        }

        if (req.UserId != _sessionUserId.Value)
        {
            return BuildGetUserMapResponseProto(403, string.Empty, "用户不匹配");
        }

        string snapshot = UserMapStore.LoadOrCreate(req.UserId, _dataRoot);
        return BuildGetUserMapResponseProto(0, snapshot, "ok");
    }

    private byte[] HandleSetSingleMapProtobuf(byte[] body)
    {
        if (_sessionUserId is null)
        {
            return BuildSetSingleMapResponseProto(401, 0, 0, 0, "请先登录");
        }

        SetSingleMapRequest req;
        try
        {
            req = SetSingleMapRequest.Parser.ParseFrom(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SetSingleMapRequest 解析失败");
            return BuildSetSingleMapResponseProto(1, 0, 0, 0, "请求格式错误");
        }

        if (req.UserId != _sessionUserId.Value)
        {
            return BuildSetSingleMapResponseProto(403, 0, 0, 0, "用户不匹配");
        }

        if (req.BigLevelId < 1 || req.BigLevelId > 3 || req.LevelId < 1 || req.LevelId > 5)
        {
            return BuildSetSingleMapResponseProto(400, 0, 0, 0, "关卡参数无效");
        }

        try
        {
            (int nextBig, int nextSmall) = UserMapStore.ApplyVictoryAndSave(
                req.UserId,
                _dataRoot,
                req.BigLevelId,
                req.LevelId,
                req.CarrotState,
                req.IsAllClear);

            int unlockFlag = nextBig == 0 ? 0 : 1;
            return BuildSetSingleMapResponseProto(0, nextBig, nextSmall, unlockFlag, "保存成功");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存地图失败 userId={UserId}", req.UserId);
            return BuildSetSingleMapResponseProto(500, 0, 0, 0, "服务器保存失败");
        }
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

    private static byte[] BuildGetUserMapResponseProto(int result, string mapSnapshot, string message)
    {
        var resp = new GetUserMapResponse
        {
            Result = result,
            MapSnapshot = mapSnapshot ?? string.Empty,
            Message = message ?? string.Empty,
        };

        return BinaryFrame.Encode(SimpleOpcodes.GetUserMapResponse, resp.ToByteArray());
    }

    private static byte[] BuildSetSingleMapResponseProto(int result, int bigLevelId, int levelId, int unlocked, string message)
    {
        var resp = new SetSingleMapResponse
        {
            Result = result,
            BigLevelId = bigLevelId,
            LevelId = levelId,
            Unlocked = unlocked,
            Message = message ?? string.Empty,
        };

        return BinaryFrame.Encode(SimpleOpcodes.SetSingleMapResponse, resp.ToByteArray());
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
