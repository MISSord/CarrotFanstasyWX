namespace CarrotFantasy
{
    /// <summary>
    /// 表现层事件命名空间说明：打开面板、特效等应对内核事件做翻译，
    /// 避免在 Model 内直接 Dispatch UI 语义串。
    /// 兼容旧代码仍可使用 <see cref="BattleEvent"/> 中与 UI 相关的常量订阅。
    /// </summary>
    public static class BattlePresentationEvent
    {
        public const string OpenPveVictoryFlow = BattleEvent.SHOW_GAME_FINISH_PAGE;
        public const string OpenPveDefeatFlow = BattleEvent.SHOW_GAME_OVER_PAGE;
    }
}
