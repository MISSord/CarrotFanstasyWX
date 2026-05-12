using CarrotFantasyServer.Protocol;
using CfNet;
using Google.Protobuf;

namespace CarrotFantasyServer.PostLogin;

/// <summary>登录成功后主动下发用户地图快照（与 GetUserMapResponse 203 负载一致，客户端无需先发 202）。</summary>
public sealed class UserMapPostLoginPushContributor : IPostLoginPushContributor
{
    public void AppendFrames(PostLoginPushContext context, List<byte[]> outboundFrames)
    {
        string snapshot = UserMapStore.LoadOrCreate(context.UserId, context.DataRoot);
        var resp = new GetUserMapResponse
        {
            Result = 0,
            MapSnapshot = snapshot,
            Message = "post_login_push",
        };

        outboundFrames.Add(BinaryFrame.Encode(SimpleOpcodes.GetUserMapResponse, resp.ToByteArray()));
    }
}
