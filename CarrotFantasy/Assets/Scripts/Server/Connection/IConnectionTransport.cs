using ETModel;
using System;

namespace CarrotFantasy
{
    public interface IConnectionTransport : IDisposable
    {
        event Action<ushort, IMessage> OnMessage;
        event Action<string> OnError;

        bool IsConnected { get; }

        void Init(string address);
        void Start();
        void Stop();
        void Send(IMessage message, int messageNumber);
    }
}
