using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarrotFantasy
{
    public class BattleScene : BaseScene
    {
        public BattleScene(BaseSceneType type, string name, Dictionary<string, dynamic> param) : base(type, name, param)
        {
            this.prefabUrl = null;
        }

        public override void init()
        {
            base.init();
            this.AddListener();

            GameManager manager = this.gameObj.AddComponent<GameManager>();
            manager.init();
            manager.initBattle();

            Sche.delayExeOnceTimes(manager.startGame, 2.0f);
        }

        private void AddListener()
        {
            BusinessProvision.Instance.eventDispatcher.AddListener(CommonEventType.RETURN_TO_MAIN_SCENE, this.returnToMainScene);
        }

        private void RemoveListener()
        {
            BusinessProvision.Instance.eventDispatcher.RemoveListener(CommonEventType.RETURN_TO_MAIN_SCENE, this.returnToMainScene);
        }

        private void returnToMainScene()
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
