using System.Collections.Generic;

namespace CarrotFantasy
{
    public class BattleInputComponent : BaseBattleComponent
    {
        public List<InputOrder> curNoProcessDic = new List<InputOrder>();

        private readonly List<int> shouldRemoveList = new List<int>();
        private readonly List<InputOrder> frameBatch = new List<InputOrder>();

        private BattleTowerComponent towerComponent;
        private BattleMapComponent mapComponent;
        private BattleFlowFieldComponent flowFieldComponent;

        public BattleInputComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = BattleComponentType.InputComponent;
        }

        public override void Init()
        {
            this.towerComponent = (BattleTowerComponent)this.baseBattle.GetComponent(BattleComponentType.TowerComponent);
            this.mapComponent = (BattleMapComponent)this.baseBattle.GetComponent(BattleComponentType.MapComponent);
            BaseBattleComponent flowComp;
            if (this.baseBattle.componentDic.TryGetValue(BattleComponentType.FlowFieldComponent, out flowComp))
            {
                this.flowFieldComponent = flowComp as BattleFlowFieldComponent;
            }
            else
            {
                this.flowFieldComponent = null;
            }
        }

        public override void OnTick(Fix64 time)
        {
            if (this.curNoProcessDic.Count == 0) return;

            int curF = this.baseBattle.curFrameId;
            this.shouldRemoveList.Clear();
            this.frameBatch.Clear();

            for (int i = 0; i < this.curNoProcessDic.Count; i++)
            {
                InputOrder o = this.curNoProcessDic[i];
                if (o.frameId == curF)
                {
                    this.frameBatch.Add(o);
                    this.shouldRemoveList.Add(i);
                }
                else if (o.frameId < curF)
                {
                    this.shouldRemoveList.Add(i);
                }
            }

            if (this.frameBatch.Count > 1)
            {
                this.frameBatch.Sort(CompareSameFrameOrder);
            }

            bool flowDirty = false;
            for (int i = 0; i < this.frameBatch.Count; i++)
            {
                InputOrder o = this.frameBatch[i];
                this.towerComponent.ExePlayerOrder(o);
                this.mapComponent.ExePlayerOrder(o);
                if (o.order == InputOrderType.ADD_ORDER || o.order == InputOrderType.REMOVE_ORDER)
                {
                    flowDirty = true;
                }
            }

            if (flowDirty && this.flowFieldComponent != null)
            {
                this.flowFieldComponent.Rebuild();
            }

            RemoveByIndicesDescending(this.curNoProcessDic, this.shouldRemoveList);
        }

        /// <summary>先按玩家，再按序列号，保证同帧多指令顺序确定。</summary>
        private static int CompareSameFrameOrder(InputOrder a, InputOrder b)
        {
            int c = a.playerId.CompareTo(b.playerId);
            if (c != 0) return c;
            return a.seq.CompareTo(b.seq);
        }

        private static void RemoveByIndicesDescending(List<InputOrder> list, List<int> indices)
        {
            if (indices.Count == 0) return;
            if (indices.Count > 1)
            {
                indices.Sort((x, y) => y.CompareTo(x));
            }

            for (int i = 0; i < indices.Count; i++)
            {
                list.RemoveAt(indices[i]);
            }
        }

        public void AddOrder(InputOrder order)
        {
            order.EnsureSequence(this.baseBattle.AllocateInputSequence());
            this.curNoProcessDic.Add(order);
        }

        public override void ClearInfo()
        {
            this.curNoProcessDic.Clear();
            this.shouldRemoveList.Clear();
            this.frameBatch.Clear();
        }

        public override void Dispose()
        {
            this.ClearInfo();
            base.Dispose();
        }
    }
}
