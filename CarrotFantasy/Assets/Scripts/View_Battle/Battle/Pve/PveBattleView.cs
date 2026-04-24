namespace CarrotFantasy
{
    public class PveBattleView : BattleView_base
    {

        public PveBattleView(BaseBattle battle) : base(battle)
        {
            this.rootGameObject = ServerProvision.sceneServer.currentScene.gameObj;
        }

        public override void init()
        {
            this.addComponent(new BVSceneComponent(this));
            this.addComponent(new BVMapComponent(this));
            this.addComponent(new BVMonsterComponent(this));
            this.addComponent(new BVTowerComponent(this));
            this.addComponent(new BVBulletComponent(this));
            this.addComponent(new BVItemComponent(this));
            this.addComponent(new BVUIComponent(this));
        }

        public override void clearGameInfo()
        {
            base.clearGameInfo();
            GameViewObjectPool.Instance.clearGameInfo();
        }

    }
}
