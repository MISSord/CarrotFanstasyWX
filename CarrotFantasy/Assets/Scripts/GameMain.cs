using UnityEngine;

namespace CarrotFantasy
{
    public class GameMain : MonoBehaviour  //游戏开始脚本(作为业务层，游戏层可能有所调整)
    {
        private GameStateMachine gameStateMachine; // 游戏状态机，主要是管理游戏一些主要流程
        private AssetBundleManager assetBundleManager;

        private void Awake()
        {
            //开始游戏前的工作
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            gameStateMachine = new GameStateMachine();

            // //加载底层模块和逻辑
            // this.StartAsync().Coroutine();

            SRPScheduler.Init();

            assetBundleManager = new AssetBundleManager();
            assetBundleManager.Init();

            UIUtil.Instance.Init();
            ServerProvision.Instance.Init();

            //加载业务
            BusinessProvision.Instance.Init();
            BusinessProvision.Instance.LoadBusiness();

            //加载登录场景
            ServerProvision.sceneServer.LoadScene(BaseSceneType.MainScene, null);
        }

        private void Start()
        {
            gameStateMachine.Init(this);
        }

        private void Update()
        {
            //OneThreadSynchronizationContext.Instance.Update();
            //Game.EventSystem.Update();

            ViewManager.Instance?.Update();
            AssetBundleManager.Instance.Update();
            Sche.Tick(new Fix64(Time.deltaTime));
            if (BusinessProvision.Instance.IsGameQuit == true)
            {
                OnApplicationQuit();
            }
        }

        private void LateUpdate()
        {
            //Game.EventSystem.LateUpdate();
        }

        private void OnApplicationQuit()
        {
            //Game.Close();
#if UNITY_EDITOR//在编辑器模式退出
            UnityEditor.EditorApplication.isPlaying = false;
#else//发布后退出
        Application.Quit();
#endif
        }

        public void ChangeMachineState(GameState state)
        {
            this.gameStateMachine.ChangeState(state);
        }

        //private async ETVoid StartAsync()
        //{
        //	try
        //	{
        //		SynchronizationContext.SetSynchronizationContext(OneThreadSynchronizationContext.Instance);

        //		DontDestroyOnLoad(gameObject);
        //		ClientConfigHelper.SetConfigHelper();
        //		Game.EventSystem.Add(DLLType.Core, typeof(Core).Assembly);
        //		Game.EventSystem.Add(DLLType.Model, typeof(GameMain).Assembly);

        //		Game.Scene.AddComponent<GlobalConfigComponent>(); //web资源服务器设置组件
        //		Game.Scene.AddComponent<ResourcesComponent>(); //资源加载组件
        //		Game.Scene.AddComponent<OpcodeTypeComponent>();
        //		Game.Scene.AddComponent<NetOuterComponent>();


        //		//测试输出正确加载了Config所带的信息
        //		//ETModel.Game.Scene.GetComponent<ResourcesComponent>().LoadBundle("config.unity3d");
        //		//Game.Scene.AddComponent<ConfigComponent>();
        //		//ETModel.Game.Scene.GetComponent<ResourcesComponent>().UnloadBundle("config.unity3d");
        //		//UnitConfig unitConfig = (UnitConfig)Game.Scene.GetComponent<ConfigComponent>().Get(typeof(UnitConfig), 1001);
        //		//Log.Debug($"config {JsonHelper.ToJson(unitConfig)}");

        //	}
        //	catch (Exception e)
        //	{
        //		Log.Error(e);
        //	}
        //}
    }
}