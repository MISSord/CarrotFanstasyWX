using CarrotFantasy;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 状态机接口（GameState 枚举在 CarrotFantasy.Shared）
public interface IGameState
{
    void Enter();
    void Update();
    void Exit();
    GameState GetStateType();
}

// 状态基类，包含上下文引用
public abstract class BaseGameState : IGameState
{
    protected GameContext context;

    protected BaseGameState(GameContext context)
    {
        this.context = context;
    }

    public abstract void Enter();
    public abstract void Update();
    public abstract void Exit();
    public abstract GameState GetStateType();
}

// 状态机管理器（GameContext 在 CarrotFantasy.AOT）
public class GameStateMachine
{
    private Dictionary<GameState, IGameState> states = new Dictionary<GameState, IGameState>();
    private IGameState currentState;
    private GameContext gameContext;
    private GameMain curMain;

#if UNITY_EDITOR
    private LoadMode loadMode;
#endif

    public void Init(GameMain main)
    {
        this.curMain = main;

        gameContext = new GameContext();

        // 初始化所有状态
        states.Add(GameState.CheckUpdate, new CheckUpdateState(gameContext));
        states.Add(GameState.DownloadConfirm, new DownloadConfirmState(gameContext));
        states.Add(GameState.Download, new DownloadState(gameContext));
        states.Add(GameState.SelectGameMode, new SelectGameModeState(gameContext));
        states.Add(GameState.EnterGame, new EnterGameState(gameContext));
        states.Add(GameState.Error, new ErrorState(gameContext));

        ViewManager.Instance.OpenView<StartLoadPanel>();

        // 资源更新（含 HybridCLR DLL）已在 AOT 的 AotResourceUpdateRunner 完成，
        // 热更层从选模式开始；配置表在此重新加载以吃到最新 AB。
        LubanConfigLoader.Reload();
        GameJsonLoader.Reload();

#if UNITY_EDITOR
        loadMode = (LoadMode)EditorPrefs.GetInt("GameLoadMode", 0);
#endif
        ChangeState(GameState.SelectGameMode);
    }

    public void Update(float deltaTime)
    {
        if (currentState != null)
        {
            currentState.Update();
        }
    }

    public void ChangeState(GameState newState)
    {
        if (states.ContainsKey(newState))
        {
            Debug.Log($"开始切换到状态: {newState}");
            if (currentState != null)
            {
                currentState.Exit();
            }

            currentState = states[newState];
            currentState.Enter();
        }
    }
}

// 具体状态实现
public class CheckUpdateState : BaseGameState
{
    private AssetBundleUpdateChecker checker;
    private bool isCanDownLoad = false;
    private bool isFinishCheck = false;
    private bool useTestingBootstrap;

    public CheckUpdateState(GameContext context) : base(context)
    {
        this.context = context;
        checker = new AssetBundleUpdateChecker();
    }

    public override void Enter()
    {
        Debug.Log("进入检测更新流程");
        context.Clear();
        useTestingBootstrap = false;
        isFinishCheck = false;

#if UNITY_EDITOR
        LoadMode loadMode = (LoadMode)EditorPrefs.GetInt("GameLoadMode", 0);
        if (loadMode == LoadMode.Testing && AssetBundleUpdateChecker.TryBootstrapTestingCheck(out UpdateCheckResult testingResult))
        {
            context.result = testingResult;
            isFinishCheck = true;
            useTestingBootstrap = true;
            AssetBundleManager.Instance.SetAssetBundleItem(testingResult.customManifest);
            Debug.Log("[Testing] 使用本地 AB 清单，跳过远程清单下载。");
            return;
        }
#endif

        checker?.StartUpdateCheck(AssetBundlePathHelper.GetServerLoadUrl(), CheckResultCallBack);
    }

    private void CheckResultCallBack(UpdateCheckResult finalResutl)
    {
        context.result = finalResutl;
        isFinishCheck = true;
        if (finalResutl.totalDownloadSize > 0) //有需要下载的
        {
            Debug.Log(string.Format("校验完成回调，需要下载{0}B的资源", finalResutl.totalDownloadSize));
            // 需要下载资源，进入 DownloadConfirmState 后弹出 IMGUI 确认对话框
        }
    }

    public override void Update()
    {
        if (useTestingBootstrap && isFinishCheck)
        {
            HandleCheckFinished();
            return;
        }

        checker?.Update();

        if (checker != null && checker.IsRunning == false && checker.CurrentState != CheckerState.Idle)
        {
            HandleCheckFinished();
        }
    }

