using System;
using System.Collections.Generic;
using System.Text;

namespace CarrotFantasy
{
    public static class MapInfoHelper
    {
        // 格式
        // #1,1,1,1,1#1,2,1,1,1
        // 第一个为大关卡id 第二个为小关卡id，
        // 第三个为萝卜状态 1为正常 2为银 3为金, 0为没有
        // 第四个为是否全部清除道具 1为是，2为否
        // 第五个为是否解锁 1为是，2为否

        const char BigInfoSplitSign = '#';
        const string BlankMapInfo = "#{0},{1},{2},{3},{4}";

        public static string GetInitMapInfo()
        {
            return BuildSnapshot(MapConfigReader.CreateDefaultCells());
        }

        public static string MapInfoConvertString(SingleMapInfo mapInfo)
        {
            return string.Format(
                BlankMapInfo,
                mapInfo.bigLevelId,
                mapInfo.levelId,
                mapInfo.carrotState,
                mapInfo.isAllClear,
                mapInfo.unLocked);
        }

        public static string GetNewMapInfo(string oldMapInfo, SingleMapInfo mapInfo)
        {
            SingleMapInfo[] cells = MergeSnapshot(oldMapInfo);
            if (mapInfo != null)
            {
                ApplyCell(cells, mapInfo);
            }

            return BuildSnapshot(cells);
        }

        public static string BuildSnapshot(SingleMapInfo[] cells)
        {
            if (cells == null || cells.Length == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null)
                {
                    continue;
                }

                sb.Append(MapInfoConvertString(cells[i]));
            }

            return sb.ToString();
        }

        public static SingleMapInfo[] MergeSnapshot(string snapshot)
        {
            SingleMapInfo[] cells = MapConfigReader.CreateDefaultCells();
            Dictionary<int, SingleMapInfo> overlay = ParseSnapshot(snapshot);
            if (overlay.Count == 0)
            {
                return cells;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                SingleMapInfo cell = cells[i];
                SingleMapInfo saved;
                if (overlay.TryGetValue(MapConfigReader.MakeKey(cell.bigLevelId, cell.levelId), out saved))
                {
                    cell.carrotState = saved.carrotState;
                    cell.isAllClear = saved.isAllClear;
                    cell.unLocked = saved.unLocked;
                }
            }

            return cells;
        }

        public static Dictionary<int, SingleMapInfo> ParseSnapshot(string snapshot)
        {
            var result = new Dictionary<int, SingleMapInfo>();
            if (string.IsNullOrEmpty(snapshot))
            {
                return result;
            }

            string[] parts = snapshot.Split(BigInfoSplitSign);
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                string[] fields = parts[i].Split(',');
                if (fields.Length < 5)
                {
                    continue;
                }

                byte big;
                byte level;
                byte carrot;
                byte allClear;
                byte unlocked;
                if (!byte.TryParse(fields[0], out big)
                    || !byte.TryParse(fields[1], out level)
                    || !byte.TryParse(fields[2], out carrot)
                    || !byte.TryParse(fields[3], out allClear)
                    || !byte.TryParse(fields[4], out unlocked))
                {
                    continue;
                }

                result[MapConfigReader.MakeKey(big, level)] = new SingleMapInfo
                {
                    bigLevelId = big,
                    levelId = level,
                    carrotState = carrot,
                    isAllClear = allClear,
                    unLocked = unlocked,
                };
            }

            return result;
        }

        static void ApplyCell(SingleMapInfo[] cells, SingleMapInfo mapInfo)
        {
            int key = MapConfigReader.MakeKey(mapInfo.bigLevelId, mapInfo.levelId);
            for (int i = 0; i < cells.Length; i++)
            {
                SingleMapInfo cell = cells[i];
                if (cell == null)
                {
                    continue;
                }

                if (MapConfigReader.MakeKey(cell.bigLevelId, cell.levelId) == key)
                {
                    cell.carrotState = mapInfo.carrotState;
                    cell.isAllClear = mapInfo.isAllClear;
                    cell.unLocked = mapInfo.unLocked;
                    return;
                }
            }
        }
    }
}
