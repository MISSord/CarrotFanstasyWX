using UnityEngine;

namespace CarrotFantasy
{
    public class PveBattleView : BattleView_base
    {
        public PveBattleView(BaseBattle battle, BattleViewHost viewHost)
            : base(battle, viewHost)
        {
        }

        public override void Init()
        {
            this.EnsureViewComponentsRegistered();
            base.Init();
        }

        void EnsureViewComponentsRegistered()
        {
            if (this.HasRegisteredComponents)
            {
                return;
            }

            this.AddComponent(new BVSceneComponent(this));
            this.AddComponent(new BVBattleWorldUiComponent(this));
            this.AddComponent(new BVMapComponent(this));
            this.AddComponent(new BVMonsterComponent(this));
            this.AddComponent(new BVTowerComponent(this));
            this.AddComponent(new BVBulletComponent(this));
            this.AddComponent(new BVItemComponent(this));
            this.AddComponent(new BVUIComponent(this));
        }

        public override void ClearGameInfo()
        {
            base.ClearGameInfo();
            GameViewObjectPool.Instance.ClearGameInfo();
        }
    }
}
