using UnityEngine;

namespace CarrotFantasy
{
    public class WechatSocketCallbackReceiver : MonoBehaviour
    {
        public void OnWechatSocketOpen(string _)
        {
            WechatJsBridgeSocket.HandleOpenFromJs();
        }

        public void OnWechatSocketClose(string payload)
        {
            WechatJsBridgeSocket.HandleCloseFromJs(payload);
        }

        public void OnWechatSocketError(string payload)
        {
            WechatJsBridgeSocket.HandleErrorFromJs(payload);
        }

        public void OnWechatSocketMessage(string base64Payload)
        {
            WechatJsBridgeSocket.HandleMessageFromJs(base64Payload);
        }
    }
}
