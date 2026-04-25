using System;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class BattleUnitView_Monster : BattleUnitView
    {
        private Slider slider;
        private SpriteRenderer spriteRender;
        private Animator animator;

        private String spriteUrl = "Pictures/NormalMordel/Game/{0}/Monster/{1}-1";
        private String animatorUrl = "Animator/AnimatorController/Monster/{0}/{1}";

        public override void InitTransform(Transform node)
        {
            base.InitTransform(node);
            this.slider = this.transform.Find("MonsterCanvas/HPSlider").GetComponent<Slider>();
            this.spriteRender = this.transform.GetComponent<SpriteRenderer>();
            this.animator = this.transform.GetComponent<Animator>();
            this.slider.value = 1;
            this.slider.gameObject.transform.eulerAngles = Vector3.zero;
        }

        public override void Init()
        {
            base.Init();
            BattleUnit_Monster monster = (BattleUnit_Monster)this.unit;
            this.spriteRender.sprite = ResourceLoader.Instance.loadRes<Sprite>(
                String.Format(this.spriteUrl, monster.curLevel, monster.monsterId));
            this.animator.runtimeAnimatorController = ResourceLoader.Instance.loadRes<RuntimeAnimatorController>(
                String.Format(this.animatorUrl, monster.curLevel, monster.monsterId));
            this.animator.Play(monster.monsterId.ToString());
        }

        public override void InitListener()
        {
            base.InitListener();
            this.unitEventDispatcher.AddListener(BattleEvent.MONSTER_LIVE_REDUCE, this.UpdateLiveNumber);
        }

        public override void RemoveListener()
        {
            base.RemoveListener();
            if (this.unitEventDispatcher != null)
            {
                this.unitEventDispatcher.RemoveListener(BattleEvent.MONSTER_LIVE_REDUCE, this.UpdateLiveNumber);
            }
        }

        private void UpdateLiveNumber()
        {
            this.slider.value = ((float)((BattleUnit_Monster)this.unit).curLive / (float)((BattleUnit_Monster)this.unit).totalLive);
        }

        protected override void UpdateFaceDirection()
        {
            base.UpdateFaceDirection();
            this.slider.gameObject.transform.eulerAngles = Vector3.zero;
        }

        public override void ClearUnitInfo()
        {
            base.ClearUnitInfo();
            this.slider = null;
            this.animator = null;
            this.spriteRender = null;
        }
    }
}
