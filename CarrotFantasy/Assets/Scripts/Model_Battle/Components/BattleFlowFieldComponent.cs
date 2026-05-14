using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 从萝卜格（路径终点）反向最短路流场：塔为动态障碍；塔格八邻内的可走格仅允许四向出边；
    /// 其余可走格允许八向（含切角判定）。建塔/拆塔后由 <see cref="Rebuild"/> 刷新。
    /// </summary>
    public class BattleFlowFieldComponent : BaseBattleComponent
    {
        public const int CostStraight = 1000;
        public const int CostDiagonal = 1414;

        /// <summary>
        /// 为 true 且 <see cref="IsBuilt"/> 时，刷怪使用 <see cref="BattleUnit_MonsterFlow"/> + <see cref="UnitMoveComponent_MonsterFlowField"/>；
        /// 否则使用经典 <see cref="BattleUnit_Monster"/> + <see cref="UnitMoveComponent_Monster"/>。
        /// </summary>
        public bool useMonsterFlowMovement = true;

        private BattleMapComponent map;
        private int xc;
        private int yr;
        private int cellCount;

        private int goalGx;
        private int goalGy;
        private int goalIdx;

        private bool[] staticWalkable;
        private bool[] towerBlocked;
        private bool[] inTowerEightRing;
        private int[] distToGoal;
        private int[] nextGx;
        private int[] nextGy;

        private List<(int fromIdx, int cost)>[] preds;
        private readonly List<(int tx, int ty, int cost)> tmpForwardMoves = new List<(int, int, int)>(8);

        public bool IsBuilt { get; private set; }

        public BattleFlowFieldComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = BattleComponentType.FlowFieldComponent;
        }

        public override void Init()
        {
            this.map = (BattleMapComponent)this.baseBattle.GetComponent(BattleComponentType.MapComponent);
            this.xc = this.map.xColumn;
            this.yr = this.map.yRow;
            this.cellCount = this.xc * this.yr;

            List<BattleMapGrid.GridIndex> path = this.map.levelInfo.monsterPath;
            if (path == null || path.Count == 0)
            {
                this.staticWalkable = new bool[this.cellCount];
                this.IsBuilt = false;
                return;
            }

            BattleMapGrid.GridIndex goal = path[path.Count - 1];
            this.goalGx = goal.xIndex;
            this.goalGy = goal.yIndex;
            this.goalIdx = this.ToIdx(this.goalGx, this.goalGy);

            this.staticWalkable = new bool[this.cellCount];
            for (int i = 0; i < path.Count; i++)
            {
                int ix = path[i].xIndex;
                int iy = path[i].yIndex;
                if (this.InBounds(ix, iy))
                {
                    this.staticWalkable[this.ToIdx(ix, iy)] = true;
                }
            }

            this.towerBlocked = new bool[this.cellCount];
            this.inTowerEightRing = new bool[this.cellCount];
            this.distToGoal = new int[this.cellCount];
            this.nextGx = new int[this.cellCount];
            this.nextGy = new int[this.cellCount];

            this.preds = new List<(int, int)>[this.cellCount];
            for (int i = 0; i < this.cellCount; i++)
            {
                this.preds[i] = new List<(int, int)>(8);
            }

            this.Rebuild();
        }

        public int GoalGridX
        {
            get { return this.goalGx; }
        }

        public int GoalGridY
        {
            get { return this.goalGy; }
        }

        /// <summary>离散最短路代价（直 1000 / 斜 1414），不可达为大数。</summary>
        public int GetDistanceCost(int gx, int gy)
        {
            if (!this.InBounds(gx, gy))
            {
                return int.MaxValue / 4;
            }

            return this.distToGoal[this.ToIdx(gx, gy)];
        }

        /// <summary>沿最短路下一格；已在目标或不可达时返回 (-1,-1)。</summary>
        public void GetNextGrid(int gx, int gy, out int nx, out int ny)
        {
            if (!this.InBounds(gx, gy) || !this.IsBuilt)
            {
                nx = -1;
                ny = -1;
                return;
            }

            int i = this.ToIdx(gx, gy);
            nx = this.nextGx[i];
            ny = this.nextGy[i];
        }

        public void Rebuild()
        {
            if (this.staticWalkable == null || this.cellCount == 0)
            {
                this.IsBuilt = false;
                return;
            }

            this.RefreshTowerMasks();
            this.BuildPredecessors();
            this.RunShortestPathFromGoal();
            this.BuildNextPointers();
        }

        private void RefreshTowerMasks()
        {
            for (int i = 0; i < this.cellCount; i++)
            {
                this.towerBlocked[i] = false;
                this.inTowerEightRing[i] = false;
            }

            for (int x = 0; x < this.xc; x++)
            {
                for (int y = 0; y < this.yr; y++)
                {
                    if (!this.map.gridsList[x, y].hasTower)
                    {
                        continue;
                    }

                    int ti = this.ToIdx(x, y);
                    this.towerBlocked[ti] = true;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0)
                            {
                                continue;
                            }

                            int nx = x + dx;
                            int ny = y + dy;
                            if (this.InBounds(nx, ny))
                            {
                                this.inTowerEightRing[this.ToIdx(nx, ny)] = true;
                            }
                        }
                    }
                }
            }
        }

        private void BuildPredecessors()
        {
            for (int i = 0; i < this.cellCount; i++)
            {
                this.preds[i].Clear();
            }

            for (int x = 0; x < this.xc; x++)
            {
                for (int y = 0; y < this.yr; y++)
                {
                    int fromIdx = this.ToIdx(x, y);
                    this.CollectForwardMoves(x, y);
                    for (int k = 0; k < this.tmpForwardMoves.Count; k++)
                    {
                        int tx = this.tmpForwardMoves[k].tx;
                        int ty = this.tmpForwardMoves[k].ty;
                        int cost = this.tmpForwardMoves[k].cost;
                        int toIdx = this.ToIdx(tx, ty);
                        this.preds[toIdx].Add((fromIdx, cost));
                    }
                }
            }
        }

        private void CollectForwardMoves(int x, int y)
        {
            this.tmpForwardMoves.Clear();
            if (!this.IsWalkableMoveCell(x, y))
            {
                return;
            }

            bool cardinalOnly = this.inTowerEightRing[this.ToIdx(x, y)];

            this.TryAddCardinal(x, y, x + 1, y, CostStraight);
            this.TryAddCardinal(x, y, x - 1, y, CostStraight);
            this.TryAddCardinal(x, y, x, y + 1, CostStraight);
            this.TryAddCardinal(x, y, x, y - 1, CostStraight);

            if (cardinalOnly)
            {
                return;
            }

            this.TryAddDiagonal(x, y, x + 1, y + 1, CostDiagonal);
            this.TryAddDiagonal(x, y, x + 1, y - 1, CostDiagonal);
            this.TryAddDiagonal(x, y, x - 1, y + 1, CostDiagonal);
            this.TryAddDiagonal(x, y, x - 1, y - 1, CostDiagonal);
        }

        private void TryAddCardinal(int x, int y, int tx, int ty, int cost)
        {
            if (!this.InBounds(tx, ty))
            {
                return;
            }

            if (!this.IsWalkableMoveCell(tx, ty))
            {
                return;
            }

            this.tmpForwardMoves.Add((tx, ty, cost));
        }

        private void TryAddDiagonal(int x, int y, int tx, int ty, int cost)
        {
            if (!this.InBounds(tx, ty))
            {
                return;
            }

            int mx = tx - x;
            int my = ty - y;
            int cornerAx = x + mx;
            int cornerAy = y;
            int cornerBx = x;
            int cornerBy = y + my;
            if (!this.IsWalkableMoveCell(cornerAx, cornerAy) || !this.IsWalkableMoveCell(cornerBx, cornerBy))
            {
                return;
            }

            if (!this.IsWalkableMoveCell(tx, ty))
            {
                return;
            }

            this.tmpForwardMoves.Add((tx, ty, cost));
        }

        private bool IsWalkableMoveCell(int x, int y)
        {
            if (!this.InBounds(x, y))
            {
                return false;
            }

            int i = this.ToIdx(x, y);
            if (!this.staticWalkable[i])
            {
                return false;
            }

            if (this.towerBlocked[i])
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// dist[v] = 从 v 沿合法一步边走到萝卜的最小代价；对 v 松弛边 v→to 即 preds[to] 中的 (v,c)：dist[v] = min(dist[v], dist[to]+c)。
        /// </summary>
        private void RunShortestPathFromGoal()
        {
            int inf = int.MaxValue / 4;
            for (int i = 0; i < this.cellCount; i++)
            {
                this.distToGoal[i] = inf;
            }

            if (!this.IsWalkableMoveCell(this.goalGx, this.goalGy))
            {
                this.IsBuilt = false;
                return;
            }

            this.distToGoal[this.goalIdx] = 0;

            for (int it = 0; it < this.cellCount; it++)
            {
                bool changed = false;
                for (int to = 0; to < this.cellCount; to++)
                {
                    if (this.distToGoal[to] >= inf / 2)
                    {
                        continue;
                    }

                    List<(int fromIdx, int cost)> list = this.preds[to];
                    for (int k = 0; k < list.Count; k++)
                    {
                        int v = list[k].fromIdx;
                        int w = list[k].cost;
                        int nd = this.distToGoal[to] + w;
                        if (nd < this.distToGoal[v])
                        {
                            this.distToGoal[v] = nd;
                            changed = true;
                        }
                    }
                }

                if (!changed)
                {
                    break;
                }
            }

            this.IsBuilt = true;
        }

        private void BuildNextPointers()
        {
            int inf = int.MaxValue / 4;
            for (int i = 0; i < this.cellCount; i++)
            {
                this.nextGx[i] = -1;
                this.nextGy[i] = -1;
            }

            for (int x = 0; x < this.xc; x++)
            {
                for (int y = 0; y < this.yr; y++)
                {
                    int idx = this.ToIdx(x, y);
                    if (!this.IsWalkableMoveCell(x, y))
                    {
                        continue;
                    }

                    if (idx == this.goalIdx)
                    {
                        continue;
                    }

                    this.CollectForwardMoves(x, y);
                    int bestD = inf;
                    int bx = -1;
                    int by = -1;
                    for (int k = 0; k < this.tmpForwardMoves.Count; k++)
                    {
                        int tx = this.tmpForwardMoves[k].tx;
                        int ty = this.tmpForwardMoves[k].ty;
                        int d = this.distToGoal[this.ToIdx(tx, ty)];
                        if (d < bestD)
                        {
                            bestD = d;
                            bx = tx;
                            by = ty;
                        }
                        else if (d == bestD && d < inf && bx >= 0)
                        {
                            if (ty < by || (ty == by && tx < bx))
                            {
                                bx = tx;
                                by = ty;
                            }
                        }
                    }

                    if (bx >= 0 && bestD < inf)
                    {
                        this.nextGx[idx] = bx;
                        this.nextGy[idx] = by;
                    }
                }
            }
        }

        private int ToIdx(int x, int y)
        {
            return x + y * this.xc;
        }

        private bool InBounds(int x, int y)
        {
            return x >= 0 && x < this.xc && y >= 0 && y < this.yr;
        }

        public override void ClearInfo()
        {
            this.IsBuilt = false;
        }

        public override void Dispose()
        {
            this.map = null;
            this.staticWalkable = null;
            this.towerBlocked = null;
            this.inTowerEightRing = null;
            this.distToGoal = null;
            this.nextGx = null;
            this.nextGy = null;
            this.preds = null;
            base.Dispose();
        }
    }
}
