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

        public virtual void init() { }

        public virtual void stateIn() { } // 参数由各状态自己去获取

        public abstract String onTick(Fix64 time);

        public virtual void stateOut() { }

        public virtual void Dispose()
        {
            this.statetype = null;
        }

        public String getStateType() { return statetype; }
    }
}
