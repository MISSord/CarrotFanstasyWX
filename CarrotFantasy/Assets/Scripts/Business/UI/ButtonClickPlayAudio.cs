using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>
    /// 按钮点击时通过 <see cref="AudioManager"/> 播放音效（默认走 AB 路径）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class ButtonClickPlayAudio : MonoBehaviour
    {
        [Tooltip("Resources 音效路径（AudioClips/...）；仅在 loadRoute=Resources 时使用")]
        [SerializeField]
        private string effectClipPath;

        [Tooltip("加载路线：正式内容建议 AssetBundle；编辑器临时资源可选 Resources")]
        [SerializeField]
        private AudioLoadRoute loadRoute = AudioLoadRoute.AssetBundle;

        [Tooltip("AB 音效的 bundleName；仅在 loadRoute=AssetBundle 时使用")]
        [SerializeField]
        private string effectAbBundleName;

        [Tooltip("AB 音效的 assetName；仅在 loadRoute=AssetBundle 时使用")]
        [SerializeField]
        private string effectAbAssetName;

        [Tooltip("为 true 时先停止当前全局音效通道再播放（近似「重来一遍」；连续点击可能被 AudioManager 内同路径防抖间隔限制）")]
        [SerializeField]
        private bool restartWhenPlaying = true;

        [Tooltip("传入 AudioManager.PlayEffect 的音量系数")]
        [SerializeField]
        private int volumeScale = 1;

        private Button button;

        private void Awake()
        {
            this.button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            XUI.AddButtonListener(this.button, this.PlayAudio);
        }

        private void OnDisable()
        {
            this.button.onClick.RemoveListener(this.PlayAudio);
        }

        private void PlayAudio()
        {
            AudioManager audio = AudioManager.Instance;
            if (this.restartWhenPlaying)
            {
                audio.StopEffectClip();
            }

            if (this.loadRoute == AudioLoadRoute.Resources)
            {
                audio.PlayEffectByResources(this.effectClipPath, this.volumeScale);
                return;
            }

            audio.PlayEffectByAb(this.effectAbBundleName, this.effectAbAssetName, this.volumeScale);
        }
    }
}
