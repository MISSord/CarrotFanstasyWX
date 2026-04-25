using System.Collections.Generic;

namespace CarrotFantasy
{
    public class BattleScene : BaseScene
    {
        public BattleScene(BaseSceneType type, string name, Dictionary<string, dynamic> param) : base(type, name, param)
        {
            this.prefabUrl = null;
        }

        public override void Init()
        {
            base.Init();
            this.AddListener();

            GameManager manager = this.gameObj.AddComponent<GameManager>();
            manager.Init();
            manager.InitBattle();

            Sche.DelayExeOnceTimes(manager.StartGame, 2.0f);
        }

        private void AddListener()
        {
            BusinessProvision.Instance.eventDispatcher.AddListener(CommonEventType.RETURN_TO_MAIN_SCENE, this.ReturnToMainScene);
        }

        private void RemoveListener()
        {
            BusinessProvision.Instance.eventDispatcher.RemoveListener(CommonEventType.RETURN_TO_MAIN_SCENE, this.ReturnToMainScene);
        }

        private void ReturnToMainScene()
        {
            ServerProvision.sceneServer.LoadScene(BaseSceneType.MainScene, null);
        }

        public override void Dispose()
        {
            GameManager.Instance.Dispose();
            this.RemoveListener();
            base.Dispose();
        }
    }
}
