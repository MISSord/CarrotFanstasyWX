using LitJson;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class MapConfigReader
    {
        public static List<Stage> stageList;
        public static string filePath;

        public static void InitConfig()
        {
            filePath = "Game/Json/MapConfig.json";
            string jsonStr = GameJsonLoader.LoadMapConfigJsonText();
            if (!string.IsNullOrEmpty(jsonStr))
            {
                PlayerManager playerManager = JsonMapper.ToObject<PlayerManager>(jsonStr);
                stageList = playerManager.unLockedNormalModelLevelList;
            }
            else
            {
                Debug.LogError("MapConfig 读取失败");
            }
        }

        public static Stage GetSingleStage(int bigLevel, int level)
        {
            if (((bigLevel - 1) * 5 + level - 1) <= (stageList.Count - 1))
            {
                return stageList[(bigLevel - 1) * 5 + level - 1];
            }
            return null;
        }
    }

    public class Stage
    {
        public int[] mTowerIDList; //本关卡可以建的塔种类
        public int mTowerIDListLength; //建塔数组长度
        public int mTotalRound; //一共几波怪
    }

    public class PlayerManager
    {
        public List<Stage> unLockedNormalModelLevelList;//所有的小关卡
    }
}
