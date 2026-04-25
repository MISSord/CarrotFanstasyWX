using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public abstract class BaseStateMachine
    {
        private BaseBattleState currentState;
        private Dictionary<String, BaseBattleState> stateDic = new Dictionary<string, BaseBattleState>();
        public EventDispatcher eventDispatcher { get; private set; }

        public BaseStateMachine(BaseBattle battle)
        {
            this.eventDispatcher = battle.eventDispatcher;
        }

        protected abstract BaseBattleState CreateStateInstance(String type);//子类实现

        public BaseBattleState GetState(String type)
        {
            BaseBattleState bBattle;
            if (stateDic.ContainsKey(type))
            {
                stateDic.TryGetValue(type, out bBattle);
            }
            else
            {
                bBattle = this.CreateStateInstance(type);
                bBattle.Init();
                stateDic.Add(type, bBattle);
            }
            return bBattle;
        }

        protected virtual void LeaveState()
        {
            if (currentState != null)
            {
                currentState.StateOut();
                Debug.Log(String.Format("退出旧的状态{0}", currentState.statetype));
                currentState = null;
            }
        }

        protected virtual void EnterState(BaseBattleState lastState)
        {
            currentState = lastState;
            currentState.StateIn();
        }

        public virtual void SetCurrentState(String stateType)
        {
            BaseBattleState lastState = this.GetState(stateType);
            this.LeaveState();
            this.EnterState(lastState);
            Debug.Log(String.Format("进入新的状态{0}", lastState.statetype));
        }

        public BaseBattleState GetCurrentState()
        {
            return currentState;
        }

        public void OnTick(Fix64 time)
        {
            if (currentState == null) return;
            String nextType = currentState.OnTick(time);
            if (!String.Equals(nextType, currentState.GetStateType()))
            {
                this.SetCurrentState(nextType);
            }
        }

        public void ClearGameInfo()
        {
            if (this.currentState != null)
            {
                this.currentState.StateOut();
            }
            foreach (KeyValuePair<String, BaseBattleState> stateInfo in stateDic)
            {
                stateInfo.Value.Dispose();
            }
            this.stateDic.Clear();
        }

        public void Dispose()
        {
            this.ClearGameInfo();
            this.eventDispatcher = null;
        }
    }
}
