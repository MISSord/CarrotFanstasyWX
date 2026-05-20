using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>从 <see cref="LevelInfo"/> 填充格子状态与世界路径（PVE / 测试地图共用）。</summary>
    public static class BattleMapLayoutUtil
    {
        public static void ApplyGridStates(BattleMapGrid[,] gridsList, int xColumn, int yRow, LevelInfo levelInfo)
        {
            if (gridsList == null || levelInfo == null || levelInfo.gridPoints == null)
            {
                return;
            }

            for (int x = 0; x < xColumn; x++)
            {
                for (int y = 0; y < yRow; y++)
                {
                    int flat = y + x * yRow;
                    if (flat < levelInfo.gridPoints.Count)
                    {
                        gridsList[x, y].LoadGridInfo(levelInfo.gridPoints[flat]);
                    }
                }
            }
        }

        public static List<Fix64Vector2> BuildMonsterPathWorldPositions(
            BattleMapGrid[,] gridsList,
            int xColumn,
            int yRow,
            LevelInfo levelInfo,
            out Fix64Vector2 startPoint)
        {
            List<Fix64Vector2> pathList = new List<Fix64Vector2>();
            startPoint = Fix64Vector2.Zero;

            if (gridsList == null || levelInfo == null || levelInfo.monsterPath == null)
            {
                return pathList;
            }

            for (int i = 0; i < levelInfo.monsterPath.Count; i++)
            {
                BattleMapGrid.GridIndex idx = levelInfo.monsterPath[i];
                if (idx.xIndex >= 0 && idx.xIndex < xColumn && idx.yIndex >= 0 && idx.yIndex < yRow)
                {
                    BattleMapGrid mapGrid = gridsList[idx.xIndex, idx.yIndex];
                    pathList.Add(new Fix64Vector2(mapGrid.realX, mapGrid.realY));
                }
            }

            if (pathList.Count > 0)
            {
                startPoint = pathList[0];
            }
            else if (xColumn > 0 && yRow > 0)
            {
                BattleMapGrid g = gridsList[0, 0];
                startPoint = new Fix64Vector2(g.realX, g.realY);
            }

            return pathList;
        }

        public static void ComputeMapWorldBounds(
            BattleMapGrid[,] gridsList,
            int xColumn,
            int yRow,
            out Fix64Vector2 mapLeftBottom,
            out Fix64Vector2 mapRightTop)
        {
            Fix64 ratio = new Fix64(BattleConfig.MAP_RATIO / (float)2);
            mapLeftBottom = new Fix64Vector2(gridsList[0, 0].realX - ratio, gridsList[0, 0].realY - ratio);
            mapRightTop = new Fix64Vector2(
                gridsList[xColumn - 1, yRow - 1].realX + ratio,
                gridsList[xColumn - 1, yRow - 1].realY + ratio);
        }
    }
}
