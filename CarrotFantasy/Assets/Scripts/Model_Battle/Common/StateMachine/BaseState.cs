using System;

namespace CarrotFantasy
{
    public abstract class BaseState
    {
        public String statetype;
        public BaseState(String btype)
        {
            statetype = btype;
        }

        public virtual void Init() { }

        public virtual void StateIn() { } // 参数由各状态自己去获取

        public abstract String OnTick(Fix64 time);

        public virtual void StateOut() { }

        public virtual void Dispose()
        {
            this.statetype = null;
        }

        public String GetStateType() { return statetype; }
    }
}
