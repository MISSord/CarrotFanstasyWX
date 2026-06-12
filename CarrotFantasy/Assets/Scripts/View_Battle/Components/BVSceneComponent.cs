using System;
using UnityEngine;

namespace CarrotFantasy
{
    public class BVSceneComponent : BaseBattleViewComponent
    {
        public BVSceneComponent(BattleView_base battleView) : base(battleView)
        {
            this.componentType = BattleViewComponentType.SCENE;
        }

        public override void Init()
        {
        }

        public GameObject RegisterGameContainer(String name)
        {
            BattleViewHost host = this.battleView != null ? this.battleView.ViewHost : null;
            if (host == null)
            {
                Debug.LogError("[BVSceneComponent] BattleViewHost 未绑定，无法注册: " + name);
                return null;
            }

            return host.RegisterContainer(name);
        }

        public override void ClearGameInfo()
        {
            base.ClearGameInfo();
        }

        public void TearDownRegisteredContainers()
        {
            if (this.battleView != null && this.battleView.ViewHost != null)
            {
                this.battleView.ViewHost.ClearRegisteredContainers();
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
