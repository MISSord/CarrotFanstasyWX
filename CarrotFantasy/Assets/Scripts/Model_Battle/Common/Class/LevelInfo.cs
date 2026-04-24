using System.Collections.Generic;

namespace CarrotFantasy
{
    public class LevelInfo
    {
        public int bigLevelID;
        public int levelID;

        public List<BattleMapGrid.GridState> gridPoints;

        public List<BattleMapGrid.GridIndex> monsterPath;

        public List<Round.RoundInfo> roundInfo;
    }
}
