using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 均匀空间网格：每逻辑帧在 HitTest 内 Clear 后 InsertAll，供子弹碰撞 broad phase 使用。
    /// 窄相位仍读 Transform 上实时更新的 bodyHitTestShape。
    /// </summary>
    public class BattleSpatialGrid
    {
        private readonly Fix64 cellSize;
        private readonly Fix64 originX;
        private readonly Fix64 originY;
        private readonly int gridWidth;
        private readonly int gridHeight;

        private readonly Dictionary<int, List<UnitTransformComponent>>[] layerBuckets;
        private readonly string[] layerKeys;

        public BattleSpatialGrid(BattleMapComponent map, Fix64 cellSizeWorld)
        {
            this.cellSize = cellSizeWorld > Fix64.Zero ? cellSizeWorld : new Fix64(BattleConfig.MAP_RATIO);
            this.originX = map.mapLeftBottomPosition.X;
            this.originY = map.mapLeftBottomPosition.Y;

            Fix64 spanX = map.mapRightTopPosition.X - this.originX;
            Fix64 spanY = map.mapRightTopPosition.Y - this.originY;
            if (spanX < this.cellSize)
            {
                spanX = this.cellSize;
            }

            if (spanY < this.cellSize)
            {
                spanY = this.cellSize;
            }

            this.gridWidth = (int)(float)Fix64.Ceiling(spanX / this.cellSize) + 2;
            this.gridHeight = (int)(float)Fix64.Ceiling(spanY / this.cellSize) + 2;
            if (this.gridWidth < 1)
            {
                this.gridWidth = 1;
            }

            if (this.gridHeight < 1)
            {
                this.gridHeight = 1;
            }

            this.layerKeys = new[]
            {
                BattleUnitType.BULLET,
                BattleUnitType.MONSTER,
                BattleUnitType.TOWER,
                BattleUnitType.ITEM,
            };
            this.layerBuckets = new Dictionary<int, List<UnitTransformComponent>>[this.layerKeys.Length];
            for (int i = 0; i < this.layerBuckets.Length; i++)
            {
                this.layerBuckets[i] = new Dictionary<int, List<UnitTransformComponent>>();
            }
        }

        public void Clear()
        {
            for (int i = 0; i < this.layerBuckets.Length; i++)
            {
                Dictionary<int, List<UnitTransformComponent>> dict = this.layerBuckets[i];
                foreach (KeyValuePair<int, List<UnitTransformComponent>> kv in dict)
                {
                    kv.Value.Clear();
                }

                dict.Clear();
            }
        }

        public void InsertAll(Dictionary<string, List<UnitTransformComponent>> transformsByType)
        {
            if (transformsByType == null)
            {
                return;
            }

            for (int li = 0; li < this.layerKeys.Length; li++)
            {
                string key = this.layerKeys[li];
                List<UnitTransformComponent> list;
                if (!transformsByType.TryGetValue(key, out list) || list == null)
                {
                    continue;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    this.Insert(li, list[i]);
                }
            }
        }

        private void Insert(int layerIndex, UnitTransformComponent transform)
        {
            if (transform == null || transform.bodyHitTestShape == null)
            {
                return;
            }

            HitTestShape_Circle circle = transform.bodyHitTestShape;
            int minCx;
            int maxCx;
            int minCy;
            int maxCy;
            this.GetCellRange(circle, out minCx, out maxCx, out minCy, out maxCy);

            Dictionary<int, List<UnitTransformComponent>> dict = this.layerBuckets[layerIndex];
            for (int cy = minCy; cy <= maxCy; cy++)
            {
                for (int cx = minCx; cx <= maxCx; cx++)
                {
                    int cellKey = this.CellKey(cx, cy);
                    List<UnitTransformComponent> bucket;
                    if (!dict.TryGetValue(cellKey, out bucket))
                    {
                        bucket = new List<UnitTransformComponent>(4);
                        dict.Add(cellKey, bucket);
                    }

                    bucket.Add(transform);
                }
            }
        }

        /// <summary>
        /// 收集与 <paramref name="source"/> 所在格邻域内、指定层上的候选（按 uid 去重）。
        /// </summary>
        public void QueryNearLayer(string targetLayer, UnitTransformComponent source, List<UnitTransformComponent> results, HashSet<int> seenUids)
        {
            results.Clear();
            if (source == null || source.bodyHitTestShape == null)
            {
                return;
            }

            int layerIndex = this.LayerIndex(targetLayer);
            if (layerIndex < 0)
            {
                return;
            }

            int minCx;
            int maxCx;
            int minCy;
            int maxCy;
            this.GetCellRange(source.bodyHitTestShape, out minCx, out maxCx, out minCy, out maxCy);
            this.ExpandCellRange(ref minCx, ref maxCx, ref minCy, ref maxCy);

            Dictionary<int, List<UnitTransformComponent>> dict = this.layerBuckets[layerIndex];
            for (int cy = minCy; cy <= maxCy; cy++)
            {
                for (int cx = minCx; cx <= maxCx; cx++)
                {
                    List<UnitTransformComponent> bucket;
                    if (!dict.TryGetValue(this.CellKey(cx, cy), out bucket))
                    {
                        continue;
                    }

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        UnitTransformComponent candidate = bucket[i];
                        if (candidate == null || candidate.unit == null)
                        {
                            continue;
                        }

                        int uid = candidate.unit.uid;
                        if (seenUids.Add(uid))
                        {
                            results.Add(candidate);
                        }
                    }
                }
            }
        }

        /// <summary>圆–圆窄相位（与塔射程、HitTest 共用 <see cref="BattleRangeQuery"/>）。</summary>
        public static bool TryNarrowPhaseCircleCircle(HitTestShape_Circle a, HitTestShape_Circle b)
        {
            return BattleRangeQuery.CirclesOverlap(a, b);
        }

        private int LayerIndex(string layer)
        {
            for (int i = 0; i < this.layerKeys.Length; i++)
            {
                if (this.layerKeys[i].Equals(layer))
                {
                    return i;
                }
            }

            return -1;
        }

        private void GetCellRange(HitTestShape_Circle circle, out int minCx, out int maxCx, out int minCy, out int maxCy)
        {
            minCx = this.ToCellX(circle.centerX - circle.radius);
            maxCx = this.ToCellX(circle.centerX + circle.radius);
            minCy = this.ToCellY(circle.centerY - circle.radius);
            maxCy = this.ToCellY(circle.centerY + circle.radius);
        }

        private void ExpandCellRange(ref int minCx, ref int maxCx, ref int minCy, ref int maxCy)
        {
            minCx = minCx > 0 ? minCx - 1 : 0;
            minCy = minCy > 0 ? minCy - 1 : 0;
            maxCx = maxCx + 1 < this.gridWidth ? maxCx + 1 : this.gridWidth - 1;
            maxCy = maxCy + 1 < this.gridHeight ? maxCy + 1 : this.gridHeight - 1;
        }

        private int ToCellX(Fix64 worldX)
        {
            int cx = (int)(float)Fix64.Floor((worldX - this.originX) / this.cellSize);
            return this.Clamp(cx, 0, this.gridWidth - 1);
        }

        private int ToCellY(Fix64 worldY)
        {
            int cy = (int)(float)Fix64.Floor((worldY - this.originY) / this.cellSize);
            return this.Clamp(cy, 0, this.gridHeight - 1);
        }

        private int CellKey(int cx, int cy)
        {
            return cx + cy * this.gridWidth;
        }

        private int Clamp(int v, int min, int max)
        {
            if (v < min)
            {
                return min;
            }

            if (v > max)
            {
                return max;
            }

            return v;
        }
    }
}
