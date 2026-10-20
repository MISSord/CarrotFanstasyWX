using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class MapModel
    {
        public EventDispatcher eventDispatcher;
        readonly Dictionary<int, SingleMapInfo> allMapInfo = new Dictionary<int, SingleMapInfo>();
        readonly List<SingleMapInfo> allMapInfoList = new List<SingleMapInfo>();
        readonly Dictionary<int, BigLevelInfo> bigLevelInfo = new Dictionary<int, BigLevelInfo>();

        public MapModel(EventDispatcher eventDis)
        {
            this.eventDispatcher = eventDis;
            MapConfigReader.InitConfig();
        }

        public void ParseMapInfo(string mapInfo)
        {
            SingleMapInfo[] cells = MapInfoHelper.MergeSnapshot(mapInfo);
            this.allMapInfo.Clear();
            this.allMapInfoList.Clear();
            this.bigLevelInfo.Clear();

            for (int i = 0; i < cells.Length; i++)
            {
                SingleMapInfo info = cells[i];
                if (info == null)
                {
                    continue;
                }

                this.allMapInfo[MapConfigReader.MakeKey(info.bigLevelId, info.levelId)] = info;
                this.allMapInfoList.Add(info);
            }

            this.InitBigLevelMapInfo();
            this.eventDispatcher.DispatchEvent(MapEventType.MAP_INFO_CHANGE);
        }

        private void InitBigLevelMapInfo()
        {
            IReadOnlyList<int> bigIds = MapConfigReader.GetBigLevelIds();
            for (int i = 0; i < bigIds.Count; i++)
            {
                int big = bigIds[i];
                this.bigLevelInfo[big] = new BigLevelInfo
                {
                    bigLevel = big,
                    isLock = true,
                };
            }

            for (int i = 0; i < this.allMapInfoList.Count; i++)
            {
                SingleMapInfo cell = this.allMapInfoList[i];
                BigLevelInfo info;
                if (!this.bigLevelInfo.TryGetValue(cell.bigLevelId, out info))
                {
                    info = new BigLevelInfo
                    {
                        bigLevel = cell.bigLevelId,
                        isLock = true,
                    };
                    this.bigLevelInfo[cell.bigLevelId] = info;
                }

                info.count += 1;
                if (cell.unLocked == MapInfoType.UNLOCK_LEVEL)
                {
                    info.unlockCount += 1;
                    info.isLock = false;
                }
            }
        }

        private void RefreshBigLevelMapInfo(int bigLevel)
        {
            BigLevelInfo levelInfo;
            if (!this.bigLevelInfo.TryGetValue(bigLevel, out levelInfo))
            {
                levelInfo = new BigLevelInfo { bigLevel = bigLevel, isLock = true };
                this.bigLevelInfo[bigLevel] = levelInfo;
            }

            levelInfo.unlockCount = 0;
            levelInfo.count = 0;
            levelInfo.isLock = true;
            for (int i = 0; i < this.allMapInfoList.Count; i++)
            {
                SingleMapInfo cell = this.allMapInfoList[i];
                if (cell.bigLevelId != bigLevel)
                {
                    continue;
                }

                levelInfo.count += 1;
                if (cell.unLocked == MapInfoType.UNLOCK_LEVEL)
                {
                    levelInfo.isLock = false;
                    levelInfo.unlockCount += 1;
                }
            }
        }

        public void UpdateSingleMapInfo(SingleMapInfo unSave)
        {
            if (unSave == null)
            {
                Debug.Log("更新单个地图信息有错误");
                return;
            }

            SingleMapInfo info;
            if (!this.allMapInfo.TryGetValue(MapConfigReader.MakeKey(unSave.bigLevelId, unSave.levelId), out info))
            {
                Debug.Log("更新单个地图信息有错误");
                return;
            }

            info.carrotState = info.carrotState >= unSave.carrotState ? info.carrotState : unSave.carrotState;
            info.isAllClear = info.isAllClear <= unSave.isAllClear ? info.isAllClear : unSave.isAllClear;
            this.RefreshBigLevelMapInfo(unSave.bigLevelId);
            this.eventDispatcher.DispatchEvent(MapEventType.MAP_INFO_CHANGE);
        }

        public string ExportSnapshot()
        {
            if (this.allMapInfoList.Count == 0)
            {
                return MapInfoHelper.GetInitMapInfo();
            }

            return MapInfoHelper.BuildSnapshot(this.allMapInfoList.ToArray());
        }

        public void UpdateSingleMapInfoUnLockState(int bigLevel, int level, byte unLock)
        {
            if (bigLevel == 0 || level == 0)
            {
                Debug.Log("开完全部地图了");
                return;
            }

            SingleMapInfo info;
            if (!this.allMapInfo.TryGetValue(MapConfigReader.MakeKey(bigLevel, level), out info))
            {
                Debug.Log("开完全部地图了");
                return;
            }

            info.unLocked = unLock;
            this.RefreshBigLevelMapInfo(bigLevel);
            this.eventDispatcher.DispatchEvent(MapEventType.MAP_INFO_CHANGE);
        }

        public SingleMapInfo[] GetOnceBigLevelMapInfo(int bigLevel)
        {
            IReadOnlyList<int> levelIds = MapConfigReader.GetLevelIds(bigLevel);
            var mapInfoList = new SingleMapInfo[levelIds.Count];
            for (int i = 0; i < levelIds.Count; i++)
            {
                mapInfoList[i] = this.GetSingleMapInfo(bigLevel, levelIds[i]);
            }

            return mapInfoList;
        }

        public List<BigLevelInfo> GetAllBigLevelInfos()
        {
            IReadOnlyList<int> bigIds = MapConfigReader.GetBigLevelIds();
            var items = new List<BigLevelInfo>(bigIds.Count);
            for (int i = 0; i < bigIds.Count; i++)
            {
                BigLevelInfo info = this.GetBigLevelInfo(bigIds[i]);
                if (info != null && info.count > 0)
                {
                    items.Add(info);
                }
            }

            return items;
        }

        public BigLevelInfo GetBigLevelInfo(int bigLevel)
        {
            BigLevelInfo info;
            if (this.bigLevelInfo.TryGetValue(bigLevel, out info))
            {
                return info;
            }

            return null;
        }

        public int GetBigLevelCount()
        {
            return this.bigLevelInfo.Count;
        }

        public Stage GetStage(int bigLevel, int level)
        {
            return MapConfigReader.GetSingleStage(bigLevel, level);
        }

        public SingleMapInfo GetSingleMapInfo(int bigLevel, int level)
        {
            SingleMapInfo info;
            if (this.allMapInfo.TryGetValue(MapConfigReader.MakeKey(bigLevel, level), out info))
            {
                return info;
            }

            return null;
        }

        public void Dispose()
        {
            this.allMapInfo.Clear();
            this.allMapInfoList.Clear();
            this.bigLevelInfo.Clear();
            this.eventDispatcher.Dispose();
        }
    }
}
