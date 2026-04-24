namespace CarrotFantasy
{
    public static class WechatSocketBridgeProvider
    {
        private static IWechatSocketBridge bridge;

        public static IWechatSocketBridge Bridge
        {
            get
            {
                if (bridge == null)
                {
#if WECHAT_MINIGAME || WECHAT || UNITY_WEIXINMINIGAME
                    bridge = new WechatJsBridgeSocket();
#else
                    bridge = new NullWechatSocketBridge();
#endif
                }

                return bridge;
            }
            set
            {
                bridge = value;
            }
        }
    }
}
