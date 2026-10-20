using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>GM 用地图进度读写（Editor 窗口与运行时调试共用）。</summary>
    public static class GmMapProgressService
    {
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
            SingleMapInfo[] cells = MapConfigReader.CreateDefaultCells();
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].carrotState = clearedCarrotState;
                cells[i].isAllClear = clearedAllClear;
                cells[i].unLocked = MapInfoType.UNLOCK_LEVEL;
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
            ClampToExisting(ref targetBig, ref targetLevel);

            SingleMapInfo[] cells = MapConfigReader.CreateDefaultCells();
            for (int i = 0; i < cells.Length; i++)
            {
                SingleMapInfo cell = cells[i];
                ApplyUnlockUpToState(cell, targetBig, targetLevel, clearedCarrotState, clearedAllClear);
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

            ClampToExisting(ref bigLevelId, ref levelId);
            var cell = new SingleMapInfo
            {
                bigLevelId = (byte)bigLevelId,
                levelId = (byte)levelId,
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

        static void ApplyUnlockUpToState(
            SingleMapInfo info,
            int targetBig,
            int targetLevel,
            byte clearedCarrotState,
            byte clearedAllClear)
        {
            if (IsOnOrBefore(info.bigLevelId, info.levelId, targetBig, targetLevel))
            {
                info.unLocked = MapInfoType.UNLOCK_LEVEL;
                if (IsStrictlyBefore(info.bigLevelId, info.levelId, targetBig, targetLevel))
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

        static void ClampToExisting(ref int big, ref int level)
        {
            IReadOnlyList<int> bigIds = MapConfigReader.GetBigLevelIds();
            if (bigIds.Count == 0)
            {
                big = 1;
                level = 1;
                return;
            }

            if (IndexOf(bigIds, big) < 0)
            {
                if (big < bigIds[0])
                {
                    big = bigIds[0];
                }
                else
                {
                    big = bigIds[bigIds.Count - 1];
                }
            }

            IReadOnlyList<int> levelIds = MapConfigReader.GetLevelIds(big);
            if (levelIds.Count == 0)
            {
                level = 1;
                return;
            }

            if (IndexOf(levelIds, level) < 0)
            {
                if (level < levelIds[0])
                {
                    level = levelIds[0];
                }
                else
                {
                    level = levelIds[levelIds.Count - 1];
                }
            }
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
    }
}
