using ETModel;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class ConnectionServer
    {
        private readonly Dictionary<ushort, CallBack<IMessage>> listenerMap = new Dictionary<ushort, CallBack<IMessage>>();
        private readonly Dictionary<Type, int> messageNumberMap = new Dictionary<Type, int>();
        private int nextMessageNumber = 1;

        private IConnectionTransport transport;
        private string connectAddress = string.Empty;
        private bool isInited = false;

        public void init(string address = "")
        {
            this.connectAddress = address ?? string.Empty;
            this.rebuildTransport();
            this.isInited = true;
        }

        public void start()
        {
            this.ensureInit();
            this.transport.Start();
        }

        public void AddListener(ushort opcode, CallBack<IMessage> callBack)
        {
            if (callBack == null)
            {
                return;
            }

            if (!this.listenerMap.TryGetValue(opcode, out CallBack<IMessage> existed))
            {
                this.listenerMap[opcode] = callBack;
                return;
            }

            existed += callBack;
            this.listenerMap[opcode] = existed;
        }

        public void RemoveListener(ushort opcode, CallBack<IMessage> callBack)
        {
            if (callBack == null)
            {
                return;
            }

            if (!this.listenerMap.TryGetValue(opcode, out CallBack<IMessage> existed))
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

        public int getMessageNumber(IMessage message)
        {
            if (message == null)
            {
                return 0;
            }

            if (ConnectionMessageCodec.TryGetOpcode(message, out ushort opcode))
            {
                return opcode;
            }

            Type messageType = message.GetType();
            if (this.messageNumberMap.TryGetValue(messageType, out int messageNumber))
            {
                return messageNumber;
            }

            messageNumber = this.nextMessageNumber++;
            this.messageNumberMap.Add(messageType, messageNumber);
            return messageNumber;
        }

        public void Send(IMessage message)
        {
            if (message == null)
            {
                Debug.LogWarning("ConnectionServer.Send called with null message.");
                return;
            }

            this.ensureInit();

            if (!this.transport.IsConnected)
            {
                this.transport.Start();
            }

            int messageNumber = this.getMessageNumber(message);
            this.transport.Send(message, messageNumber);
        }

        public void dispatchMessage(ushort opcode, IMessage message)
        {
            if (this.listenerMap.TryGetValue(opcode, out CallBack<IMessage> callBack))
            {
                callBack(message);
                return;
            }

            Debug.LogWarning(string.Format("ConnectionServer receive message without listener. opcode: {0}", opcode));
        }

        public void Dispose()
        {
            this.listenerMap.Clear();
            this.messageNumberMap.Clear();
            this.nextMessageNumber = 1;

            if (this.transport != null)
            {
                this.transport.OnMessage -= this.dispatchMessage;
                this.transport.OnError -= this.onTransportError;
                this.transport.Dispose();
                this.transport = null;
            }

            this.isInited = false;
        }

        private void ensureInit()
        {
            if (this.isInited == false || this.transport == null)
            {
                this.init(this.connectAddress);
            }
        }

        private void rebuildTransport()
        {
            if (this.transport != null)
            {
                this.transport.OnMessage -= this.dispatchMessage;
                this.transport.OnError -= this.onTransportError;
                this.transport.Dispose();
                this.transport = null;
            }

            this.transport = ConnectionTransportFactory.Create();
            this.transport.Init(this.connectAddress);
            this.transport.OnMessage += this.dispatchMessage;
            this.transport.OnError += this.onTransportError;
        }

        private void onTransportError(string error)
        {
            Debug.LogError(string.Format("Connection transport error: {0}", error));
        }
    }
}
