using CarrotFantasy;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static AssetBundleUpdateChecker;

// 游戏状态枚举
public enum GameState
{
    CheckUpdate,    // 检测更新
    Download,       // 下载AB包
    Login,          // 登录
    EnterGame,      // 进游戏
    InGame,         // 游戏中
    Restart,        // 重启游戏
    Exit,           // 游戏退出
    Error,          //游戏进程错误状态
}

// 状态机接口
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

// 共享数据上下文
public class GameContext
{
    // 更新相关数据
    public UpdateCheckResult result { get; set; }

    // 下载进度
    public float DownloadProgress { get; set; }

    // 清理上下文（重启时使用）
    public void Clear()
    {
        result = null;
    }
}

// 状态机管理器
public class GameStateMachine
{
    private Dictionary<GameState, IGameState> states = new Dictionary<GameState, IGameState>();
    private IGameState currentState;
    private GameContext gameContext;

#if UNITY_EDITOR
    private LoadMode loadMode;
#endif

    public void Init()
    {
        gameContext = new GameContext();

        // 初始化所有状态
        states.Add(GameState.CheckUpdate, new CheckUpdateState(gameContext));
        states.Add(GameState.Download, new DownloadState(gameContext));
        states.Add(GameState.EnterGame, new EnterGameState(gameContext));

#if UNITY_EDITOR
        loadMode = (LoadMode)EditorPrefs.GetInt("GameLoadMode", 0);
        if (loadMode == LoadMode.Development || loadMode == LoadMode.DebugMode)
        {
            ChangeState(GameState.EnterGame);
            return;
        }
#endif

        // 初始状态：检测更新
        ChangeState(GameState.CheckUpdate);
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

    public CheckUpdateState(GameContext context) : base(context)
    {
        this.context = context;
        checker = new AssetBundleUpdateChecker();
    }

    public override void Enter()
    {
        Debug.Log("进入检测更新流程");
        checker?.StartUpdateCheck(AssetBundlePathHelper.GetServerLoadUrl(), CheckResultCallBack);
    }

    private void CheckResultCallBack(UpdateCheckResult finalResutl)
    {
        context.result = finalResutl;
        isFinishCheck = true;
        if (finalResutl.totalDownloadSize > 0) //有需要下载的
        {
            Debug.Log(string.Format("校验完成回调，需要下载{0}B的资源", finalResutl.totalDownloadSize));
            //ViewManager.Instance.OpenView("");
        }
    }

    public override void Update()
    {
        checker?.Update();

        if (checker != null && checker.IsRunning == false && checker.CurrentState != CheckerState.Idle)
        {
            GameMain root = GameObject.FindObjectOfType<GameMain>();
            if (checker.CurrentState == CheckerState.Error)
            {
                //检测清单失败，进入失败状态
                root?.ChangeMachineState(GameState.Error);
                return;
            }

            //完成检查
            if (isFinishCheck == true)
            {
                //有需要更新，进入下载模式
                if (context.result.hasChanges == true)
                {
                    root?.ChangeMachineState(GameState.Download);
                }
                else
                {
                    AssetBundleManager.Instance.SetAssetBundleItem(context.result.customManifest);
                    root?.ChangeMachineState(GameState.EnterGame);
                }
            }
        }
    }

    public override void Exit()
    {
        Debug.Log("退出检测更新流程");
        checker?.EndCheck();
    }

    public override GameState GetStateType() => GameState.CheckUpdate;
}

public class DownloadState : BaseGameState
{
    private AssetBundleDownloader downloader;
    private bool isSetABMainifest = false;

    public DownloadState(GameContext gameContext) : base(gameContext)
    {
        this.downloader = AssetBundleDownloader.Instance;
        this.downloader.Init();
    }

    public override void Enter()
    {
        isSetABMainifest = false;
        Debug.Log("进入下载流程");
        downloader?.StartDownload(context, FinsihDownCallBack, FinishDownLoadCallBack);
    }

    //只完成了全部下载的回调
    private void FinsihDownCallBack(bool isSuccess)
    {
        Debug.Log("完成全部AB包下载");
    }

    //完成全部下载和解压的回调
    private void FinishDownLoadCallBack(bool isSuccess)
    {
        AssetBundleUpdateChecker.SaveLocalManifest(context.result.customManifest);
        AssetBundleManager.Instance.SetAssetBundleItem(context.result.customManifest);
        isSetABMainifest = true;
    }

    public override void Update()
    {
        downloader?.Update();

        //下载和解压完成，进行登录状态
        if (downloader != null && downloader.GetLoaderState() == LoaderState.Idle && isSetABMainifest == true)
        {
            GameMain root = GameObject.FindObjectOfType<GameMain>();
            root?.ChangeMachineState(GameState.EnterGame);
        }
    }

    public override void Exit()
    {
        Debug.Log("退出下载流程");
        downloader?.EndDownload();
    }

    public override GameState GetStateType() => GameState.Download;
}

public class LoginState : BaseGameState
{
    //private ILoginManager loginManager;
    //private DownLoadView downView;

    public LoginState(GameContext context) : base(context)
    {
        // loginManager = GameManager.Instance.LoginManager;
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

public class EnterGameState : BaseGameState
{
    //public TestView testView;
    //public DownLoadView downloadView;

    public EnterGameState(GameContext context) : base(context)
    {
        //testView = new TestView();
        //testView.RegisterData();

        //downloadView = new DownLoadView();
        //downloadView.RegisterData();
    }

    public override void Enter()
    {
        Debug.Log("进入进游戏流程");
        //testView.Open();
    }

    public override void Update()
    {

        //// 模拟进入游戏完成，开始游戏
        //GameRoot root = GameObject.FindObjectOfType<GameRoot>();
        //root?.ChangeMachineState(GameState.InGame);
    }

    public override void Exit()
    {
        Debug.Log("退出进游戏流程");
    }

    public override GameState GetStateType() => GameState.EnterGame;
}

public class InGameState : IGameState
{
    //private IInGameManager inGameManager;

    public InGameState()
    {
        // inGameManager = GameManager.Instance.InGameManager;
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
