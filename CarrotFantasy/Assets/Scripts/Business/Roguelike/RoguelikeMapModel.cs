using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>肉鸽章节/小关进度模型（对齐 <see cref="MapModel"/> 精简版）。</summary>
    public class RoguelikeMapModel
    {
        public EventDispatcher eventDispatcher { get; private set; }

        RoguelikeSingleLevelInfo[] allLevelInfo;
        readonly Dictionary<int, RoguelikeBigLevelInfo> bigLevelInfo = new Dictionary<int, RoguelikeBigLevelInfo>();

        public RoguelikeMapModel(EventDispatcher eventDis)
        {
            this.eventDispatcher = eventDis;
            RoguelikeLevelConfigReader.Instance.Init();
        }

        public void ApplyDefaultProgress()
        {
            int maxBig = RoguelikeLevelConfigReader.MaxBigLevel;
            int perBig = RoguelikeLevelConfigReader.LevelsPerBig;
            this.allLevelInfo = new RoguelikeSingleLevelInfo[maxBig * perBig];

            for (int big = 1; big <= maxBig; big++)
            {
                for (int level = 1; level <= perBig; level++)
                {
                    int index = this.GetIndex(big, level);
                    this.allLevelInfo[index] = new RoguelikeSingleLevelInfo
                    {
                        bigLevelId = (byte)big,
                        levelId = (byte)level,
                        cleared = RoguelikeMapInfoType.NOT_CLEARED,
                        unlocked = (big == 1 && level == 1)
                            ? RoguelikeMapInfoType.UNLOCK_LEVEL
                            : RoguelikeMapInfoType.LOCK_LEVEL,
                    };
                }
            }

            this.RebuildBigLevelInfo();
            this.eventDispatcher.DispatchEvent(RoguelikeMapEventType.MAP_INFO_CHANGE);
        }

        /// <summary>
        /// 快照格式：每格 <c>#大关,小关,cleared,unlocked</c>，与经典 Map 串风格一致。
        /// </summary>
        public void ParseSnapshot(string snapshot)
        {
            if (string.IsNullOrEmpty(snapshot))
            {
                this.ApplyDefaultProgress();
                return;
            }

            string[] parts = snapshot.Split('#');
            int maxBig = RoguelikeLevelConfigReader.MaxBigLevel;
            int perBig = RoguelikeLevelConfigReader.LevelsPerBig;
            this.allLevelInfo = new RoguelikeSingleLevelInfo[maxBig * perBig];

            for (int i = 0; i < this.allLevelInfo.Length; i++)
            {
                int big = i / perBig + 1;
                int level = i % perBig + 1;
                this.allLevelInfo[i] = new RoguelikeSingleLevelInfo
                {
                    bigLevelId = (byte)big,
                    levelId = (byte)level,
                    cleared = RoguelikeMapInfoType.NOT_CLEARED,
                    unlocked = RoguelikeMapInfoType.LOCK_LEVEL,
                };
            }

            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }
                string[] fields = parts[i].Split(',');
                if (fields.Length < 4)
                {
                    continue;
                }

                byte big = byte.Parse(fields[0]);
                byte level = byte.Parse(fields[1]);
                int index = this.GetIndex(big, level);
                if (index < 0 || index >= this.allLevelInfo.Length)
                {
                    continue;
                }

                this.allLevelInfo[index].cleared = byte.Parse(fields[2]);
                this.allLevelInfo[index].unlocked = byte.Parse(fields[3]);
            }

            this.RebuildBigLevelInfo();
            this.eventDispatcher.DispatchEvent(RoguelikeMapEventType.MAP_INFO_CHANGE);
        }

        public string ExportSnapshot()
        {
            if (this.allLevelInfo == null || this.allLevelInfo.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < this.allLevelInfo.Length; i++)
            {
                RoguelikeSingleLevelInfo info = this.allLevelInfo[i];
                sb.Append('#')
                    .Append(info.bigLevelId).Append(',')
                    .Append(info.levelId).Append(',')
                    .Append(info.cleared).Append(',')
                    .Append(info.unlocked);
            }
            return sb.ToString();
        }

        void RebuildBigLevelInfo()
        {
            this.bigLevelInfo.Clear();
            for (int i = 0; i < this.allLevelInfo.Length; i++)
            {
                RoguelikeSingleLevelInfo cell = this.allLevelInfo[i];
                RoguelikeBigLevelInfo big;
                if (!this.bigLevelInfo.TryGetValue(cell.bigLevelId, out big))
                {
                    big = new RoguelikeBigLevelInfo
                    {
                        bigLevel = cell.bigLevelId,
                        isLock = true,
                    };
                    this.bigLevelInfo.Add(cell.bigLevelId, big);
                }

                big.count += 1;
                if (cell.unlocked == RoguelikeMapInfoType.UNLOCK_LEVEL)
                {
                    big.unlockCount += 1;
                    big.isLock = false;
                }
            }
        }

        int GetIndex(int bigLevel, int level)
        {
            return (bigLevel - 1) * RoguelikeLevelConfigReader.LevelsPerBig + (level - 1);
        }

        public RoguelikeSingleLevelInfo GetSingleLevelInfo(int bigLevel, int level)
        {
            int index = this.GetIndex(bigLevel, level);
            if (this.allLevelInfo == null || index < 0 || index >= this.allLevelInfo.Length)
            {
                return null;
            }
            return this.allLevelInfo[index];
        }

        public RoguelikeSingleLevelInfo[] GetLevelsForBig(int bigLevel)
        {
            int perBig = RoguelikeLevelConfigReader.LevelsPerBig;
            RoguelikeSingleLevelInfo[] list = new RoguelikeSingleLevelInfo[perBig];
            for (int level = 1; level <= perBig; level++)
            {
                list[level - 1] = this.GetSingleLevelInfo(bigLevel, level);
            }
            return list;
        }

        public RoguelikeBigLevelInfo GetBigLevelInfo(int bigLevel)
        {
            RoguelikeBigLevelInfo info;
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

        public bool IsLevelUnlocked(int bigLevel, int level)
        {
            RoguelikeSingleLevelInfo info = this.GetSingleLevelInfo(bigLevel, level);
            return info != null && info.unlocked == RoguelikeMapInfoType.UNLOCK_LEVEL;
        }

        public void MarkClearedAndUnlockNext(int bigLevel, int level)
        {
            RoguelikeSingleLevelInfo info = this.GetSingleLevelInfo(bigLevel, level);
            if (info == null)
            {
                Debug.LogWarning("[RoguelikeMapModel] MarkCleared invalid level " + bigLevel + "-" + level);
                return;
            }

            info.cleared = RoguelikeMapInfoType.CLEARED;

            int nextBig = bigLevel;
            int nextLevel = level + 1;
            if (nextLevel > RoguelikeLevelConfigReader.LevelsPerBig)
            {
                nextBig = bigLevel + 1;
                nextLevel = 1;
            }

            if (nextBig <= RoguelikeLevelConfigReader.MaxBigLevel)
            {
                RoguelikeSingleLevelInfo next = this.GetSingleLevelInfo(nextBig, nextLevel);
                if (next != null)
                {
                    next.unlocked = RoguelikeMapInfoType.UNLOCK_LEVEL;
                }
            }

            this.RebuildBigLevelInfo();
            this.eventDispatcher.DispatchEvent(RoguelikeMapEventType.MAP_INFO_CHANGE);
        }

        /// <summary>GM / 调试：强制解锁指定小关。</summary>
        public void ForceUnlockLevel(int bigLevel, int level)
        {
            RoguelikeSingleLevelInfo info = this.GetSingleLevelInfo(bigLevel, level);
            if (info == null)
            {
                return;
            }

            info.unlocked = RoguelikeMapInfoType.UNLOCK_LEVEL;
            this.RebuildBigLevelInfo();
            this.eventDispatcher.DispatchEvent(RoguelikeMapEventType.MAP_INFO_CHANGE);
        }

        public void Dispose()
        {
            this.allLevelInfo = null;
            this.bigLevelInfo.Clear();
        }
    }
}
