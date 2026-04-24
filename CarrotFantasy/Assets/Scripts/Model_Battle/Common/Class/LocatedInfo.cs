using System.Collections.Generic;

namespace CarrotFantasy
{
    public class LocatedInfo
    {
        public float minCoordX, maxCoordX, minCoordY, maxCoordY;

        public List<BattleMapGrid> locatedGrids;

        public LocatedInfo(float minCoordX, float maxCoordX, float minCoordY, float maxCoordY, List<BattleMapGrid> list)
        {
            this.minCoordX = minCoordX;
            this.minCoordY = minCoordY;
            this.maxCoordX = maxCoordX;
            this.maxCoordY = maxCoordY;

            this.locatedGrids = list;
        }
    }
}
