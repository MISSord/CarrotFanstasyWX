using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>战斗场景 <see cref="BaseScene"/> 参数字典键。</summary>
    public static class BattleSceneParamKeys
    {
        public const string PveLaunchParams = "pveLaunchParams";

        public static PveModelBattleParams TryGetPveLaunchParams(Dictionary<string, dynamic> sceneParam)
        {
            if (sceneParam == null)
            {
                return null;
            }

            if (!sceneParam.TryGetValue(PveLaunchParams, out dynamic value))
            {
                return null;
            }

            return value as PveModelBattleParams;
        }
    }
}
