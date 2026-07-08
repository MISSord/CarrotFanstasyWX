using System;

namespace CarrotFantasy
{
    /// <summary>战斗内 UI：须在 Open 前由 Session 注入 battle，禁止运行时回查全局会话。</summary>
    public abstract class BattleBoundView : BaseView
    {
        protected BaseBattle battle;
        protected BattleDataComponent dataComponent;
        protected BattlePVEDataComponent pveDataComponent;

        Action pendingBattleOpenCallback;

        public bool BindBattle(BaseBattle battleKernel)
        {
            this.OnBeforeClearBattleBinding();
            this.ClearBattleBinding();

            if (battleKernel == null)
            {
                UnityEngine.Debug.LogError("[BattleBoundView] BindBattle 失败：battle 为空。");
                return false;
            }

            this.battle = battleKernel;
            this.dataComponent =
                (BattleDataComponent)battleKernel.GetComponent(BattleComponentType.DataComponent);
            this.pveDataComponent = BattlePVEDataComponent.GetFrom(battleKernel);
            return true;
        }

        protected bool IsBattleBound
        {
            get { return this.battle != null; }
        }

        protected void ClearBattleBinding()
        {
            this.battle = null;
            this.dataComponent = null;
            this.pveDataComponent = null;
        }

        /// <summary>换绑 battle 前解绑旧局监听，避免缓存面板复用时残留旧 Session 订阅。</summary>
        protected virtual void OnBeforeClearBattleBinding()
        {
        }

        /// <summary>UI index 已加载后，重新绑定当前 battle（缓存复开 / LoadCallBack 后）。</summary>
        protected virtual void RefreshBattleBinding()
        {
        }

        public void SetPendingBattleOpenCallback(Action callback)
        {
            this.pendingBattleOpenCallback = callback;
        }

        /// <summary>UI 已就绪时刷新绑定并触发 pending 回调；否则等待 LoadCallBack 调用 <see cref="NotifyBattleUiReady"/>。</summary>
        public bool TryCompleteBattleOpen()
        {
            if (!this.IsBattleBound || !this.GetIsLoadedIndex(0))
            {
                return false;
            }

            this.RefreshBattleBinding();
            this.InvokePendingBattleOpenCallback();
            return true;
        }

        /// <summary>异步 LoadCallBack 完成后由子类调用，触发 pending 回调。</summary>
        protected void NotifyBattleUiReady()
        {
            if (!this.IsBattleBound || !this.GetIsLoadedIndex(0))
            {
                return;
            }

            this.InvokePendingBattleOpenCallback();
        }

        void InvokePendingBattleOpenCallback()
        {
            if (this.pendingBattleOpenCallback == null)
            {
                return;
            }

            Action callback = this.pendingBattleOpenCallback;
            this.pendingBattleOpenCallback = null;
            callback();
        }
    }
}
