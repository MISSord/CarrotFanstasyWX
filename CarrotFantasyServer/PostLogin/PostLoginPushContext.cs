namespace CarrotFantasyServer.PostLogin;

/// <summary>登录成功后各推送贡献者共用的上下文（可随业务扩展字段）。</summary>
public readonly struct PostLoginPushContext
{
    public PostLoginPushContext(long userId, string dataRoot)
    {
        UserId = userId;
        DataRoot = dataRoot;
    }

    public long UserId { get; }

    /// <summary>用户数据根目录（如 ContentRoot/userdata）。</summary>
    public string DataRoot { get; }
}
