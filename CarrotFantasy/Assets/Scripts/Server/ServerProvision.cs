namespace CarrotFantasy
{
    public class ServerProvision
    {
        private static ServerProvision _server;
        public static SceneServer sceneServer;
        public static ConnectionServer connectionServer;
        public static ConnectionLifecycle connectionLifecycle;
        public static BattleSessionHost battleSessionHost;

        public static ServerProvision Instance
        {
            get
            {
                if (_server == null)
                {
                    _server = new ServerProvision();
                }
                return _server;
            }
        }

        public void Init()
        {
            // GameMain 可能为了启动画面提前初始化过 ViewManager；这里只复用/重绑，
            // 不能重建，否则会清空已注册的 StartLoadPanel，启动画面会直接消失。
            ViewManager viewManager = ViewManager.EnsureInitialized();
            viewManager.RebindScenePresentation();

            connectionServer = new ConnectionServer();
            connectionServer.Init(GameNetworkEndpoints.WebSocketUrl);

            connectionLifecycle = new ConnectionLifecycle();
            connectionLifecycle.Init(connectionServer);

            sceneServer = new SceneServer();
            sceneServer.Init();

            battleSessionHost = new BattleSessionHost();
        }
    }
}
