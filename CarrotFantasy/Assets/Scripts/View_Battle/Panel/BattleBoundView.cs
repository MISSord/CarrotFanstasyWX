using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>战斗内 UI：须在 Open 前由 Session 注入 battle，禁止运行时回查全局会话。</summary>
    public abstract class BattleBoundView : BaseView
    {
        protected BaseBattle battle;
        protected BattleDataComponent dataComponent;
        protected BattlePVEDataComponent pveDataComponent;

        public bool BindBattle(BaseBattle battleKernel)
        {
            this.ClearBattleBinding();

            if (battleKernel == null)
            {
                Debug.LogError("[BattleBoundView] BindBattle 失败：battle 为空。");
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
    }
}
