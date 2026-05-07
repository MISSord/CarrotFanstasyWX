using System;

namespace CarrotFantasy
{
    public interface IConnectionTransport : IDisposable
    {
        /// <summary>收到一条完整 WebSocket Binary 消息。</summary>
        event Action<byte[]> OnPacket;

        event Action<string> OnError;

        bool IsConnected { get; }

        void Init(string address);
        void Start();
        void Stop();

        /// <summary>发送一整帧（<see cref="ConnectionBinaryFrame"/> 编码后的字节）。</summary>
        void SendRaw(byte[] packet);

        /// <summary>测试或编辑器：模拟收到一条二进制帧。</summary>
        void DispatchIncomingPacket(byte[] packet);
    }
}
