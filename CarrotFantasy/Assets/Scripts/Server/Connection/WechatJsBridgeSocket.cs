using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CarrotFantasy
{
    public class WechatJsBridgeSocket : IWechatSocketBridge
    {
        private const string BridgeGameObjectName = "__WechatSocketBridge__";

        public event Action OnOpen;
        public event Action<int, string> OnClose;
        public event Action<string> OnError;
        public event Action<byte[]> OnMessage;

        public bool IsOpen { get; private set; }

        private static WechatJsBridgeSocket activeInstance;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WechatSocket_SetGameObjectName(string gameObjectName);

        [DllImport("__Internal")]
        private static extern void WechatSocket_Connect(string address);

        [DllImport("__Internal")]
        private static extern void WechatSocket_Send(string base64Payload);

        [DllImport("__Internal")]
        private static extern void WechatSocket_Close();
#endif

        public WechatJsBridgeSocket()
        {
            activeInstance = this;
            EnsureBridgeReceiver();
        }

        public void Connect(string address)
        {
            EnsureBridgeReceiver();
            activeInstance = this;

#if UNITY_WEBGL && !UNITY_EDITOR
            WechatSocket_SetGameObjectName(BridgeGameObjectName);
            WechatSocket_Connect(address ?? string.Empty);
#else
            this.OnError?.Invoke("WechatJsBridgeSocket requires WebGL runtime.");
#endif
        }

        public void Send(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                this.OnError?.Invoke("WechatJsBridgeSocket.Send payload is empty.");
                return;
            }

            if (!this.IsOpen)
            {
                this.OnError?.Invoke("WechatJsBridgeSocket is not open.");
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            string base64 = Convert.ToBase64String(payload);
            WechatSocket_Send(base64);
#else
            this.OnError?.Invoke("WechatJsBridgeSocket requires WebGL runtime.");
#endif
        }

        public void Close()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WechatSocket_Close();
#endif
            this.IsOpen = false;
        }

        private static void EnsureBridgeReceiver()
        {
            GameObject gameObject = GameObject.Find(BridgeGameObjectName);
            if (gameObject == null)
            {
                gameObject = new GameObject(BridgeGameObjectName);
                gameObject.AddComponent<WechatSocketCallbackReceiver>();
                UnityEngine.Object.DontDestroyOnLoad(gameObject);
                return;
            }

            if (gameObject.GetComponent<WechatSocketCallbackReceiver>() == null)
            {
                gameObject.AddComponent<WechatSocketCallbackReceiver>();
            }
        }

        internal static void HandleOpenFromJs()
        {
            if (activeInstance == null)
            {
                return;
            }

            activeInstance.IsOpen = true;
            activeInstance.OnOpen?.Invoke();
        }

        internal static void HandleCloseFromJs(string closePayload)
        {
            if (activeInstance == null)
            {
                return;
            }

            activeInstance.IsOpen = false;
            int code = 0;
            string reason = string.Empty;

            if (!string.IsNullOrEmpty(closePayload))
            {
                int separatorIndex = closePayload.IndexOf('|');
                if (separatorIndex > 0)
                {
                    string codePart = closePayload.Substring(0, separatorIndex);
                    int.TryParse(codePart, out code);
                    reason = closePayload.Substring(separatorIndex + 1);
                }
                else
                {
                    reason = closePayload;
                }
            }

            activeInstance.OnClose?.Invoke(code, reason);
        }

        internal static void HandleErrorFromJs(string error)
        {
            if (activeInstance == null)
            {
                return;
            }

            activeInstance.IsOpen = false;
            activeInstance.OnError?.Invoke(error ?? "wechat socket unknown error");
        }

        internal static void HandleMessageFromJs(string base64Packet)
        {
            if (activeInstance == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(base64Packet))
            {
                activeInstance.OnError?.Invoke("wechat socket message payload is empty");
                return;
            }

            try
            {
                byte[] payload = Convert.FromBase64String(base64Packet);
                activeInstance.OnMessage?.Invoke(payload);
            }
            catch (Exception e)
            {
                activeInstance.OnError?.Invoke(string.Format("wechat socket decode failed: {0}", e.Message));
            }
        }
    }
}
