using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>GM 用地图进度读写（Editor 窗口与运行时调试共用）。</summary>
    public static class GmMapProgressService
    {
        public const int MaxBigLevel = 3;
        public const int LevelsPerBig = 5;

        public static long ResolveUserId(long fallbackUserId = StandaloneBackendMock.DefaultUserId)
        {
            if (Application.isPlaying && AccountServer.Instance != null && AccountServer.Instance.userId > 0)
            {
                return AccountServer.Instance.userId;
            }

            return fallbackUserId > 0 ? fallbackUserId : StandaloneBackendMock.DefaultUserId;
        }

        public static string LoadPersistedSnapshot(long userId)
        {
            return StandaloneMapPersistence.TryLoad(ResolveUserId(userId));
        }

        public static string BuildResetSnapshot()
        {
            return MapInfoHelper.GetInitMapInfo();
        }

        public static string BuildUnlockAllSnapshot(byte clearedCarrotState = MapInfoType.CARROT_STATE_GOLD, byte clearedAllClear = MapInfoType.ALL_CLEAR)
        {
            SingleMapInfo[] cells = new SingleMapInfo[MaxBigLevel * LevelsPerBig];
            int index = 0;
            for (int big = 1; big <= MaxBigLevel; big++)
            {
                for (int level = 1; level <= LevelsPerBig; level++)
                {
                    cells[index++] = new SingleMapInfo
                    {
                        bigLevelId = (byte)big,
                        levelId = (byte)level,
                        carrotState = clearedCarrotState,
                        isAllClear = clearedAllClear,
                        unLocked = MapInfoType.UNLOCK_LEVEL,
                    };
                }
            }

            return MapInfoHelper.BuildSnapshot(cells);
        }

        /// <summary>
        /// 解锁到指定关（含）：此前关卡按 cleared 状态写入；目标关仅解锁未通关。
        /// 目标关之后的关卡全部锁定。
        /// </summary>
        public static string BuildUnlockUpToSnapshot(
            int targetBig,
            int targetLevel,
            byte clearedCarrotState = MapInfoType.CARROT_STATE_GOLD,
            byte clearedAllClear = MapInfoType.ALL_CLEAR)
        {
            targetBig = ClampBig(targetBig);
            targetLevel = ClampLevel(targetLevel);

            SingleMapInfo[] cells = new SingleMapInfo[MaxBigLevel * LevelsPerBig];
            int index = 0;
            for (int big = 1; big <= MaxBigLevel; big++)
            {
                for (int level = 1; level <= LevelsPerBig; level++)
                {
                    cells[index++] = BuildCellState(
                        big,
                        level,
                        targetBig,
                        targetLevel,
                        clearedCarrotState,
                        clearedAllClear);
                }
            }

            return MapInfoHelper.BuildSnapshot(cells);
        }

        public static string BuildSingleCellSnapshot(
            string baseSnapshot,
            int bigLevelId,
            int levelId,
            byte carrotState,
            byte isAllClear,
            byte unlocked)
        {
            if (string.IsNullOrEmpty(baseSnapshot))
            {
                baseSnapshot = BuildResetSnapshot();
            }

            var cell = new SingleMapInfo
            {
                bigLevelId = (byte)ClampBig(bigLevelId),
                levelId = (byte)ClampLevel(levelId),
                carrotState = carrotState,
                isAllClear = isAllClear,
                unLocked = unlocked,
            };
            return MapInfoHelper.GetNewMapInfo(baseSnapshot, cell);
        }

        public static void ApplySnapshot(string snapshot, long userId)
        {
            if (string.IsNullOrEmpty(snapshot))
            {
                return;
            }

            long resolvedUserId = ResolveUserId(userId);
            StandaloneMapPersistence.Save(resolvedUserId, snapshot);

            if (!Application.isPlaying)
            {
                return;
            }

            if (MapServer.Instance != null)
            {
                MapServer.Instance.ApplyMapSnapshot(snapshot);
            }

            if (StandaloneGameConfig.EnableStandaloneMode)
            {
                StandaloneBackendMock.ApplyMapSnapshotForGm(snapshot, resolvedUserId);
            }
        }

        public static void DeletePersistedSnapshot(long userId)
        {
            StandaloneMapPersistence.Delete(ResolveUserId(userId));
        }

        public static string GetLiveSnapshotOrPersisted(long userId)
        {
            if (Application.isPlaying && MapServer.Instance?.mapModel != null)
            {
                return MapServer.Instance.mapModel.ExportSnapshot();
            }

            string persisted = LoadPersistedSnapshot(userId);
            return !string.IsNullOrEmpty(persisted) ? persisted : BuildResetSnapshot();
        }

        static SingleMapInfo BuildCellState(
            int big,
            int level,
            int targetBig,
            int targetLevel,
            byte clearedCarrotState,
            byte clearedAllClear)
        {
            var info = new SingleMapInfo
            {
                bigLevelId = (byte)big,
                levelId = (byte)level,
            };

            if (IsOnOrBefore(big, level, targetBig, targetLevel))
            {
                info.unLocked = MapInfoType.UNLOCK_LEVEL;
                if (IsStrictlyBefore(big, level, targetBig, targetLevel))
                {
                    info.carrotState = clearedCarrotState;
                    info.isAllClear = clearedAllClear;
                }
                else
                {
                    info.carrotState = 0;
                    info.isAllClear = MapInfoType.NOT_ALL_CLEAR;
                }
            }
            else
            {
                info.unLocked = MapInfoType.LOCK_LEVEL;
                info.carrotState = 0;
                info.isAllClear = MapInfoType.NOT_ALL_CLEAR;
            }

            return info;
        }

        static bool IsOnOrBefore(int big, int level, int targetBig, int targetLevel)
        {
            if (big < targetBig)
            {
                return true;
            }

            if (big > targetBig)
            {
                return false;
            }

            return level <= targetLevel;
        }

        static bool IsStrictlyBefore(int big, int level, int targetBig, int targetLevel)
        {
            if (big < targetBig)
            {
                return true;
            }

            if (big > targetBig)
            {
                return false;
            }

            return level < targetLevel;
        }

        static int ClampBig(int big)
        {
            if (big < 1)
            {
                return 1;
            }

            if (big > MaxBigLevel)
            {
                return MaxBigLevel;
            }

            return big;
        }

        static int ClampLevel(int level)
        {
            if (level < 1)
            {
                return 1;
            }

            if (level > LevelsPerBig)
            {
                return LevelsPerBig;
            }

            return level;
        }
    }
}
