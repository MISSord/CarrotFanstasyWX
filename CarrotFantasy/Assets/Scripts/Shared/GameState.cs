namespace CarrotFantasy
{
    /// <summary>游戏启动/运行状态。放 Shared，供 AOT 与热更程序集共用。</summary>
    public enum GameState
    {
        CheckUpdate,
        DownloadConfirm,
        Download,
        Login,
        SelectGameMode,
        EnterGame,
        InGame,
        Restart,
        Exit,
        Error,
    }
}
