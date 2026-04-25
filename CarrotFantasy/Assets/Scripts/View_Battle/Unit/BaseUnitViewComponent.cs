using System;

namespace CarrotFantasy
{

    public class BaseUnitViewComponent
    {
        private BattleUnitView unitView;
        private BattleUnit unit;
        private BattleView_base battleView;
        private BattleEventDispatcher unitEventDispatcher;
        public String componetType { get; protected set; }
        private float x, y, z;

        public BaseUnitViewComponent(BattleUnitView unitView)
        {
            this.Rector(unitView);
        }

        public void Rector(BattleUnitView unitView)
        {
            this.unitView = unitView;
            this.unit = unitView.unit;
            this.battleView = unitView.battleView;
            this.unitEventDispatcher = this.unit.eventDipatcher;
            this.x = 0;
            this.y = 0;
            this.z = 0;
        }

        public void Reset() { }

        public void Init() { }

        public void Start() { }

        public virtual void SetUnitPosition(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public virtual void SetUnitScale(float scale) { }

        public virtual void SetUnitFaceDirection(float faceDirection) { }

        public virtual void SetUnitRotation(float rotation) { }

        public virtual void SetUnitBodyRect(float bodyRect) { }

        public virtual void Pause() { }

        public virtual void Resume() { }

        public virtual void Dispose() { }
    }
}
