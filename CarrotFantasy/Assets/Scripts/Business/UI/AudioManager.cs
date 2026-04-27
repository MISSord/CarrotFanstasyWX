using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class MusicInfo
    {
        public String path;
        public int prority;
        public int uid;
        public MusicInfo(String upath, int uprority, int uuid)
        {
            path = upath;
            prority = uprority;
            uid = uuid;
        }
    }


    public class AudioManager
    {
        private const string LogTag = "AudioManager";
        public GameObject nodeObject;
        private AudioSource audioSourceMusic;
        private AudioSource audioSourceEffect;

        public bool musicEnable { get; private set; }
        public float musicVolume { get; private set; }
        public bool effectEnable { get; private set; }
        public float effectVolume { get; private set; }

        private int uid = 0;

        private const float RESET_PLAYING_EFFECT_INTERVAL = 0.1f;
        private readonly Dictionary<string, float> effectLastPlayTime = new Dictionary<string, float>();

        Dictionary<int, MusicInfo> uid2MusicInfo = new Dictionary<int, MusicInfo>();
        Dictionary<int, List<MusicInfo>> musicProrityGroupMap = new Dictionary<int, List<MusicInfo>>();
        List<int> orderOfPriority = new List<int>();

        int currentUid = 0;
        string currentMusicPath = null;

        public void Init()
        {
            this.nodeObject = new GameObject("node_object");

            GameObject audio_music = new GameObject("audio_musice");
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

        public int PlayMusic(String path, int priority = 1)
        {
            if (string.IsNullOrEmpty(path))
            {
                GameLogController.Warning("PlayMusic path 为空，已忽略", LogTag);
                return -1;
            }

            uid = uid + 1;
            MusicInfo musicInfo = new MusicInfo(path, priority, uid);
            this.uid2MusicInfo.Add(uid, musicInfo);
            if (this.musicProrityGroupMap.ContainsKey(priority) == false)
            {
                bool insertSuc = false;
                for (int i = 0; i <= this.orderOfPriority.Count - 1; i++)
                {
                    if (priority > orderOfPriority[i])
                    {
                        orderOfPriority.Insert(i, priority);
                        insertSuc = true;
                        break;
                    }
                }
                if (!insertSuc)
                    orderOfPriority.Add(priority);
                this.musicProrityGroupMap[priority] = new List<MusicInfo>();
            }
            this.musicProrityGroupMap[priority].Add(musicInfo);
            this.CheckMusic();
            return uid;
        }

        private void CheckMusic()
        {
            for (int i = 0; i <= this.orderOfPriority.Count - 1; i++)
            {
                if (this.musicProrityGroupMap[orderOfPriority[i]].Count > 0)
                {
                    MusicInfo musicInfo = this.musicProrityGroupMap[orderOfPriority[i]][0];
                    if (musicInfo.uid != this.currentUid)
                    {
                        AudioClip clip = ResourceLoader.Instance.loadRes<AudioClip>(musicInfo.path);
                        if (clip == null)
                        {
                            GameLogController.Warning("音乐资源不存在: " + musicInfo.path, LogTag);
                            return;
                        }
                        if (this.currentMusicPath == musicInfo.path && this.audioSourceMusic.isPlaying)
                        {
                            this.currentUid = musicInfo.uid;
                            return;
                        }
                        this.audioSourceMusic.clip = clip;
                        this.audioSourceMusic.Play();
                        this.currentUid = musicInfo.uid;
                        this.currentMusicPath = musicInfo.path;
                    }
                    break;
                }
            }
        }

        public void StopMusic()
        {
            this.audioSourceMusic.Stop();
            this.currentUid = 0;
            this.currentMusicPath = null;
        }

        public void StopMusic(int uuid)
        {
            MusicInfo musicInfo;
            if (this.uid2MusicInfo.TryGetValue(uuid, out musicInfo))
            {
                this.uid2MusicInfo.Remove(uuid);
                this.musicProrityGroupMap[musicInfo.prority].Remove(musicInfo);
                if (this.musicProrityGroupMap[musicInfo.prority].Count == 0)
                {
                    this.musicProrityGroupMap.Remove(musicInfo.prority);
                    this.orderOfPriority.Remove(musicInfo.prority);
                }
                this.CheckMusic();
            }
        }

        public void PlayEffect(String path, int volumeScale = 1)
        {
            if (this.effectEnable == false) { return; }
            if (string.IsNullOrEmpty(path)) { return; }
            float curTime = Time.realtimeSinceStartup;
            float lastPlayTime = -RESET_PLAYING_EFFECT_INTERVAL;
            if (this.effectLastPlayTime.TryGetValue(path, out float time))
            {
                lastPlayTime = time;
            }

            if (lastPlayTime + RESET_PLAYING_EFFECT_INTERVAL >= curTime)
            {
                return;
            }

            this.effectLastPlayTime[path] = curTime;
            AudioClip clip = ResourceLoader.Instance.loadRes<AudioClip>(path);
            if (clip == null)
            {
                GameLogController.Warning("音效资源不存在: " + path, LogTag);
                return;
            }
            this.audioSourceEffect.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
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
            this.audioSourceMusic?.Stop();
            this.audioSourceEffect?.Stop();
            this.uid2MusicInfo.Clear();
            this.musicProrityGroupMap.Clear();
            this.orderOfPriority.Clear();
            this.effectLastPlayTime.Clear();
            this.currentUid = 0;
            this.currentMusicPath = null;
            GameObject.Destroy(this.nodeObject);
            this.nodeObject = null;
            this.audioSourceMusic = null;
            this.audioSourceEffect = null;
        }
    }
}
