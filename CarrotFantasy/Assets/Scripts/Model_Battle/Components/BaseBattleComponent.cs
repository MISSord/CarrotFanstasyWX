using System;

namespace CarrotFantasy
{
    public abstract class BaseBattleComponent
    {
        public BaseBattle baseBattle { get; private set; }
        public String componentType { get; protected set; } //子类赋值

        public EventDispatcher eventDispatcher { get; protected set; }

        public BaseBattleComponent(BaseBattle bBattle)
        {
            this.baseBattle = bBattle;
            this.eventDispatcher = bBattle.eventDispatcher;
        }

        public abstract void Init();

        public virtual void Start() { } //用于开始游戏(即使是重新开始)

        public virtual void OnTick(Fix64 time) { }

        public virtual void LateTick(Fix64 time) { }

        public virtual void ClearInfo() { } //用于重新开始游戏

        public virtual void Dispose() { }
    }

}
