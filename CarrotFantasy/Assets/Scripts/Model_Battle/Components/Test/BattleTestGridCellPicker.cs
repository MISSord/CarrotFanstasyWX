using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>从地图格子中随机选取出生点/目标点。</summary>
    public static class BattleTestGridCellPicker
    {
        public static void PickTwoDistinctCells(BattleTestMapComponent map, BattleTestRandom rng, out int startGx, out int startGy, out int targetGx, out int targetGy)
        {
            List<BattleMapGrid.GridIndex> path = map.LevelInfo != null ? map.LevelInfo.monsterPath : null;
            if (path != null && path.Count >= 2)
            {
                int startIdx = rng.NextInt(0, path.Count);
                int targetIdx = rng.NextInt(0, path.Count);
                int guard = 0;
                while (targetIdx == startIdx && guard < 32)
                {
                    targetIdx = rng.NextInt(0, path.Count);
                    guard++;
                }

                startGx = path[startIdx].xIndex;
                startGy = path[startIdx].yIndex;
                targetGx = path[targetIdx].xIndex;
                targetGy = path[targetIdx].yIndex;
                return;
            }

            startGx = rng.NextInt(0, map.xColumn);
            startGy = rng.NextInt(0, map.yRow);
            targetGx = rng.NextInt(0, map.xColumn);
            targetGy = rng.NextInt(0, map.yRow);
            int guard2 = 0;
            while ((targetGx == startGx && targetGy == startGy) && guard2 < 32)
            {
                targetGx = rng.NextInt(0, map.xColumn);
                targetGy = rng.NextInt(0, map.yRow);
                guard2++;
            }
        }

        public static Fix64Vector2 CellToWorld(BattleMapComponent map, int gx, int gy)
        {
            if (gx < 0)
            {
                gx = 0;
            }

            if (gy < 0)
            {
                gy = 0;
            }

            if (gx >= map.xColumn)
            {
                gx = map.xColumn - 1;
            }

            if (gy >= map.yRow)
            {
                gy = map.yRow - 1;
            }

            return map.GetMapGridPosition(gx, gy);
        }
    }
}
