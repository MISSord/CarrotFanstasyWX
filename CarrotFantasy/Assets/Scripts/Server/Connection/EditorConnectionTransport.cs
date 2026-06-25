using System;
using UnityEngine;

namespace CarrotFantasy
{
    public class EditorConnectionTransport : IConnectionTransport
    {
        public event Action<byte[]> OnPacket;
        public event Action<string> OnError;
        public event Action OnConnected;
        public event Action OnDisconnected;

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
            this.OnConnected?.Invoke();
        }

        public void Stop()
        {
            if (!this.IsConnected)
            {
                return;
            }

            this.IsConnected = false;
            this.OnDisconnected?.Invoke();
        }

        public void SendRaw(byte[] packet)
        {
            if (!this.IsConnected)
            {
                this.OnError?.Invoke("EditorConnectionTransport is not connected.");
                return;
            }

            if (packet == null || packet.Length < ConnectionBinaryFrame.OpcodeSize)
            {
                this.OnError?.Invoke("EditorConnectionTransport.SendRaw packet is null or too short.");
                return;
            }

            ushort opcode = BitConverter.ToUInt16(packet, 0);
            Debug.Log(string.Format("EditorConnectionTransport.SendRaw opcode: {0}, packetBytes: {1}", opcode, packet.Length));

            if (opcode == SimpleBinaryOpcodes.Ping)
            {
                this.DispatchIncomingPacket(ConnectionBinaryFrame.Encode(SimpleBinaryOpcodes.Pong, Array.Empty<byte>()));
            }
        }

        public void DispatchIncomingPacket(byte[] packet)
        {
            this.OnPacket?.Invoke(packet);
        }

        public void Dispose()
        {
            this.Stop();
            this.OnPacket = null;
            this.OnError = null;
            this.OnConnected = null;
            this.OnDisconnected = null;
        }
    }
}
