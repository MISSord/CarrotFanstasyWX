using System;
using System.Collections.Generic;
using Google.Protobuf;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// WebSocket Binary：2 字节小端 opcode + 负载。
    /// 负载可为原始字节，也可为 Protobuf 序列化体（由 opcode 约定）。
    /// </summary>
    public class ConnectionServer
    {
        private readonly Dictionary<ushort, Action<byte[]>> listenerMap = new Dictionary<ushort, Action<byte[]>>();
        private readonly Dictionary<ushort, ProtobufBinding> protobufListenerMap = new Dictionary<ushort, ProtobufBinding>();

        private IConnectionTransport transport;
        private string connectAddress = string.Empty;
        private bool isInited = false;

        private abstract class ProtobufBinding
        {
            public abstract bool TryDispatch(byte[] payload);
        }

        private sealed class ProtobufBinding<T> : ProtobufBinding where T : class, IMessage<T>
        {
            private readonly MessageParser<T> parser;
            private readonly Action<T> handler;

            public ProtobufBinding(MessageParser<T> parser, Action<T> handler)
            {
                this.parser = parser;
                this.handler = handler;
            }

            public override bool TryDispatch(byte[] payload)
            {
                T msg = this.parser.ParseFrom(payload);
                this.handler?.Invoke(msg);
                return true;
            }
        }

        public void Init(string address = "")
        {
            this.connectAddress = address ?? string.Empty;
            this.RebuildTransport();
            this.isInited = true;
        }

        public void Start()
        {
            this.EnsureInit();
            this.transport.Start();
        }

        public void AddListener(ushort opcode, Action<byte[]> callBack)
        {
            if (callBack == null)
            {
                return;
            }

            if (!this.listenerMap.TryGetValue(opcode, out Action<byte[]> existed))
            {
                this.listenerMap[opcode] = callBack;
                return;
            }

            existed += callBack;
            this.listenerMap[opcode] = existed;
        }

        public void RemoveListener(ushort opcode, Action<byte[]> callBack)
        {
            if (callBack == null)
            {
                return;
            }

            if (!this.listenerMap.TryGetValue(opcode, out Action<byte[]> existed))
            {
                return;
            }

            existed -= callBack;
            if (existed == null)
            {
                this.listenerMap.Remove(opcode);
                return;
            }

            this.listenerMap[opcode] = existed;
        }

        /// <summary>按 opcode 用指定 Parser 反序列化后回调（优先于原始字节监听）。</summary>
        public void AddProtobufListener<T>(ushort opcode, MessageParser<T> parser, Action<T> handler)
            where T : class, IMessage<T>
        {
            if (parser == null || handler == null)
            {
                return;
            }

            this.protobufListenerMap[opcode] = new ProtobufBinding<T>(parser, handler);
        }

        public void RemoveProtobufListener(ushort opcode)
        {
            this.protobufListenerMap.Remove(opcode);
        }

        /// <summary>发送 Protobuf 消息体（不含 opcode，opcode 在帧头）。</summary>
        public void SendProtobuf<T>(ushort opcode, T message) where T : IMessage<T>
        {
            if (message == null)
            {
                Debug.LogWarning("ConnectionServer.SendProtobuf: message is null.");
                return;
            }

            byte[] payload = message.ToByteArray();
            this.Send(opcode, payload);
        }

        public void Send(ushort opcode, byte[] payload = null)
        {
            this.EnsureInit();

            if (!this.transport.IsConnected)
            {
                this.transport.Start();
            }

            byte[] packet = ConnectionBinaryFrame.Encode(opcode, payload);
            this.transport.SendRaw(packet);
        }

        public void DispatchPacket(byte[] packet)
        {
            this.HandleTransportPacket(packet);
        }

        private void HandleTransportPacket(byte[] packet)
        {
            if (!ConnectionBinaryFrame.TryDecode(packet, out ushort opcode, out byte[] payload))
            {
                Debug.LogWarning("ConnectionServer received packet too short for opcode.");
                return;
            }

            if (this.protobufListenerMap.TryGetValue(opcode, out ProtobufBinding binding))
            {
                try
                {
                    binding.TryDispatch(payload);
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(string.Format("ConnectionServer Protobuf 解析失败 opcode={0}: {1}", opcode, ex.Message));
                    return;
                }
            }

            if (this.listenerMap.TryGetValue(opcode, out Action<byte[]> callBack))
            {
                callBack?.Invoke(payload);
                return;
            }

            Debug.LogWarning(string.Format("ConnectionServer receive packet without listener. opcode: {0}, payloadLen: {1}", opcode, payload.Length));
        }

        public void Dispose()
        {
            this.listenerMap.Clear();
            this.protobufListenerMap.Clear();

            if (this.transport != null)
            {
                this.transport.OnPacket -= this.HandleTransportPacket;
                this.transport.OnError -= this.OnTransportError;
                this.transport.Dispose();
                this.transport = null;
            }

            this.isInited = false;
        }

        private void EnsureInit()
        {
            if (this.isInited == false || this.transport == null)
            {
                this.Init(this.connectAddress);
            }
        }

        private void RebuildTransport()
        {
            if (this.transport != null)
            {
                this.transport.OnPacket -= this.HandleTransportPacket;
                this.transport.OnError -= this.OnTransportError;
                this.transport.Dispose();
                this.transport = null;
            }

            this.transport = ConnectionTransportFactory.Create();
            this.transport.Init(this.connectAddress);
            this.transport.OnPacket += this.HandleTransportPacket;
            this.transport.OnError += this.OnTransportError;
        }

        private void OnTransportError(string error)
        {
            Debug.LogError(string.Format("Connection transport error: {0}", error));
        }
    }
}
