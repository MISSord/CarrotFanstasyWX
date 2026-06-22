using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>单机模式地图快照本地持久化（PlayerPrefs，按 userId 分档）。</summary>
    internal static class StandaloneMapPersistence
    {
        private const string KeyPrefix = "standalone_map_snapshot_";

        public static string TryLoad(long userId)
        {
            if (userId <= 0)
            {
                return null;
            }

            string key = BuildKey(userId);
            if (!PlayerPrefs.HasKey(key))
            {
                return null;
            }

            string text = PlayerPrefs.GetString(key, string.Empty).Trim();
            return IsWellFormed(text) ? text : null;
        }

        public static void Save(long userId, string snapshot)
        {
            if (userId <= 0 || string.IsNullOrEmpty(snapshot) || !IsWellFormed(snapshot))
            {
                return;
            }

            PlayerPrefs.SetString(BuildKey(userId), snapshot);
            PlayerPrefs.Save();
        }

        public static void Delete(long userId)
        {
            if (userId <= 0)
            {
                return;
            }

            PlayerPrefs.DeleteKey(BuildKey(userId));
            PlayerPrefs.Save();
        }

        static string BuildKey(long userId)
        {
            return KeyPrefix + userId;
        }

        static bool IsWellFormed(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            return text.Split('#').Length >= 16;
        }
    }
}
