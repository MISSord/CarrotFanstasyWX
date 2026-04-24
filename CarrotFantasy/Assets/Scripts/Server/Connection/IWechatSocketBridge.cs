using System;

namespace CarrotFantasy
{
    public interface IWechatSocketBridge
    {
        event Action OnOpen;
        event Action<int, string> OnClose;
        event Action<string> OnError;
        event Action<byte[]> OnMessage;

        bool IsOpen { get; }

        void Connect(string address);
        void Send(byte[] payload);
        void Close();
    }
}
