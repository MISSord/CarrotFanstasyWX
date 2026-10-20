using System;
using System.Collections.Generic;
using cfg;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 冒险大小关配置。数据来自 Luban <c>TbMapBigLevel</c> / <c>TbMapLevel</c>
    /// （Excel：<c>MapLevelInfo.xlsx</c> 的两个子表），按 <c>BigLevelId + LevelId</c> 查询。
    /// </summary>
    public static class MapConfigReader
    {
        static readonly List<int> bigLevelIds = new List<int>();
        static readonly Dictionary<int, List<int>> levelIdsByBig = new Dictionary<int, List<int>>();
        static readonly Dictionary<int, MapBigLevelDef> bigDefs = new Dictionary<int, MapBigLevelDef>();
        static readonly Dictionary<int, MapLevelDef> levelDefs = new Dictionary<int, MapLevelDef>();
        static readonly Dictionary<int, Stage> stages = new Dictionary<int, Stage>();
        static bool initialized;

        public static void InitConfig()
        {
            initialized = false;
            bigLevelIds.Clear();
            levelIdsByBig.Clear();
            bigDefs.Clear();
            levelDefs.Clear();
            stages.Clear();

            try
            {
                LoadFromLuban();
            }
            catch (Exception ex)
            {
                Debug.LogError("[MapConfigReader] Luban 加载失败: " + ex);
            }

            initialized = true;
            if (levelDefs.Count == 0)
            {
                Debug.LogError("[MapConfigReader] TbMapLevel 为空，请先导出 ConfigTools/gen_code_json.bat");
            }
        }

        public static void EnsureInit()
        {
            if (!initialized)
            {
                InitConfig();
            }
        }

        static void LoadFromLuban()
        {
            Tables tables = LubanConfigLoader.Tables;
            TbMapBigLevel bigTable = tables.TbMapBigLevel;
            TbMapLevel levelTable = tables.TbMapLevel;
            if (bigTable?.DataList != null)
            {
                for (int i = 0; i < bigTable.DataList.Count; i++)
                {
                    MapBigLevelDef def = bigTable.DataList[i];
                    bigDefs[def.BigLevelId] = def;
                    if (!bigLevelIds.Contains(def.BigLevelId))
                    {
                        bigLevelIds.Add(def.BigLevelId);
                    }
                }
            }

            if (levelTable?.DataList == null)
            {
                return;
            }

            for (int i = 0; i < levelTable.DataList.Count; i++)
            {
                MapLevelDef def = levelTable.DataList[i];
                int key = MakeKey(def.BigLevelId, def.LevelId);
                levelDefs[key] = def;
                stages[key] = ToStage(def);

                List<int> levelIds;
                if (!levelIdsByBig.TryGetValue(def.BigLevelId, out levelIds))
                {
                    levelIds = new List<int>();
                    levelIdsByBig[def.BigLevelId] = levelIds;
                }

                if (!levelIds.Contains(def.LevelId))
                {
                    levelIds.Add(def.LevelId);
                }

                if (!bigLevelIds.Contains(def.BigLevelId))
                {
                    bigLevelIds.Add(def.BigLevelId);
                }
            }

            bigLevelIds.Sort(CompareBigLevel);
            foreach (KeyValuePair<int, List<int>> kv in levelIdsByBig)
            {
                kv.Value.Sort();
            }
        }

        static int CompareBigLevel(int a, int b)
        {
            int orderA = GetSortOrderRaw(a);
            int orderB = GetSortOrderRaw(b);
            if (orderA != orderB)
            {
                return orderA.CompareTo(orderB);
            }

            return a.CompareTo(b);
        }

        static Stage ToStage(MapLevelDef def)
        {
            int[] towers = def.TowerIds != null ? def.TowerIds.ToArray() : Array.Empty<int>();
            return new Stage
            {
                mTowerIDList = towers,
                mTowerIDListLength = towers.Length,
                mTotalRound = def.TotalRound,
            };
        }

        public static int MakeKey(int bigLevel, int level)
        {
            return bigLevel * 100 + level;
        }

        public static bool Exists(int bigLevel, int level)
        {
            EnsureInit();
            return levelDefs.ContainsKey(MakeKey(bigLevel, level));
        }

        public static Stage GetSingleStage(int bigLevel, int level)
        {
            EnsureInit();
            Stage stage;
            if (stages.TryGetValue(MakeKey(bigLevel, level), out stage))
            {
                return stage;
            }

            return null;
        }

        public static MapLevelDef GetLevelDef(int bigLevel, int level)
        {
            EnsureInit();
            MapLevelDef def;
            if (levelDefs.TryGetValue(MakeKey(bigLevel, level), out def))
            {
                return def;
            }

            return null;
        }

        public static MapBigLevelDef GetBigLevelDef(int bigLevel)
        {
            EnsureInit();
            MapBigLevelDef def;
            if (bigDefs.TryGetValue(bigLevel, out def))
            {
                return def;
            }

            return null;
        }

        public static int GetSortOrder(int bigLevel)
        {
            EnsureInit();
            return GetSortOrderRaw(bigLevel);
        }

        static int GetSortOrderRaw(int bigLevel)
        {
            MapBigLevelDef def;
            if (bigDefs.TryGetValue(bigLevel, out def))
            {
                return def.SortOrder;
            }

            return bigLevel;
        }

        public static IReadOnlyList<int> GetBigLevelIds()
        {
            EnsureInit();
            return bigLevelIds;
        }

        public static IReadOnlyList<int> GetLevelIds(int bigLevel)
        {
            EnsureInit();
            List<int> ids;
            if (levelIdsByBig.TryGetValue(bigLevel, out ids))
            {
                return ids;
            }

            return Array.Empty<int>();
        }

        public static int GetLevelCount(int bigLevel)
        {
            return GetLevelIds(bigLevel).Count;
        }

        public static int GetBigLevelCount()
        {
            EnsureInit();
            return bigLevelIds.Count;
        }

        public static bool TryGetNextLevel(int bigLevel, int level, out int nextBig, out int nextLevel)
        {
            nextBig = 0;
            nextLevel = 0;
            IReadOnlyList<int> levelIds = GetLevelIds(bigLevel);
            int index = IndexOf(levelIds, level);
            if (index >= 0 && index + 1 < levelIds.Count)
            {
                nextBig = bigLevel;
                nextLevel = levelIds[index + 1];
                return true;
            }

            IReadOnlyList<int> bigIds = GetBigLevelIds();
            int bigIndex = IndexOf(bigIds, bigLevel);
            if (bigIndex < 0 || bigIndex + 1 >= bigIds.Count)
            {
                return false;
            }

            int followingBig = bigIds[bigIndex + 1];
            IReadOnlyList<int> followingLevels = GetLevelIds(followingBig);
            if (followingLevels.Count == 0)
            {
                return false;
            }

            nextBig = followingBig;
            nextLevel = followingLevels[0];
            return true;
        }

        public static (int big, int level) GetNextLevel(int bigLevel, int level)
        {
            int nextBig;
            int nextLevel;
            if (TryGetNextLevel(bigLevel, level, out nextBig, out nextLevel))
            {
                return (nextBig, nextLevel);
            }

            return (0, 0);
        }

        static int IndexOf(IReadOnlyList<int> list, int value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                {
                    return i;
                }
            }

            return -1;
        }

        public static SingleMapInfo CreateDefaultCell(int bigLevel, int level)
        {
            MapLevelDef def = GetLevelDef(bigLevel, level);
            var info = new SingleMapInfo
            {
                bigLevelId = (byte)bigLevel,
                levelId = (byte)level,
                carrotState = 0,
                isAllClear = MapInfoType.NOT_ALL_CLEAR,
                unLocked = MapInfoType.LOCK_LEVEL,
            };

            if (def != null && def.DefaultUnlocked)
            {
                info.unLocked = MapInfoType.UNLOCK_LEVEL;
            }

            return info;
        }

        public static SingleMapInfo[] CreateDefaultCells()
        {
            EnsureInit();
            var cells = new List<SingleMapInfo>(levelDefs.Count);
            IReadOnlyList<int> bigIds = GetBigLevelIds();
            for (int i = 0; i < bigIds.Count; i++)
            {
                int big = bigIds[i];
                IReadOnlyList<int> levelIds = GetLevelIds(big);
                for (int j = 0; j < levelIds.Count; j++)
                {
                    cells.Add(CreateDefaultCell(big, levelIds[j]));
                }
            }

            return cells.ToArray();
        }
    }

    public class Stage
    {
        public int[] mTowerIDList;
        public int mTowerIDListLength;
        public int mTotalRound;
    }
}
