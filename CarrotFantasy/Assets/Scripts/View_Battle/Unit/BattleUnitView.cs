using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BattleUnitView
    {
        private Dictionary<String, BaseUnitViewComponent> componentDic = new Dictionary<string, BaseUnitViewComponent>();
        private List<BaseUnitViewComponent> componentList = new List<BaseUnitViewComponent>();

        public BattleView_base battleView;
        public BattleUnit unit;
        public BattleEventDispatcher unitEventDispatcher;

        private Vector3 curViewPosition;
        private float curViewFaceDirection;

        private Vector3 hidePositon = new Vector3(5000, 5000, 0);
        private UnitTransformComponent transformComponent;

        private bool isPaused = false;
        public bool isVisible; //是否可见

        private Vector3 lastPosition;

        public Transform transform { get; set; }

        public virtual void InitTransform(Transform node)
        {
            this.transform = node;
        }

        public void LoadInfo(BattleView_base battleView, BattleUnit battleUnit)
        {
            this.battleView = battleView;
            this.unit = battleUnit;

            this.unitEventDispatcher = battleUnit.eventDipatcher;
            this.isPaused = false;
            this.isVisible = true;
            this.transformComponent = (UnitTransformComponent)this.unit.GetComponent(UnitComponentType.TRANSFORM);
        }

        public virtual void ReloadInfo()
        {
            this.InitViewPosition();
            this.InitViewFaceDirection();
            this.UpdateUnitScale();
            this.UpdateBodyRect();
        }

        public virtual void Init()
        {
            this.InitListener();
            this.InitViewPosition();
            this.InitViewFaceDirection();
            this.UpdateUnitScale();
            this.UpdateBodyRect();
        }

        public virtual void OnTick(float deltaTime)
        {
            this.UpdatePosition(deltaTime);
        }

        public virtual void InitComponents() //子类调用
        {
            for (int i = 0; i <= this.componentList.Count - 1; i++)
            {
                this.componentList[i].Init();
            }
        }

        public void AddComponent(BaseUnitViewComponent component)
        {
            if (component == null) return;
            this.componentDic.Add(component.componetType, component);
            this.componentList.Add(component);
        }

        public void RemoveComponent(BaseUnitViewComponent component)
        {
            if (component == null) return;
            this.componentDic.Remove(component.componetType);
            this.componentList.Remove(component);
        }

        public BaseUnitViewComponent GetComponent(String type)
        {
            return this.componentDic[type];
        }

        public virtual void InitListener()
        {
            this.unitEventDispatcher.AddListener(UnitEvent.FACE_DIRECTION_CHANGE, this.UpdateFaceDirection);
            this.unitEventDispatcher.AddListener(UnitEvent.STATUS_CHANGE, this.UpdateUnitScale);
            this.unitEventDispatcher.AddListener(UnitEvent.ROTATION_CHANGE, this.UpdateRotation);
            this.unitEventDispatcher.AddListener(UnitEvent.BODY_RECT_CHANE, this.UpdateBodyRect);
            this.unitEventDispatcher.AddListener(UnitEvent.POSITION_CHANGE, this.GetLastPosition);
        }

        public virtual void RemoveListener()
        {
            if (this.unitEventDispatcher == null) return;
            this.unitEventDispatcher.RemoveListener(UnitEvent.FACE_DIRECTION_CHANGE, this.UpdateFaceDirection);
            this.unitEventDispatcher.RemoveListener(UnitEvent.STATUS_CHANGE, this.UpdateUnitScale);
            this.unitEventDispatcher.RemoveListener(UnitEvent.ROTATION_CHANGE, this.UpdateRotation);
            this.unitEventDispatcher.RemoveListener(UnitEvent.BODY_RECT_CHANE, this.UpdateBodyRect);
            this.unitEventDispatcher.RemoveListener(UnitEvent.POSITION_CHANGE, this.GetLastPosition);

        }

        private void UpdateUnitScale()
        {
            //for (int i = 0; i <= this.componentList.Count - 1; i++)
            //{
            //this.componentList[i].SetUnitScale(this.transformComponent.scale * this.unit.define.scale);
            //}
            //this.updateUnityScale();
        }

        private void UpdateRotation()
        {
            for (int i = 0; i <= this.componentList.Count - 1; i++)
            {
                this.componentList[i].SetUnitRotation((float)this.transformComponent.rotation);
            }
        }

        private void UpdateBodyRect()
        {

        }

        private void UpdatePosition(float time)
        {
            //Vector3 newViewPosition = Vector3.Lerp(transform.position, this.lastPosition, 1 / Vector3.Distance(transform.position, this.lastPosition) * time);
            //this.curViewPosition = newViewPosition;
            this.curViewPosition = this.lastPosition;
            this.RefreshUnityTransform();
            for (int i = 0; i <= this.componentList.Count - 1; i++)
            {
                this.componentList[i].SetUnitPosition(this.curViewPosition.x, this.curViewPosition.y, this.curViewPosition.z);
            }

        }

        private void InitViewPosition()
        {
            this.GetLastPosition();
            this.curViewPosition = this.lastPosition;
            this.RefreshUnityTransform();
            for (int i = 0; i <= this.componentList.Count - 1; i++)
            {
                this.componentList[i].SetUnitPosition(this.curViewPosition.x, this.curViewPosition.y, this.curViewPosition.z);
            }
        }

        private void GetLastPosition()
        {
            this.transformComponent.GetPosition(out this.lastPosition.x, out this.lastPosition.y, out this.lastPosition.z);
        }

        private void RefreshUnityTransform()
        {
            if (this.isVisible == true)
            {
                this.UpdateUnityPosition();
            }
            else
            {
                if (this.transform == null) return;
                this.transform.localPosition = hidePositon;
            }
        }

        private void UpdateUnityPosition()
        {
            this.transform.localPosition = this.curViewPosition;
        }

        private void InitViewFaceDirection()
        {
            this.curViewFaceDirection = (float)this.transformComponent.faceDirection;
            this.transform.localRotation = Quaternion.Euler(0, this.curViewFaceDirection, 0);
            for (int i = 0; i <= this.componentList.Count - 1; i++)
            {
                this.componentList[i].SetUnitFaceDirection(this.curViewFaceDirection);
            }
        }

        protected virtual void UpdateFaceDirection() //根据移动的方向进行调整
        {
            float faceDirection = (float)this.transformComponent.faceDirection;
            this.transform.localRotation = Quaternion.Euler(0, this.curViewFaceDirection, 0);
            this.curViewFaceDirection = faceDirection;
        }

        public virtual void ClearUnitInfo()
        {
            this.RemoveListener();
            this.unitEventDispatcher = null;
            this.unit = null;
            this.transformComponent = null;
            this.isVisible = false;
            for (int i = this.componentList.Count - 1; i >= 0; i--)
            {
                this.componentList[i].Dispose();
                GameViewObjectPool.Instance.PushViewObjectToPool(this.componentList[i].componetType, this.componentList[i]); ;
            }
            this.componentList.Clear();
            this.componentDic.Clear();
            this.transform = null;
        }

        public virtual void Dispose()
        {
            this.ClearUnitInfo();
            this.battleView = null;
        }
    }
}
