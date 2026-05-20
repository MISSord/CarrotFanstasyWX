namespace CarrotFantasy
{
    /// <summary>带关卡 <see cref="LevelInfo"/> 的地图（PVE JSON 或测试合成数据）。</summary>
    public interface IBattleMapLevelData
    {
        LevelInfo LevelInfo { get; }
    }
}
