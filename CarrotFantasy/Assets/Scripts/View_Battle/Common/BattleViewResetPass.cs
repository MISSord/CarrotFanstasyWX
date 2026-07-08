namespace CarrotFantasy
{
    /// <summary>视图重开 <see cref="BaseBattleViewComponent.ResetRound"/> 阶段（由 <see cref="BattleView_base.ResetRound"/> 统一编排）。</summary>
    public enum BattleViewResetPass
    {
        /// <summary>Model 重置前：动态单位回池、移除监听。</summary>
        BeforeModel,
        /// <summary>Model 重置后：按新 Model 同步静态视图、重新绑定监听。</summary>
        AfterModel,
    }
}
