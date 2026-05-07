using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class WechatMiniGameConnectionTransport : IConnectionTransport
    {
        public event Action<byte[]> OnPacket;
        public event Action<string> OnError;

        private string address = string.Empty;
        private IWechatSocketBridge bridge;
        private readonly Queue<byte[]> pendingPackets = new Queue<byte[]>();
        private bool isConnecting = false;
        public bool IsConnected { get; private set; }

        public void Init(string address)
        {
            this.address = address ?? string.Empty;
            this.BindBridge();
        }

        public void Start()
        {
            if (this.IsConnected)
            {
                return;
            }

            if (this.bridge == null)
            {
                this.BindBridge();
            }

            this.bridge.Connect(this.address);
            this.isConnecting = true;
            this.IsConnected = this.bridge.IsOpen;
            Debug.Log(string.Format("WechatMiniGameConnectionTransport connecting. address: {0}", this.address));
        }

        public void Stop()
        {
            if (this.bridge != null)
            {
                this.bridge.Close();
            }

            this.pendingPackets.Clear();
            this.isConnecting = false;
            this.IsConnected = false;
        }

        public void SendRaw(byte[] packet)
        {
            if (packet == null || packet.Length < ConnectionBinaryFrame.OpcodeSize)
            {
                this.OnError?.Invoke("WechatMiniGameConnectionTransport.SendRaw packet is null or too short.");
                return;
            }

            ushort opcode = BitConverter.ToUInt16(packet, 0);
            this.TryEnqueueOrSend(packet, opcode);
        }

        private void TryEnqueueOrSend(byte[] packet, ushort opcode)
        {
            if (!this.IsConnected)
            {
                if (this.isConnecting)
                {
                    this.pendingPackets.Enqueue(packet);
                    return;
                }

                this.OnError?.Invoke("WechatMiniGameConnectionTransport is not connected.");
                return;
            }

            this.bridge.Send(packet);
            Debug.Log(string.Format("WechatMiniGameConnectionTransport.Send opcode: {0}, packetBytes: {1}", opcode, packet.Length));
        }

        public void DispatchIncomingPacket(byte[] packet)
        {
            this.OnPacket?.Invoke(packet);
        }

        public void Dispose()
        {
            this.Stop();
            this.UnbindBridge();
            this.OnPacket = null;
            this.OnError = null;
        }

        private void BindBridge()
        {
            if (this.bridge != null)
            {
                this.UnbindBridge();
            }

            this.bridge = WechatSocketBridgeProvider.Bridge;
            if (this.bridge == null)
            {
                return;
            }

            this.bridge.OnOpen += this.HandleBridgeOpen;
            this.bridge.OnClose += this.HandleBridgeClose;
            this.bridge.OnError += this.HandleBridgeError;
            this.bridge.OnMessage += this.HandleBridgeMessage;
        }

        private void UnbindBridge()
        {
            if (this.bridge == null)
            {
                return;
            }

            this.bridge.OnOpen -= this.HandleBridgeOpen;
            this.bridge.OnClose -= this.HandleBridgeClose;
            this.bridge.OnError -= this.HandleBridgeError;
            this.bridge.OnMessage -= this.HandleBridgeMessage;
            this.bridge = null;
        }

        private void HandleBridgeOpen()
        {
            this.isConnecting = false;
            this.IsConnected = true;
            Debug.Log("WechatMiniGameConnectionTransport connected.");

            while (this.pendingPackets.Count > 0)
            {
                byte[] packet = this.pendingPackets.Dequeue();
                this.bridge.Send(packet);
            }
        }

        private void HandleBridgeClose(int code, string reason)
        {
            this.isConnecting = false;
            this.IsConnected = false;
            Debug.LogWarning(string.Format("WechatMiniGameConnectionTransport closed. code: {0}, reason: {1}", code, reason));
        }

        private void HandleBridgeError(string error)
        {
            this.isConnecting = false;
            this.IsConnected = false;
            this.OnError?.Invoke(error);
        }

        private void HandleBridgeMessage(byte[] packet)
        {
            this.OnPacket?.Invoke(packet);
        }
    }
}
