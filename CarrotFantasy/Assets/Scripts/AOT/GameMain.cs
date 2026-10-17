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

                // 原来这里直接 yield break，真机上就表现为"永久黑屏、没有任何提示"。
                yield return AotResourceUpdateRunner.ShowFatalErrorDialog(
                    this,
                    "资源更新失败（" + updateResult + "），请重启游戏后重试。");
                yield break;
            }

            // C# 不允许在 catch 块里 yield（CS1631），所以先把异常信息记下来，出了 try 再弹面板。
            string entryError = null;
            try
            {
                this.hotUpdateEntry = HybridCLRBootstrap.CreateEntry();
                this.hotUpdateEntry.Start(this.gameObject);
                this.hotUpdateStarted = true;
            }
            catch (System.Exception e)
            {
                // 异常被吞掉后 hotUpdateStarted 会一直是 false，Update 静默 return，
                // 表现同样是永久黑屏。这里必须让玩家看见原因。
                Debug.LogError($"[GameMain] 热更入口启动失败: {e}");
                entryError = e.Message;
            }

            if (entryError != null)
            {
                yield return AotResourceUpdateRunner.ShowFatalErrorDialog(
                    this,
                    "热更代码加载失败，请重启游戏后重试。\n" + entryError);
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
