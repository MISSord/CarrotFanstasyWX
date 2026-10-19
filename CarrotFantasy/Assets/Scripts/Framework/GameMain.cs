using System.Collections;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// AOT 启动壳。
    /// <para>
    /// 启动分两段：
    /// 1. **资源更新**：由 <see cref="UpdateFlow"/> 状态机驱动，<see cref="Update"/> 每帧 Tick，
    ///    跑到终态（Done / Failed / Cancelled）后状态机自行关闭，之后不再产生任何开销。
    /// 2. **启动后半段**：Lua 环境初始化 + 业务入口启动，仍是协程（与资源无关，且是纯异步）。
    /// </para>
    /// </summary>
    public class GameMain : MonoBehaviour
    {
        private static GameMain _persistentInstance;

        private AssetBundleManager assetBundleManager;
        private IGameEntry gameEntry;

        /// <summary>资源更新状态机。</summary>
        private UpdateFlow updateFlow;

        /// <summary>一次性闩锁：保证"启动后半段"只触发一次。</summary>
        private bool bootStarted;

        /// <summary>业务入口是否已成功启动。</summary>
        private bool entryStarted;

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
            // 创建并启动状态机；之后由 Update 每帧 Tick，不再用协程编排更新流程。
            this.updateFlow = AotResourceUpdateRunner.CreateFlow(this);
            this.updateFlow.Start();
        }

        private void Update()
        {
            // ① 先让下载器等资源系统推进（状态机只读它们的状态，不驱动它们）。
            AssetBundleManager.Instance?.Update();
            LuaManager.Tick();

            // ② 更新阶段：只推进状态机，不进入业务。
            if (this.updateFlow != null && this.updateFlow.IsRunning)
            {
                this.updateFlow.Tick();
                return;
            }

            // ③ 状态机跑完：成功则进启动后半段；失败/取消已由状态机内部弹面板并退出。
            if (this.updateFlow != null && this.updateFlow.HasFinished && !this.bootStarted)
            {
                this.bootStarted = true;

                if (this.updateFlow.Result == AotResourceUpdateRunner.Result.Success)
                {
                    this.StartCoroutine(this.ContinueBoot());
                }

                return;
            }

            // ④ 业务阶段。
            if (!this.entryStarted || this.gameEntry == null)
            {
                return;
            }

            this.gameEntry.Tick(Time.deltaTime);

            if (this.gameEntry.IsQuitRequested)
            {
                this.OnApplicationQuit();
            }
        }

        /// <summary>
        /// 资源就绪后的启动后半段。保留协程：Lua 初始化是纯异步，与资源更新生命周期无关。
        /// </summary>
        private IEnumerator ContinueBoot()
        {
            Debug.Log("[GameMain] 资源就绪，开始启动 Lua 与业务入口");

            // Lua 环境初始化：失败也不阻断启动，只影响 GM 指令与 Lua 热更逻辑。
            yield return LuaManager.InitializeRoutine(this);

            // C# 不允许在 catch 块里 yield（CS1631），所以先把异常信息记下来，出了 try 再弹面板。
            string entryError = null;
            try
            {
                this.gameEntry = new GameEntry();
                this.gameEntry.Start(this.gameObject);
                this.entryStarted = true;
                Debug.Log("[GameMain] 业务入口已启动");
            }
            catch (System.Exception e)
            {
                // 入口没起来时 Update 会一直静默 return，表现为永久黑屏；这里必须让玩家看见原因。
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
            this.gameEntry?.ChangeState(state);
        }

        public Coroutine RunCoroutine(IEnumerator routine)
        {
            return this.StartCoroutine(routine);
        }
    }
}
