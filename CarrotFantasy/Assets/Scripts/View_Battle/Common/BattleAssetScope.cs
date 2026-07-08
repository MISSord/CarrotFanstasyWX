using System;

namespace CarrotFantasy
{
    /// <summary>
    /// Session 级战斗视图 AB 作用域：首次 EnsureLoaded 加载，同 Session 重开复用，DestroySession/EndRound 时 Release。
    /// </summary>
    public sealed class BattleAssetScope
    {
        bool loaded;

        public bool IsLoaded
        {
            get { return this.loaded && BattleViewAssetPreloader.HasCriticalAssets(); }
        }

        /// <summary>已加载则同步成功；否则异步预加载，成功后标记 loaded。</summary>
        public void EnsureLoaded(
            BaseBattle battle,
            Action onSuccess,
            Action onFailure,
            float timeoutSeconds = BattleViewPreloadWait.DefaultTimeoutSeconds)
        {
            if (this.IsLoaded)
            {
                BattleFlowLog.Step("2/4 预加载跳过", "AssetScope 已加载");
                onSuccess?.Invoke();
                return;
            }

            BattleViewAssetPreloader.Run(
                battle,
                onSuccess: () =>
                {
                    if (BattleViewAssetPreloader.HasCriticalAssets())
                    {
                        this.loaded = true;
                        onSuccess?.Invoke();
                    }
                    else
                    {
                        onFailure?.Invoke();
                    }
                },
                onFailure: onFailure,
                timeoutSeconds: timeoutSeconds);
        }

        /// <summary>离关或销毁 Session 时释放预加载缓存。</summary>
        public void Release()
        {
            BattleViewPrefabPreloader.Clear();
            BattleViewSpritePreloader.Clear();
            this.loaded = false;
        }
    }
}
