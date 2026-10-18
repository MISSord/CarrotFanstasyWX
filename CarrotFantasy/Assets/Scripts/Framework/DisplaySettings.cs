using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// PC 显示设置：分辨率 / 全屏模式。启动时由 GameMain 应用已保存值；运行中由设置面板点「应用」后生效。
    /// </summary>
    public static class DisplaySettings
    {
        private const string PrefWidth = "DisplaySettings.Width";
        private const string PrefHeight = "DisplaySettings.Height";
        private const string PrefMode = "DisplaySettings.FullScreenMode";
        private const string PrefHasSaved = "DisplaySettings.HasSaved";

        private static Resolution[] cachedResolutions;

        public static bool IsSupported
        {
            get
            {
#if UNITY_STANDALONE
                return true;
#else
                return false;
#endif
            }
        }

        public struct Snapshot
        {
            public int Width;
            public int Height;
            public FullScreenMode Mode;
        }

        /// <summary>
        /// 启动时调用：仅当玩家曾点过「应用」才改分辨率。
        /// 首次安装不主动 SetResolution，避免干扰启动流程。
        /// </summary>
        public static void ApplySavedOrDefault()
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            if (!IsSupported || !HasSavedPreference())
            {
                return;
            }

            Apply(LoadSavedOrCurrent(), persist: false);
#endif
        }

        public static bool HasSavedPreference()
        {
            return PlayerPrefs.GetInt(PrefHasSaved, 0) == 1;
        }

        /// <summary>读已保存设置；若无存档则返回当前屏幕状态（不写入）。</summary>
        public static Snapshot LoadSavedOrCurrent()
        {
            if (HasSavedPreference())
            {
                return new Snapshot
                {
                    Width = PlayerPrefs.GetInt(PrefWidth, Screen.width),
                    Height = PlayerPrefs.GetInt(PrefHeight, Screen.height),
                    Mode = (FullScreenMode)PlayerPrefs.GetInt(PrefMode, (int)FullScreenMode.FullScreenWindow),
                };
            }

            return GetCurrent();
        }

        /// <summary>兼容旧调用名。</summary>
        public static Snapshot LoadOrCreateDefault()
        {
            return LoadSavedOrCurrent();
        }

        public static void Apply(Snapshot snapshot, bool persist)
        {
            if (!IsSupported)
            {
                return;
            }

            if (snapshot.Width <= 0 || snapshot.Height <= 0)
            {
                Debug.LogWarning($"[DisplaySettings] 无效分辨率 {snapshot.Width}x{snapshot.Height}，已忽略");
                return;
            }

            try
            {
                Screen.SetResolution(snapshot.Width, snapshot.Height, snapshot.Mode);
                Debug.Log($"[DisplaySettings] Applied {snapshot.Width}x{snapshot.Height} mode={snapshot.Mode}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DisplaySettings] SetResolution 失败: {e}");
                return;
            }

            if (persist)
            {
                Save(snapshot);
            }
        }

        public static Snapshot GetCurrent()
        {
            return new Snapshot
            {
                Width = Screen.width,
                Height = Screen.height,
                Mode = Screen.fullScreenMode,
            };
        }

        public static void Save(Snapshot snapshot)
        {
            PlayerPrefs.SetInt(PrefWidth, snapshot.Width);
            PlayerPrefs.SetInt(PrefHeight, snapshot.Height);
            PlayerPrefs.SetInt(PrefMode, (int)snapshot.Mode);
            PlayerPrefs.SetInt(PrefHasSaved, 1);
            PlayerPrefs.Save();
        }

        /// <summary>去重后的可选分辨率（按宽、高升序）。</summary>
        public static Resolution[] GetAvailableResolutions()
        {
            if (cachedResolutions != null)
            {
                return cachedResolutions;
            }

            Resolution[] raw = Screen.resolutions;
            if (raw == null || raw.Length == 0)
            {
                cachedResolutions = new[]
                {
                    new Resolution { width = Screen.width, height = Screen.height, refreshRate = 60 },
                };
                return cachedResolutions;
            }

            var map = new Dictionary<long, Resolution>();
            for (int i = 0; i < raw.Length; i++)
            {
                Resolution r = raw[i];
                if (r.width < 800 || r.height < 600)
                {
                    continue;
                }

                long key = ((long)r.width << 32) | (uint)r.height;
                if (!map.TryGetValue(key, out Resolution existing) || r.refreshRate > existing.refreshRate)
                {
                    map[key] = r;
                }
            }

            if (map.Count == 0)
            {
                cachedResolutions = new[]
                {
                    new Resolution { width = Screen.width, height = Screen.height, refreshRate = 60 },
                };
                return cachedResolutions;
            }

            var list = new List<Resolution>(map.Values);
            list.Sort((a, b) =>
            {
                int cmp = a.width.CompareTo(b.width);
                return cmp != 0 ? cmp : a.height.CompareTo(b.height);
            });
            cachedResolutions = list.ToArray();
            return cachedResolutions;
        }

        public static int FindResolutionIndex(int width, int height)
        {
            Resolution[] list = GetAvailableResolutions();
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].width == width && list[i].height == height)
                {
                    return i;
                }
            }

            // 找不到精确匹配时，选最接近的
            int best = 0;
            long bestDist = long.MaxValue;
            for (int i = 0; i < list.Length; i++)
            {
                long dw = list[i].width - width;
                long dh = list[i].height - height;
                long dist = dw * dw + dh * dh;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }

            return best;
        }

        public static FullScreenMode[] GetAvailableModes()
        {
            return new[]
            {
                FullScreenMode.ExclusiveFullScreen,
                FullScreenMode.FullScreenWindow,
                FullScreenMode.Windowed,
            };
        }

        public static string GetModeDisplayName(FullScreenMode mode)
        {
            switch (mode)
            {
                case FullScreenMode.ExclusiveFullScreen:
                    return "独占全屏";
                case FullScreenMode.FullScreenWindow:
                    return "无边框全屏";
                case FullScreenMode.Windowed:
                    return "窗口";
                default:
                    return mode.ToString();
            }
        }

        public static int FindModeIndex(FullScreenMode mode)
        {
            FullScreenMode[] modes = GetAvailableModes();
            for (int i = 0; i < modes.Length; i++)
            {
                if (modes[i] == mode)
                {
                    return i;
                }
            }

            return 1; // FullScreenWindow
        }

        public static string FormatResolution(int width, int height)
        {
            return $"{width} x {height}";
        }
    }
}
