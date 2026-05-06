namespace CarrotFantasy
{
    /// <summary>战斗指令（阶段 3：帧对齐 + 序列号保证同帧确定性）。</summary>
    public class InputOrder
    {
        public int frameId { get; private set; }
        public int x { get; private set; }
        public int y { get; private set; }
        public int order { get; private set; }

        public int towerId { get; private set; }

        /// <summary>来源玩家；单机默认 0，联机可按玩家编号写入。</summary>
        public int playerId { get; private set; }

        /// <summary>
        /// 同一逻辑帧内多条指令的先后顺序；0 表示未绑定，由 <see cref="BattleInputComponent.AddOrder"/> 分配。
        /// </summary>
        public int seq { get; private set; }

        public bool HasSequence => this.seq != 0;

        public void SetOrder(int frame, int x, int y, int orderType, int playerId = 0)
        {
            this.frameId = frame;
            this.x = x;
            this.y = y;
            this.order = orderType;
            this.playerId = playerId;
            this.seq = 0;
        }

        public void SetTowerId(int towerId)
        {
            this.towerId = towerId;
        }

        /// <summary>回放或权威端预先写入序列（须大于 0）；已绑定则不变。</summary>
        public void SetReplaySequence(int replaySeq)
        {
            if (replaySeq > 0 && this.seq == 0)
                this.seq = replaySeq;
        }

        internal void EnsureSequence(int allocatedSeq)
        {
            if (this.seq == 0)
                this.seq = allocatedSeq;
        }
    }
}
