using System;
using ETModel;
using UnityEngine;

namespace CarrotFantasy
{
    public class EditorConnectionTransport : IConnectionTransport
    {
        public event Action<ushort, IMessage> OnMessage;
        public event Action<string> OnError;

        private string address = string.Empty;
        public bool IsConnected { get; private set; }

        public void Init(string address)
        {
            this.address = address ?? string.Empty;
        }

        public void Start()
        {
            if (this.IsConnected)
            {
                return;
            }

            this.IsConnected = true;
            Debug.Log(string.Format("EditorConnectionTransport started. address: {0}", this.address));
        }

        public void Stop()
        {
            this.IsConnected = false;
        }

        public void Send(IMessage message, int messageNumber)
        {
            if (!this.IsConnected)
            {
                this.OnError?.Invoke("EditorConnectionTransport is not connected.");
                return;
            }

            if (message == null)
            {
                this.OnError?.Invoke("EditorConnectionTransport.Send message is null.");
                return;
            }

            if (!ConnectionMessageCodec.TryGetOpcode(message, out ushort opcode))
            {
                this.OnError?.Invoke(string.Format("EditorConnectionTransport cannot resolve opcode. messageType: {0}", message.GetType().FullName));
                return;
            }

            byte[] packet = ConnectionMessageCodec.EncodePacket(opcode, message);
            Debug.Log(string.Format("EditorConnectionTransport.Send opcode: {0}, packetBytes: {1}", opcode, packet.Length));
        }

        public void DispatchIncomingMessage(ushort opcode, IMessage message)
        {
            this.OnMessage?.Invoke(opcode, message);
        }

        public void Dispose()
        {
            this.Stop();
            this.OnMessage = null;
            this.OnError = null;
        }
    }
}
