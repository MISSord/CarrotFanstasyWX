namespace CarrotFantasyServer.PostLogin;

/// <summary>聚合所有 <see cref="IPostLoginPushContributor"/>，生成登录后的主动推送帧序列。</summary>
public sealed class PostLoginPushPipeline
{
    private readonly IEnumerable<IPostLoginPushContributor> _contributors;
    private readonly ILogger<PostLoginPushPipeline> _logger;

    public PostLoginPushPipeline(
        IEnumerable<IPostLoginPushContributor> contributors,
        ILogger<PostLoginPushPipeline> logger)
    {
        _contributors = contributors;
        _logger = logger;
    }

    /// <summary>按注册顺序依次执行各贡献者，汇总所有出站帧。</summary>
    public IReadOnlyList<byte[]> BuildFrames(long userId, string dataRoot)
    {
        var ctx = new PostLoginPushContext(userId, dataRoot);
        var list = new List<byte[]>(8);
        foreach (IPostLoginPushContributor contributor in _contributors)
        {
            try
            {
                contributor.AppendFrames(ctx, list);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "登录后主动推送失败: {Contributor}", contributor.GetType().Name);
            }
        }

        return list;
    }
}
