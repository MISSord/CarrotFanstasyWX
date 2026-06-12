using System.Collections;
using UnityEngine;

namespace CarrotFantasy
{
    public class GameMain : MonoBehaviour
    {
        private static GameMain _persistentInstance;

        private GameStateMachine gameStateMachine;
        private AssetBundleManager assetBundleManager;

        private void Awake()
        {
            if (_persistentInstance != null && _persistentInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            _persistentInstance = this;
            DontDestroyOnLoad(gameObject);

            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            gameStateMachine = new GameStateMachine();

            SRPScheduler.Init();

            assetBundleManager = new AssetBundleManager();
            assetBundleManager.Init();

            ServerProvision.Instance.Init();

            BusinessProvision.Instance.Init();
            BusinessProvision.Instance.LoadBusiness();
        }

        private void Start()
        {
            this.StartCoroutine(this.BootstrapMainScene());
        }

        IEnumerator BootstrapMainScene()
        {
            bool loaded = false;
            bool success = false;
            ServerProvision.sceneServer.LoadScene(
                BaseSceneType.MainScene,
                null,
                ok =>
                {
                    success = ok;
                    loaded = true;
                });

            while (!loaded)
            {
                yield return null;
            }

            if (!success)
            {
                Debug.LogError("[GameMain] MainScene 加载失败，游戏流程未启动。");
                yield break;
            }

            this.gameStateMachine.Init(this);
        }

        private void Update()
        {
            ViewManager.Instance?.Update();
            AssetBundleManager.Instance.Update();
            Sche.Tick(new Fix64(Time.deltaTime));
            ServerProvision.battleSessionHost?.Tick(Time.deltaTime);

            if (BusinessProvision.Instance.IsGameQuit == true)
            {
                OnApplicationQuit();
            }
        }

        private void LateUpdate()
        {
        }

        private void OnApplicationQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ChangeMachineState(GameState state)
        {
            this.gameStateMachine.ChangeState(state);
        }
    }
}
