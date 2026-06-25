using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace CarrotFantasyServer;

/// <summary>
/// 按 userId 跟踪当前活跃 WebSocket；同账号新登录时关闭旧连接（断线重连场景）。
/// </summary>
internal sealed class ConnectionRegistry
{
    private sealed class ConnectionEntry
    {
        public required Guid ConnectionId { get; init; }
        public required long UserId { get; init; }
        public required WebSocket Socket { get; init; }
    }

    private readonly ConcurrentDictionary<long, ConnectionEntry> _byUserId = new();
    private readonly ConcurrentDictionary<Guid, long> _userIdByConnection = new();

    /// <summary>该 userId 是否已有其它连接在线（用于区分首次登录与重连）。</summary>
    public bool HasActiveConnectionForUser(long userId, Guid exceptConnectionId)
    {
        if (!_byUserId.TryGetValue(userId, out ConnectionEntry? entry))
        {
            return false;
        }

        return entry.ConnectionId != exceptConnectionId
            && entry.Socket.State == WebSocketState.Open;
    }

    /// <summary>登录成功后登记；若同 userId 已有旧连接则异步关闭。</summary>
    public void Register(Guid connectionId, long userId, WebSocket socket, ILogger logger)
    {
        if (_byUserId.TryGetValue(userId, out ConnectionEntry? existing)
            && existing.ConnectionId != connectionId)
        {
            logger.LogInformation(
                "同账号新连接登录，踢掉旧连接 userId={UserId} oldConnectionId={OldConnectionId} newConnectionId={NewConnectionId}",
                userId,
                existing.ConnectionId,
                connectionId);
            _ = CloseSupersededAsync(existing, logger);
        }

        var entry = new ConnectionEntry
        {
            ConnectionId = connectionId,
            UserId = userId,
            Socket = socket,
        };

        _byUserId[userId] = entry;
        _userIdByConnection[connectionId] = userId;
    }

    public void Unregister(Guid connectionId)
    {
        if (!_userIdByConnection.TryRemove(connectionId, out long userId))
        {
            return;
        }

        if (_byUserId.TryGetValue(userId, out ConnectionEntry? entry)
            && entry.ConnectionId == connectionId)
        {
            _byUserId.TryRemove(userId, out _);
        }
    }

    /// <summary>当前连接是否仍为该 userId 的权威会话（被踢后返回 false）。</summary>
    public bool IsActiveSession(Guid connectionId, long userId)
    {
        if (!_byUserId.TryGetValue(userId, out ConnectionEntry? entry))
        {
            return false;
        }

        return entry.ConnectionId == connectionId;
    }

    private async Task CloseSupersededAsync(ConnectionEntry entry, ILogger logger)
    {
        _userIdByConnection.TryRemove(entry.ConnectionId, out _);

        try
        {
            if (entry.Socket.State == WebSocketState.Open
                || entry.Socket.State == WebSocketState.CloseReceived)
            {
                await entry.Socket
                    .CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "superseded_by_new_login",
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch (WebSocketException ex)
        {
            logger.LogDebug(ex, "关闭被替换连接时 WebSocket 异常 connectionId={ConnectionId}", entry.ConnectionId);
        }
        catch (ObjectDisposedException)
        {
            // 连接已被释放，忽略
        }
    }
}
