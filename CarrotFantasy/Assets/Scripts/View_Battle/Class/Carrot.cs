using System;
using UnityEngine;
using UnityEngine.UI;



/// <summary>
/// 萝卜
/// </summary>

namespace CarrotFantasy
{
    public class Carrot : MonoBehaviour
    {

        //萝卜的不同状态
        private Sprite[] sprites;
        private Animator animator;
        private float timeVal;
        private SpriteRenderer sr;
        private Text hpText;

        private BattlePVEDataComponent dataComponent;

        public void Init(BaseBattle battle)
        {
            sprites = new Sprite[7];
            for (int i = 0; i < sprites.Length; i++)
            {
                if (!FightViewSpriteAb.TryGetCarrotState(i, out sprites[i]))
                {
                    Debug.LogError("[Carrot] 状态 Sprite 未预加载: index=" + i);
                }
            }

            this.animator = this.GetComponent<Animator>();
            this.sr = this.GetComponent<SpriteRenderer>();

            Transform hpTextTransform = this.transform.Find("HpCanvas/txt_live");
            if (hpTextTransform != null)
            {
                this.hpText = hpTextTransform.GetComponent<Text>();
            }

            this.dataComponent = BattlePVEDataComponent.GetFrom(battle);
            if (this.dataComponent == null)
            {
                Debug.LogError("[Carrot] BattlePVEDataComponent 未就绪，跳过萝卜 UI 绑定。");
                return;
            }

            this.dataComponent.eventDispatcher.RemoveListener(BattleEvent.CARROT_LIVE_REDUCE, this.UpdateCarrotUI);
            this.dataComponent.eventDispatcher.AddListener(BattleEvent.CARROT_LIVE_REDUCE, this.UpdateCarrotUI);
            this.ResetVisualToCurrentHp();
        }

        // Update is called once per frame
        void Update()
        {
            if (timeVal >= 5)
            {
                animator.Play("Idle");
                timeVal = 0;
            }
            else
            {
                timeVal += Time.deltaTime;
            }
        }

        private void OnMouseDown()
        {
            if (this.dataComponent == null)
            {
                return;
            }

            if (this.dataComponent.carrotLive >= 10)
            {
                this.animator.Play("Touch");
                int randomNum = UnityEngine.Random.Range(1, 4);
                AudioManager.Instance.PlayEffectByResources(
                    String.Format("NormalMordel/Carrot/{0}", randomNum.ToString()));
            }
        }

        public void UpdateCarrotUI()
        {
            this.ResetVisualToCurrentHp();
        }

        void ResetVisualToCurrentHp()
        {
            if (this.dataComponent == null || this.hpText == null || this.sr == null || this.animator == null)
            {
                return;
            }

            int hp = this.dataComponent.carrotLive;
            this.hpText.text = hp.ToString();

            if (hp >= 10)
            {
                this.animator.enabled = true;
                this.animator.Play("Idle", 0, 0f);
                this.sr.sprite = null;
                this.timeVal = 0f;
                return;
            }

            this.animator.enabled = false;
            if (hp >= 7)
            {
                this.sr.sprite = this.sprites[6];
            }
            else if (hp > 0)
            {
                this.sr.sprite = this.sprites[hp - 1];
            }
        }

        public void Dispose()
        {
            if (this.dataComponent != null)
            {
                this.dataComponent.eventDispatcher.RemoveListener(BattleEvent.CARROT_LIVE_REDUCE, this.UpdateCarrotUI);
            }
        }
    }
}



