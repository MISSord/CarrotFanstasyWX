namespace CarrotFantasy
{
    public class PveBattleView : BattleView_base
    {

        public PveBattleView(BaseBattle battle) : base(battle)
        {
            this.rootGameObject = ServerProvision.sceneServer.currentScene.gameObj;
        }

        public override void Init()
        {
            this.AddComponent(new BVSceneComponent(this));
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
