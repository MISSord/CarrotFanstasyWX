using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>肉鸽大地图场景（Unity 场景名 Scene），加载后绑定 <see cref="RoguelikeRunManager"/>。</summary>
    public class RoguelikeMapScene : BaseScene
    {
        public RoguelikeMapScene(BaseSceneType type, string name, Dictionary<string, dynamic> param)
            : base(type, name, param)
        {
            this.prefabUrl = null;
        }

        public override void InitSceneObject()
        {
            this.gameObj = GameObject.Find("HexWorldMapRoot");
            if (this.gameObj == null)
            {
                HexWorldMapController controller = Object.FindObjectOfType<HexWorldMapController>();
                if (controller != null)
                {
                    this.gameObj = controller.gameObject;
                }
            }
            if (this.gameObj == null)
            {
                this.gameObj = new GameObject("RoguelikeMapRoot");
            }
        }

        public override void Init()
        {
            base.Init();
            Sche.DelayExeOnceTimes(DelayedBindHexMap, 0.05f);
        }

        static void DelayedBindHexMap()
        {
            HexWorldMapController controller = Object.FindObjectOfType<HexWorldMapController>();
            if (controller != null)
            {
                RoguelikeRunManager.EnsureOn(controller);
            }
            else
            {
                Debug.LogWarning("[RoguelikeMapScene] No HexWorldMapController in scene.");
            }
        }
    }
}
