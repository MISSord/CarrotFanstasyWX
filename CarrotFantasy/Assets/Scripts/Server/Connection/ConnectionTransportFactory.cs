namespace CarrotFantasy
{
    public static class ConnectionTransportFactory
    {
        public static IConnectionTransport Create()
        {
#if WECHAT_MINIGAME || WECHAT || UNITY_WEIXINMINIGAME
            return new WechatMiniGameConnectionTransport();
#else
            return new EditorConnectionTransport();
#endif
        }
    }
}
