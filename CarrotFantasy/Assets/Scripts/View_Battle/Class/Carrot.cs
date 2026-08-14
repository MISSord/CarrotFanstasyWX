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
        private Animator animator;
        private float timeVal;
        private SpriteRenderer sr;
        private SpriteLoader spriteLoader;
        private Text hpText;

        private BattlePVEDataComponent dataComponent;

        public void Init(BaseBattle battle)
        {
            this.animator = this.GetComponent<Animator>();
            this.sr = this.GetComponent<SpriteRenderer>();
            this.spriteLoader = this.GetComponent<SpriteLoader>();
            if (this.spriteLoader == null && this.sr != null)
            {
                this.spriteLoader = this.gameObject.AddComponent<SpriteLoader>();
            }

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
                if (this.spriteLoader != null)
                {
                    this.spriteLoader.ReleaseCurrent();
                }
                else
                {
                    this.sr.sprite = null;
                }

                this.timeVal = 0f;
                return;
            }

            this.animator.enabled = false;
            int spriteIndex = hp >= 7 ? 6 : (hp - 1);
            if (spriteIndex < 0)
            {
                return;
            }

            if (this.spriteLoader != null)
            {
                this.spriteLoader.SetAtlasSprite(
                    FightViewSpriteAb.CarrotAtlas,
                    FightViewSpriteAb.CarrotStateAsset(spriteIndex));
            }
            else if (!FightViewSpriteAb.TryGetCarrotState(spriteIndex, out Sprite sprite))
            {
                Debug.LogError("[Carrot] 状态 Sprite 未预加载: index=" + spriteIndex);
            }
            else
            {
                this.sr.sprite = sprite;
            }
        }

        public void Dispose()
        {
            if (this.dataComponent != null)
            {
                this.dataComponent.eventDispatcher.RemoveListener(BattleEvent.CARROT_LIVE_REDUCE, this.UpdateCarrotUI);
            }

            if (this.spriteLoader != null)
            {
                this.spriteLoader.ReleaseCurrent();
            }
        }
    }
}
