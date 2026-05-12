namespace CarrotFantasyServer.PostLogin;

/// <summary>
/// 登录成功并已回 <c>LoginResponse</c> 后，由服务端<strong>主动</strong>下发的数据贡献者。
/// 每个实现追加 0..n 条<strong>完整 Binary 帧</strong>（2 字节小端 opcode + 负载）。
/// </summary>
public interface IPostLoginPushContributor
{
    /// <summary>向 <paramref name="outboundFrames"/> 追加帧；异常由 <see cref="PostLoginPushPipeline"/> 统一捕获。</summary>
    void AppendFrames(PostLoginPushContext context, List<byte[]> outboundFrames);
}
