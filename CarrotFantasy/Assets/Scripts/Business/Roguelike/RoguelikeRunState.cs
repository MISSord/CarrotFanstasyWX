using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>一次肉鸽 Run 的持久数据（跨战斗、跨大地图节点）。</summary>
    [Serializable]
    public class RoguelikeRunState
    {
        public int bigLevelId;
        public int levelId;
        public int mapId;
        public string hexMapAssetId;
        public int shopPoolId;
        public int encounterTableId;
        public int randomEventPoolId;
        public int runSeed;

        public int roguelikeGold;
        public List<int> ownedItemIds = new List<int>();

        /// <summary>开局自带效果 id，不进商店背包。</summary>
        public List<int> startingEffectIds = new List<int>();

        public HexWorldProgress mapProgress = new HexWorldProgress();

        /// <summary>当前商店节点 id；0 表示未打开商店。</summary>
        public int activeShopPointId;

        public bool isActive;
    }
}
