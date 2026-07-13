/// <summary>
/// 启动/热更流程共享上下文。放在 AOT，供下载器与热更状态机共用。
/// </summary>
public class GameContext
{
    public UpdateCheckResult result { get; set; }

    public float DownloadProgress { get; set; }

    public void Clear()
    {
        result = null;
    }
}
