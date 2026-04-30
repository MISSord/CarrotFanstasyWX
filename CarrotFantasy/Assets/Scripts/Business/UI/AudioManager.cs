using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public enum AudioLoadRoute
    {
        AssetBundle = 0,
        Resources = 1,
    }

    public class AudioManager
    {
        private static AudioManager instance;
        /// <summary>全局单例，首次访问时完成初始化。</summary>
        public static AudioManager Instance => instance ?? CreateInstance();

        private static AudioManager CreateInstance()
        {
            instance = new AudioManager();
            instance.Init();
            return instance;
        }

        public static void Shutdown()
        {
            instance?.Dispose();
        }

        private AudioManager()
        {
        }

        private const string LogTag = "AudioManager";
        /// <summary>音频根节点，承载音乐/音效两个 AudioSource。</summary>
        public GameObject nodeObject { get; private set; }
        private AudioSource audioSourceMusic;
        private AudioSource audioSourceEffect;

        public bool musicEnable { get; private set; }
        public float musicVolume { get; private set; }
        public bool effectEnable { get; private set; }
        public float effectVolume { get; private set; }

        private int musicUidSeed;
        private int currentMusicUid;
        private string currentMusicKey;

        private const float RESET_PLAYING_EFFECT_INTERVAL = 0.1f;
        private readonly Dictionary<string, float> effectLastPlayTime = new Dictionary<string, float>(StringComparer.Ordinal);

        private readonly Dictionary<string, AudioClip> clipCacheAb = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> clipCacheResources = new Dictionary<string, AudioClip>(StringComparer.Ordinal);

        public void Init()
        {
            if (this.nodeObject != null)
            {
                return;
            }

            this.nodeObject = new GameObject("audio_manager_node");

            GameObject audio_music = new GameObject("audio_music");
            audio_music.transform.SetParent(nodeObject.transform, false);
            this.audioSourceMusic = audio_music.AddComponent<AudioSource>();

            GameObject audio_sound_effect = new GameObject("audio_sound_effect");
            audio_sound_effect.transform.SetParent(nodeObject.transform, false);
            this.audioSourceEffect = audio_sound_effect.AddComponent<AudioSource>();

            this.audioSourceMusic.loop = true;


            this.musicEnable = LocalStorageManager.Instance.GetDataFromLocal<bool>
                (LocalStorageType.CUR_USER_MUSIC_ENABLE, (System.Object)true, LocalStorageSaveType.BoolType);
            this.musicVolume = LocalStorageManager.Instance.GetDataFromLocal<float>
                (LocalStorageType.CUR_USER_MUSIC_VOLUME, (System.Object)1f, LocalStorageSaveType.FloatType);
            this.effectEnable = LocalStorageManager.Instance.GetDataFromLocal<bool>
                (LocalStorageType.CUR_USER_MUSIC_EFFECT_ENABLE, (System.Object)true, LocalStorageSaveType.BoolType);
            this.effectVolume = LocalStorageManager.Instance.GetDataFromLocal<float>
                (LocalStorageType.CUR_USER_MUSIC_EFFECT_VOLUME, (System.Object)1f, LocalStorageSaveType.FloatType);

            this.RefreshMusicActiveState();
            this.RefreshMusicVolume();
            this.RefreshEffectActiveState();
            this.RefreshEffectVolume();
        }

        /// <summary>
        /// 规范化 Resources 路径；仅用于 Res 链路，不参与任何 AB 推导。
        /// </summary>
        public static string NormalizeResPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            string p = path.Trim().Replace('\\', '/');
            if (!p.StartsWith("AudioClips/", StringComparison.OrdinalIgnoreCase))
            {
                p = "AudioClips/" + p.TrimStart('/');
            }

            return p;
        }

        /// <summary>AB 地址合法性校验：必须显式传入 bundleName + assetName。</summary>
        private static bool IsValidAbAddress(string bundleName, string assetName)
        {
            return !string.IsNullOrEmpty(bundleName) && !string.IsNullOrEmpty(assetName);
        }

        /// <summary>AB 缓存键：bundle 与 asset 的稳定拼接。</summary>
        private static string BuildAbCacheKey(string bundleName, string assetName)
        {
            return bundleName.Trim().Replace('\\', '/').ToLowerInvariant() + "|" + assetName.Trim();
        }

        /// <summary>
        /// AB 加载链路：只吃 bundleName + assetName，不接受逻辑路径。
        /// </summary>
        private void LoadAudioClipByAbAsync(string bundleName, string assetName, Action<AudioClip> onLoaded)
        {
            if (!IsValidAbAddress(bundleName, assetName))
            {
                onLoaded?.Invoke(null);
                return;
            }

            string cacheKey = BuildAbCacheKey(bundleName, assetName);
            if (this.clipCacheAb.TryGetValue(cacheKey, out AudioClip cached) && cached != null)
            {
                onLoaded?.Invoke(cached);
                return;
            }

            if (AssetBundleManager.Instance == null)
            {
                onLoaded?.Invoke(null);
                return;
            }

            AssetBundleManager.Instance.LoadAsset<AudioClip>(
                bundleName.Trim().Replace('\\', '/').ToLowerInvariant(),
                assetName.Trim(),
                loaded =>
                {
                    if (loaded != null)
                    {
                        this.clipCacheAb[cacheKey] = loaded;
                    }

                    onLoaded?.Invoke(loaded);
                },
                LoadPriority.Medium);
        }

        /// <summary>
        /// Resources 加载链路：只吃 Resources 逻辑路径（AudioClips/...）。
        /// </summary>
        private void LoadAudioClipByResourcesAsync(string resPath, Action<AudioClip> onLoaded)
        {
            string key = NormalizeResPath(resPath);
            if (string.IsNullOrEmpty(key))
            {
                onLoaded?.Invoke(null);
                return;
            }

            if (this.clipCacheResources.TryGetValue(key, out AudioClip cached) && cached != null)
            {
                onLoaded?.Invoke(cached);
                return;
            }

            AudioClip clip = ResourceLoader.Instance.loadRes<AudioClip>(key);
            if (clip != null)
            {
                this.clipCacheResources[key] = clip;
            }

            onLoaded?.Invoke(clip);
        }

        public void RefreshMusicActiveState()
        {
            this.audioSourceMusic.enabled = this.musicEnable;
        }

        public void RefreshMusicVolume()
        {
            this.audioSourceMusic.volume = this.musicVolume;
        }

        public void RefreshEffectActiveState()
        {
            this.audioSourceEffect.enabled = this.effectEnable;
        }

        public void RefreshEffectVolume()
        {
            this.audioSourceEffect.volume = this.effectVolume;
        }

        /// <summary>播放背景音乐（Resources）。</summary>
        public int PlayMusicByResources(String resPath, int priority = 1, Action<int> onPlaybackStarted = null)
        {
            string normalizedPath = NormalizeResPath(resPath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                GameLogController.Warning("PlayMusicByResources path 为空，已忽略", LogTag);
                return -1;
            }

            int uid = ++this.musicUidSeed;
            this.LoadAudioClipByResourcesAsync(normalizedPath, clip =>
            {
                if (clip == null)
                {
                    GameLogController.Warning("音乐资源不存在: " + normalizedPath, LogTag);
                    return;
                }

                this.audioSourceMusic.clip = clip;
                this.audioSourceMusic.Play();
                this.currentMusicUid = uid;
                this.currentMusicKey = normalizedPath;
                onPlaybackStarted?.Invoke(uid);
            });
            return uid;
        }

        /// <summary>播放背景音乐（AssetBundle）。</summary>
        public int PlayMusicByAb(string bundleName, string assetName, int priority = 1, Action<int> onPlaybackStarted = null)
        {
            if (!IsValidAbAddress(bundleName, assetName))
            {
                GameLogController.Warning("PlayMusicByAb 参数为空，已忽略", LogTag);
                return -1;
            }

            int uid = ++this.musicUidSeed;
            string key = BuildAbCacheKey(bundleName, assetName);
            this.LoadAudioClipByAbAsync(bundleName, assetName, clip =>
            {
                if (clip == null)
                {
                    GameLogController.Warning("音乐资源不存在: " + key, LogTag);
                    return;
                }

                this.audioSourceMusic.clip = clip;
                this.audioSourceMusic.Play();
                this.currentMusicUid = uid;
                this.currentMusicKey = key;
                onPlaybackStarted?.Invoke(uid);
            });
            return uid;
        }

        /// <summary>兼容入口：等价于 <see cref="PlayMusicByAb"/>。</summary>
        public int PlayMusic(string bundleName, string assetName, int priority = 1, Action<int> onPlaybackStarted = null)
        {
            return this.PlayMusicByAb(bundleName, assetName, priority, onPlaybackStarted);
        }

        public void StopMusic()
        {
            this.audioSourceMusic.Stop();
            this.currentMusicUid = 0;
            this.currentMusicKey = null;
        }

        public void StopMusic(int uuid)
        {
            if (uuid == this.currentMusicUid)
            {
                this.StopMusic();
            }
        }

        /// <summary>播放音效（AssetBundle）。</summary>
        public void PlayEffectByAb(string bundleName, string assetName, int volumeScale = 1, Action<bool> onComplete = null)
        {
            if (!this.effectEnable)
            {
                onComplete?.Invoke(false);
                return;
            }

            if (!IsValidAbAddress(bundleName, assetName))
            {
                onComplete?.Invoke(false);
                return;
            }

            string key = BuildAbCacheKey(bundleName, assetName);
            if (this.IsEffectThrottled(key))
            {
                onComplete?.Invoke(false);
                return;
            }

            this.LoadAudioClipByAbAsync(bundleName, assetName, clip =>
            {
                if (!this.effectEnable || clip == null)
                {
                    onComplete?.Invoke(false);
                    return;
                }

                this.audioSourceEffect.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
                onComplete?.Invoke(true);
            });
        }

        /// <summary>播放音效（Resources）。</summary>
        public void PlayEffectByResources(string resPath, int volumeScale = 1, Action<bool> onComplete = null)
        {
            if (!this.effectEnable)
            {
                onComplete?.Invoke(false);
                return;
            }

            string key = NormalizeResPath(resPath);
            if (string.IsNullOrEmpty(key))
            {
                onComplete?.Invoke(false);
                return;
            }

            if (this.IsEffectThrottled(key))
            {
                onComplete?.Invoke(false);
                return;
            }

            this.LoadAudioClipByResourcesAsync(key, clip =>
            {
                if (!this.effectEnable || clip == null)
                {
                    onComplete?.Invoke(false);
                    return;
                }

                this.audioSourceEffect.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
                onComplete?.Invoke(true);
            });
        }

        /// <summary>
        /// 音效同 key 短间隔防抖，避免连续点击导致重复叠加过密。
        /// </summary>
        private bool IsEffectThrottled(string key)
        {
            float curTime = Time.realtimeSinceStartup;
            if (this.effectLastPlayTime.TryGetValue(key, out float lastPlayTime) &&
                lastPlayTime + RESET_PLAYING_EFFECT_INTERVAL >= curTime)
            {
                return true;
            }

            this.effectLastPlayTime[key] = curTime;
            return false;
        }

        public void StopEffectClip()
        {
            this.audioSourceEffect.Stop();
        }

        public bool GetMusicEnable()
        {
            return this.musicEnable;
        }

        public bool GetEffectEnable()
        {
            return this.effectEnable;
        }

        public float GetMusicVolume()
        {
            return this.musicVolume;
        }

        public float GetEffectVolue()
        {
            return this.effectVolume;
        }

        public void SetMusicVolume(int volume)
        {
            SetMusicVolume((float)volume);
        }

        public void SetMusicVolume(float volume)
        {
            this.musicVolume = Mathf.Clamp01(volume);
            this.RefreshMusicVolume();
            LocalStorageManager.Instance.SetPlayerInfo<float>(LocalStorageType.CUR_USER_MUSIC_VOLUME, this.musicVolume, LocalStorageSaveType.FloatType);
            LocalStorageManager.Instance.Save();
        }

        public void SetEffectVolume(int volume)
        {
            SetEffectVolume((float)volume);
        }

        public void SetEffectVolume(float volume)
        {
            this.effectVolume = Mathf.Clamp01(volume);
            this.RefreshEffectVolume();
            LocalStorageManager.Instance.SetPlayerInfo<float>(LocalStorageType.CUR_USER_MUSIC_EFFECT_VOLUME, this.effectVolume, LocalStorageSaveType.FloatType);
            LocalStorageManager.Instance.Save();
        }

        public void SetMusicEnable(bool isActive)
        {
            if (this.musicEnable != isActive)
            {
                this.musicEnable = isActive;
                if (isActive == true)
                {
                    LocalStorageManager.Instance.SetDataToLocal(LocalStorageType.CUR_USER_MUSIC_ENABLE, (System.Object)true, LocalStorageSaveType.BoolType);
                }
                else
                {
                    LocalStorageManager.Instance.SetDataToLocal(LocalStorageType.CUR_USER_MUSIC_ENABLE, (System.Object)false, LocalStorageSaveType.BoolType);
                }
                LocalStorageManager.Instance.Save();

            }
            this.RefreshMusicActiveState();
        }

        public void SetEffectEnable(bool isActive)
        {
            if (this.effectEnable != isActive)
            {
                this.effectEnable = isActive;
                if (isActive == true)
                {
                    LocalStorageManager.Instance.SetDataToLocal(LocalStorageType.CUR_USER_MUSIC_EFFECT_ENABLE, (System.Object)true, LocalStorageSaveType.BoolType);
                }
                else
                {
                    LocalStorageManager.Instance.SetDataToLocal(LocalStorageType.CUR_USER_MUSIC_EFFECT_ENABLE, (System.Object)false, LocalStorageSaveType.BoolType);
                }
                LocalStorageManager.Instance.Save();
            }
            this.RefreshEffectActiveState();
        }

        public void Dispose()
        {
            if (this.nodeObject == null)
            {
                return;
            }

            this.audioSourceMusic?.Stop();
            this.audioSourceEffect?.Stop();
            this.effectLastPlayTime.Clear();
            this.clipCacheAb.Clear();
            this.clipCacheResources.Clear();
            this.currentMusicUid = 0;
            this.currentMusicKey = null;
            GameObject.Destroy(this.nodeObject);
            this.nodeObject = null;
            this.audioSourceMusic = null;
            this.audioSourceEffect = null;

            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