    private void HandleCheckFinished()
    {
        if (!isFinishCheck)
        {
            return;
        }

        GameMain root = GameObject.FindObjectOfType<GameMain>();
        if (useTestingBootstrap)
        {
            if (context.result.hasChanges)
            {
                root?.ChangeMachineState(GameState.DownloadConfirm);
            }
            else
            {
                AssetBundleManager.Instance.SetAssetBundleItem(context.result.customManifest);
                root?.ChangeMachineState(GameState.SelectGameMode);
            }

            useTestingBootstrap = false;
            isFinishCheck = false;
            return;
        }

        if (checker.CurrentState == CheckerState.Error)
        {
            root?.ChangeMachineState(GameState.Error);
            isFinishCheck = false;
            return;
        }

        if (context.result.hasChanges == true)
        {
            root?.ChangeMachineState(GameState.DownloadConfirm);
        }
        else
        {
            AssetBundleManager.Instance.SetAssetBundleItem(context.result.customManifest);
            root?.ChangeMachineState(GameState.SelectGameMode);
        }

        isFinishCheck = false;
    }

    public override void Exit()
    {
        Debug.Log("退出检测更新流程");
        checker?.EndCheck();
    }

    public override GameState GetStateType() => GameState.CheckUpdate;
}

public class DownloadConfirmState : BaseGameState
{
    private GameObject dialogObject;

    public DownloadConfirmState(GameContext context) : base(context)
    {
    }

    public override void Enter()
    {
        Debug.Log("进入下载确认流程");
        ShowDialog();
    }

    public override void Update()
    {
    }

    public override void Exit()
    {
        Debug.Log("退出下载确认流程");
        if (dialogObject != null)
        {
            GameObject.Destroy(dialogObject);
            dialogObject = null;
        }
    }

    public override GameState GetStateType() => GameState.DownloadConfirm;

    private void ShowDialog()
    {
        if (context.result == null)
        {
            Debug.LogError("下载确认：更新结果为空，无法显示对话框");
            return;
        }

        dialogObject = new GameObject("DownloadConfirmDialog");
        GameMain main = GameObject.FindObjectOfType<GameMain>();
        if (main != null)
        {
            dialogObject.transform.SetParent(main.transform, false);
        }

        DownloadConfirmDialog dialog = dialogObject.AddComponent<DownloadConfirmDialog>();
        dialog.Setup(context.result.totalDownloadSize, OnDownloadClicked, OnExitClicked);
    }

    private void OnDownloadClicked()
    {
        GameMain root = GameObject.FindObjectOfType<GameMain>();
        root?.ChangeMachineState(GameState.Download);
    }

    private void OnExitClicked()
    {
        BusinessProvision.Instance.eventDispatcher.DispatchEvent(CommonEventType.GAME_QUIT);
    }
}

public class DownloadState : BaseGameState
{
    private AssetBundleDownloader downloader;
    private bool isDownloadFinished;
    private bool isDownloadSuccess;
    private GameObject progressDialogObject;

    public DownloadState(GameContext gameContext) : base(gameContext)
    {
        this.downloader = AssetBundleDownloader.Instance;
        this.downloader.Init();
    }

    public override void Enter()
    {
        isDownloadFinished = false;
        isDownloadSuccess = false;
        Debug.Log("进入下载流程");
        CreateProgressDialog();
        downloader?.StartDownload(context, OnAllDownloadsFinished, OnAllDownloadsAndConvertsFinished);
    }

    private void CreateProgressDialog()
    {
        progressDialogObject = new GameObject("DownloadProgressDialog");
        GameMain main = GameObject.FindObjectOfType<GameMain>();
        if (main != null)
        {
            progressDialogObject.transform.SetParent(main.transform, false);
        }

        DownloadProgressDialog dialog = progressDialogObject.AddComponent<DownloadProgressDialog>();
        dialog.Setup(downloader);
    }

    /// <summary>全部下载任务结束（尚未等转换时也可能触发；当前 Downloader 主要走 completeCallback）。</summary>
    private void OnAllDownloadsFinished(bool isSuccess)
    {
        Debug.Log(isSuccess ? "完成全部 AB 包下载" : "AB 包下载存在失败");
    }

    /// <summary>全部下载与解压/转换结束。</summary>
    private void OnAllDownloadsAndConvertsFinished(bool isSuccess)
    {
        isDownloadFinished = true;
        isDownloadSuccess = isSuccess;
        if (!isSuccess)
        {
            Debug.LogError("AB 包下载未全部成功，不更新本地清单");
            return;
        }

        // 仅全部成功后写本地清单，避免失败时远程 Hash 落盘导致下次误判已最新。
        AssetBundleUpdateChecker.SaveLocalManifest(context.result.customManifest);
        AssetBundleManager.Instance.SetAssetBundleItem(context.result.customManifest);
        LubanConfigLoader.Reload();
        GameJsonLoader.Reload();
    }

    public override void Update()
    {
        downloader?.Update();

        if (!isDownloadFinished)
        {
            return;
        }

        GameMain root = GameObject.FindObjectOfType<GameMain>();
        if (!isDownloadSuccess)
        {
            root?.ChangeMachineState(GameState.Error);
            return;
        }

        // 下载和解压完成，进入模式选择
        if (downloader != null && downloader.GetLoaderState() == LoaderState.Idle)
        {
            root?.ChangeMachineState(GameState.SelectGameMode);
        }
    }

    public override void Exit()
    {
        Debug.Log("退出下载流程");
        if (progressDialogObject != null)
        {
            GameObject.Destroy(progressDialogObject);
            progressDialogObject = null;
        }
        downloader?.EndDownload();
        isDownloadFinished = false;
        isDownloadSuccess = false;
    }

