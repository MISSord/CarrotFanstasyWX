using System.Collections;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// AOT 启动壳：先资源更新（含 HybridCLR DLL），再加载热更入口。
    /// </summary>
    public class GameMain : MonoBehaviour
    {
        private static GameMain _persistentInstance;

        private AssetBundleManager assetBundleManager;
        private IHotUpdateEntry hotUpdateEntry;
        private bool hotUpdateStarted;

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
            DisplaySettings.ApplySavedOrDefault();
#if CF_DEV_TOOLS || UNITY_EDITOR
            RuntimeLogConsole.EnsureInstalled();
#endif

            SRPScheduler.Init();
            AssetBundlePathHelper.Initialize();

            assetBundleManager = new AssetBundleManager();
            assetBundleManager.Init();
        }

        private void Start()
        {
            this.StartCoroutine(this.BootstrapRoutine());
        }

        private IEnumerator BootstrapRoutine()
        {
            AotResourceUpdateRunner.Result updateResult = AotResourceUpdateRunner.Result.Failed;
            yield return AotResourceUpdateRunner.Run(this, result => updateResult = result);

            if (updateResult != AotResourceUpdateRunner.Result.Success)
            {
                Debug.LogError($"[GameMain] 资源更新未成功: {updateResult}");
                yield break;
            }

            try
            {
                this.hotUpdateEntry = HybridCLRBootstrap.CreateEntry();
                this.hotUpdateEntry.Start(this.gameObject);
                this.hotUpdateStarted = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameMain] 热更入口启动失败: {e}");
            }
        }

        private void Update()
        {
            AssetBundleManager.Instance?.Update();

            if (!this.hotUpdateStarted || this.hotUpdateEntry == null)
            {
                return;
            }

            this.hotUpdateEntry.Tick(Time.deltaTime);

            if (this.hotUpdateEntry.IsQuitRequested)
            {
                this.OnApplicationQuit();
            }
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
            this.hotUpdateEntry?.ChangeState(state);
        }

        public Coroutine RunCoroutine(IEnumerator routine)
        {
            return this.StartCoroutine(routine);
        }
    }
}
