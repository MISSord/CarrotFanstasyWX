using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(AudioSource))]
    public class ButtonClickPlayAudio : MonoBehaviour
    {
        [SerializeField] private bool restartWhenPlaying = true;

        private Button button;
        private AudioSource audioSource;

        private void Awake()
        {
            this.button = GetComponent<Button>();
            this.audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            this.button.onClick.AddListener(this.PlayAudio);
        }

        private void OnDisable()
        {
            this.button.onClick.RemoveListener(this.PlayAudio);
        }

        private void PlayAudio()
        {
            if (this.audioSource == null || this.audioSource.clip == null)
            {
                return;
            }

            if (this.restartWhenPlaying)
            {
                this.audioSource.Stop();
            }

            this.audioSource.Play();
        }
    }
}