    public override GameState GetStateType() => GameState.Download;
}

/// <summary>热更检查或下载失败时的兜底状态：提示后退出。</summary>
public class ErrorState : BaseGameState
{
    public ErrorState(GameContext context) : base(context)
    {
    }

    public override void Enter()
    {
        Debug.LogError("进入错误状态：资源更新失败");
#if UNITY_EDITOR
        if (UnityEditor.EditorUtility.DisplayDialog(
                "资源更新失败",
                "资源下载或校验失败，请检查网络后重试。",
                "退出"))
        {
            UnityEditor.EditorApplication.isPlaying = false;
        }
#else
        BusinessProvision.Instance.eventDispatcher.DispatchEvent(CommonEventType.GAME_QUIT);
#endif
    }

    public override void Update()
    {
    }

    public override void Exit()
    {
        Debug.Log("退出错误状态");
    }

    public override GameState GetStateType() => GameState.Error;
}

public class LoginState : BaseGameState
{
    //private ILoginManager loginManager;
    //private DownLoadView downView;

    public LoginState(GameContext context) : base(context)
    {
        //downView = new DownLoadView();
        //downView.RegisterData();
    }

    public override void Enter()
    {
        Debug.Log("进入登录流程");
        //downView.Open();
    }

    public override void Update()
    {
        //loginManager?.UpdateLogin();

        //// 登录成功后进入游戏
        //if (loginManager != null && loginManager.IsLoginSuccess())
        //{
        //    GameStateMachine machine = GameObject.FindObjectOfType<GameStateMachine>();
        //    machine?.ChangeState(GameState.EnterGame);
        //}
    }

    public override void Exit()
    {
        Debug.Log("退出登录流程");
    }

    public override GameState GetStateType() => GameState.Login;
}

public class SelectGameModeState : BaseGameState
{
    public SelectGameModeState(GameContext context) : base(context)
    {
    }

    public override void Enter()
    {
        Debug.Log("进入游玩模式选择");
        GameMain main = GameObject.FindObjectOfType<GameMain>();
        if (main == null)
        {
            return;
        }

        GameModeSelectGui gui = main.GetComponent<GameModeSelectGui>();
        if (gui == null)
        {
            gui = main.gameObject.AddComponent<GameModeSelectGui>();
        }

        gui.Show();
    }

    public override void Update()
    {
    }

    public override void Exit()
    {
        Debug.Log("退出游玩模式选择");
    }

    public override GameState GetStateType() => GameState.SelectGameMode;
}

public class EnterGameState : BaseGameState
{
    public EnterGameState(GameContext context) : base(context)
    {

    }

    public override void Enter()
    {
        Debug.Log("进入进游戏流程");
        ViewManager.Instance.CloseView<StartLoadPanel>();
        UIServer.Instance.TryLoadDeferredAbUi();
        ViewManager.Instance.OpenView<MainPanel>();
    }

    public override void Update()
    {

    }

    public override void Exit()
    {
        Debug.Log("退出进游戏流程");
    }

    public override GameState GetStateType() => GameState.EnterGame;
}

public class InGameState : IGameState
{

    public InGameState()
    {
    }

    public void Enter()
    {
        Debug.Log("进入游戏中流程");
        //inGameManager?.StartInGame();
    }

    public void Update()
    {
        //inGameManager?.UpdateInGame();

        // 这里处理游戏中的状态转换逻辑
        // 例如：重启游戏、退出游戏等
        // 这部分逻辑可以根据具体游戏需求实现
    }

    public void Exit()
    {
        Debug.Log("退出游戏中流程");
        //inGameManager?.EndInGame();
    }

    public GameState GetStateType() => GameState.InGame;
}

//public class RestartState : IGameState
//{
//    public void Enter()
//    {
//        Debug.Log("进入重启游戏流程");
//        // 执行重启逻辑，比如清理资源、重置数据等

//        // 重启完成后回到检测更新状态
//        GameStateMachine machine = GameObject.FindObjectOfType<GameStateMachine>();
//        machine?.ChangeState(GameState.CheckUpdate);
//    }

//    public void Update()
//    {
//        // 重启流程通常比较快，可能不需要每帧更新
//    }

//    public void Exit()
//    {
//        Debug.Log("退出重启游戏流程");
//    }

//    public GameState GetStateType() => GameState.Restart;
//}

//public class ExitState : IGameState
//{
//    public void Enter()
//    {
//        Debug.Log("进入游戏退出流程");
//        // 执行退出逻辑，比如保存数据、清理资源等

//        // 退出游戏
//#if UNITY_EDITOR
//        UnityEditor.EditorApplication.isPlaying = false;
//#else
//            Application.Quit();
//#endif
//    }

//    public void Update()
//    {
//        // 退出流程通常不需要更新
//    }

//    public void Exit()
//    {
//        Debug.Log("退出游戏退出流程");
//    }

//    public GameState GetStateType() => GameState.Exit;
//}
