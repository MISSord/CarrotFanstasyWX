using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    public abstract class BaseBattle
    {
        public EventDispatcher eventDispatcher { get; private set; }
        public int curFrameId { get; private set; }
        public Fix64 curClock = Fix64.Zero;
        private int uid;
        private int inputSeqCounter;
        public bool isPause { get; set; }
        public bool isDoulbSpeed { get; private set; }

        public Dictionary<String, BaseBattleComponent> componentDic = new Dictionary<string, BaseBattleComponent>();
        public List<BaseBattleComponent> componentList = new List<BaseBattleComponent>();

        public BaseStateMachine stateMachine;

        /// <summary>渲染层传入时间的累积余量，用于固定逻辑步长。</summary>
        private Fix64 logicAccumulator = Fix64.Zero;

        public bool isStart = false;
        public bool isIgnoreViewListener = false; //目前用于视图监听器广播过程，服务端为true

        /// <summary>由 BattleManager 或联机/测试运行时注入；未注入时部分宿主能力（提示、提交存档）不执行。</summary>
        public IBattleHostBridge HostBridge { get; private set; }

        public void SetHostBridge(IBattleHostBridge bridge)
        {
            this.HostBridge = bridge;
        }

        public BaseBattle()
        {
            this.eventDispatcher = new EventDispatcher();
            this.isPause = false;
            this.isDoulbSpeed = false;
            this.curFrameId = 0;
        }

        protected virtual void AddListener() { }

        protected virtual void RemoveListener() { }

        public abstract void Init();

        public abstract void InitComponent(); //子类实现

        public BaseBattleComponent GetComponent(String type)
        {
            if (type != null)
            {
                return componentDic[type];
            }
            return null;
        }

        public void AddComponent(BaseBattleComponent component)
        {
            if (component != null)
            {
                if (componentDic.ContainsKey(component.componentType))
                {
                    //报错
                }
                else
                {
                    componentDic.Add(component.componentType, component);
                    componentList.Add(component);
                }
            }
        }

        public void RemoveComponent(BaseBattleComponent component)
        {
            if (component != null)
            {
                componentDic.Remove(component.componentType);
                componentList.Remove(component);
            }
        }

        public virtual void Tick(Fix64 deltaTime)
        {
            if (this.isPause == true) return;

            this.logicAccumulator += deltaTime;
            Fix64 logicDt = BattleLogicTiming.LogicDeltaTime;
            int maxSteps = BattleLogicTiming.MaxLogicStepsPerRenderFrame <= 0 ? 8 : BattleLogicTiming.MaxLogicStepsPerRenderFrame;

            int steps = 0;
            while (steps < maxSteps && this.logicAccumulator >= logicDt)
            {
                this.logicAccumulator -= logicDt;
                SimulateOneLogicFrame(logicDt);
                steps++;
            }
        }

        /// <summary>推进一个固定逻辑帧（阶段 2：确定性仿真步）。</summary>
        protected virtual void SimulateOneLogicFrame(Fix64 logicDt)
        {
            curClock += logicDt;
            curFrameId += 1;
            for (int i = 0; i < componentList.Count; i++)
            {
                componentList[i].OnTick(logicDt);
            }
            stateMachine.OnTick(logicDt);
            for (int i = 0; i < componentList.Count; i++)
            {
                componentList[i].LateTick(logicDt);
            }
            eventDispatcher.DispatchEvent(BattleEvent.AFTER_TICK);
        }

        public virtual void StartGame()
        {
            if (isStart == true) return;
            isStart = true;
            for (int i = 0; i < componentList.Count; i++)
            {
                componentList[i].Start();
            }

            this.stateMachine.SetCurrentState(BattleStateType.START_GAME);
        }

        public virtual void ClearGameInfo()
        {
            this.stateMachine.ClearGameInfo();
            for (int i = componentList.Count - 1; i >= 0; i--)
            {
                this.componentList[i].ClearInfo();
            }
            this.componentDic.Clear();
            this.componentList.Clear();
            this.isStart = false;
            this.isDoulbSpeed = false;
            this.isPause = false;
            this.curFrameId = 0;
            this.logicAccumulator = Fix64.Zero;
            this.curClock = Fix64.Zero;
            this.uid = 0;
            this.inputSeqCounter = 0;
        }

        protected virtual void PauseTheGame()
        {
            this.isPause = true;
            this.eventDispatcher.DispatchEvent<bool>(BattleEvent.GAME_STATE_CHANGE, this.isPause);
        }

        protected virtual void GoOnTheGame()
        {
            this.isPause = false;
            this.eventDispatcher.DispatchEvent<bool>(BattleEvent.GAME_STATE_CHANGE, this.isPause);
        }

        public int GetUid()
        {
            this.uid += 1;
            return this.uid;
        }

        /// <summary>为输入指令分配单调递增序号（提交顺序即确定性顺序）。</summary>
        public int AllocateInputSequence()
        {
            this.inputSeqCounter += 1;
            return this.inputSeqCounter;
        }

        public virtual void Dispose()
        {
            this.stateMachine.Dispose();
            for (int i = componentList.Count - 1; i >= 0; i--) //倒着去除
            {
                this.componentList[i].Dispose();
            }
            this.componentList.Clear();
            this.componentDic.Clear();
            this.RemoveListener();
            BattleUnitPool.Instance.Dispose();
            this.eventDispatcher.Dispose();
        }
    }
}
