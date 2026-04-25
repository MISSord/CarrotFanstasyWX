using System;

namespace CarrotFantasy
{
    public abstract class BaseUnitComponent
    {
        public BattleUnit unit { get; private set; }
        public String unitComponentType { get; protected set; } //在子类里赋值

        public void LoadUnit(BattleUnit unit)
        {
            this.unit = unit;
        }

        public virtual void Init() { }

        public virtual void Start() { }

        public abstract void OnTick(Fix64 deltaTime);

        public virtual void LateTick(Fix64 deltaTime) { }

        public virtual void Dispose()
        {
            this.unit = null;
        }
    }
}
