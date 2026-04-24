namespace CarrotFantasy
{
    public class ServerProvision
    {
        private static ServerProvision _server;
        public static PanelServer panelServer;
        public static SceneServer sceneServer;
        public static ConnectionServer connectionServer;

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
            connectionServer = new ConnectionServer();
            connectionServer.init();
            sceneServer = new SceneServer();
            sceneServer.Init();
            panelServer = new PanelServer();
            panelServer.Init();
        }
    }
}
