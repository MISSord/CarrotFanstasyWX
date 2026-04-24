using System;

namespace CarrotFantasy
{
    public class NullWechatSocketBridge : IWechatSocketBridge
    {
        public event Action OnOpen;
        public event Action<int, string> OnClose;
        public event Action<string> OnError;
        public event Action<byte[]> OnMessage;

        public bool IsOpen { get; private set; }

        public void Connect(string address)
        {
            this.IsOpen = false;
            this.OnError?.Invoke("Wechat socket bridge is not configured. Please set WechatSocketBridgeProvider.Bridge.");
        }

        public void Send(byte[] payload)
        {
            this.OnError?.Invoke("Wechat socket bridge is not configured. Send ignored.");
        }

        public void Close()
        {
            this.IsOpen = false;
            this.OnClose?.Invoke(0, "closed by null bridge");
        }

        public void SimulateOpen()
        {
            this.IsOpen = true;
            this.OnOpen?.Invoke();
        }

        public void SimulateMessage(byte[] payload)
        {
            this.OnMessage?.Invoke(payload);
        }
    }
}
