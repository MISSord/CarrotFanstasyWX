using System.Text;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 可选挂到场景：连接 <see cref="CarrotFantasyServer"/> 并演练 SimpleBinary 协议。
    /// Editor 下 <see cref="EditorConnectionTransport"/> 不发真实网络，可用 <see cref="ConnectionServer.DispatchPacket"/> 做本地注入。
    /// </summary>
    public sealed class SimpleBinaryProtocolDemo : MonoBehaviour
    {
        [SerializeField]
        private string webSocketUrl = "ws://localhost:5003/ws";

        [SerializeField]
        private bool sendSamplesOnStart = true;

        private void Start()
        {
            ConnectionServer cs = ServerProvision.connectionServer;
            if (cs == null)
            {
                Debug.LogWarning("SimpleBinaryProtocolDemo: ServerProvision.connectionServer 为空，请先执行 ServerProvision.Init。");
                return;
            }

            cs.Init(this.webSocketUrl);
            cs.AddListener(SimpleBinaryOpcodes.Pong, this.OnPong);
            cs.AddListener(SimpleBinaryOpcodes.EchoUtf8Reply, this.OnEchoUtf8Reply);
            cs.AddListener(SimpleBinaryOpcodes.DemoStructuredReply, this.OnDemoStructuredReply);
            cs.Start();

            if (this.sendSamplesOnStart)
            {
                cs.Send(SimpleBinaryOpcodes.Ping);
                cs.Send(SimpleBinaryOpcodes.EchoUtf8, Encoding.UTF8.GetBytes("hello-binary"));

                byte[] structuredPayload = new ConnectionBinaryPayloadWriter()
                    .WriteInt32LittleEndian(10001)
                    .WriteUInt16LittleEndian((ushort)Encoding.UTF8.GetByteCount("Carrot"))
                    .WriteUtf8("Carrot")
                    .ToArray();
                cs.Send(SimpleBinaryOpcodes.DemoStructuredRequest, structuredPayload);
            }
        }

        private void OnDestroy()
        {
            ConnectionServer cs = ServerProvision.connectionServer;
            if (cs == null)
            {
                return;
            }

            cs.RemoveListener(SimpleBinaryOpcodes.Pong, this.OnPong);
            cs.RemoveListener(SimpleBinaryOpcodes.EchoUtf8Reply, this.OnEchoUtf8Reply);
            cs.RemoveListener(SimpleBinaryOpcodes.DemoStructuredReply, this.OnDemoStructuredReply);
        }

        private void OnPong(byte[] payload)
        {
            Debug.Log(string.Format("SimpleBinary: Pong, payloadLen={0}", payload.Length));
        }

        private void OnEchoUtf8Reply(byte[] payload)
        {
            string text = payload.Length == 0 ? string.Empty : Encoding.UTF8.GetString(payload);
            Debug.Log(string.Format("SimpleBinary: EchoUtf8Reply -> {0}", text));
        }

        private void OnDemoStructuredReply(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                Debug.LogWarning("SimpleBinary: DemoStructuredReply empty");
                return;
            }

            byte ok = payload[0];
            string msg = payload.Length > 1 ? Encoding.UTF8.GetString(payload, 1, payload.Length - 1) : string.Empty;
            Debug.Log(string.Format("SimpleBinary: DemoStructuredReply ok={0}, msg={1}", ok, msg));
        }
    }
}
