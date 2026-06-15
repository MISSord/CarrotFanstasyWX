using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>战斗视图 GameObject 对象池键与预制体类型校验（塔与子弹不得共用键）。</summary>
    public static class FightViewGameObjectPoolKeys
    {
        public static string Tower(int towerId, int levelIndex)
        {
            return string.Format("Tower_{0}_{1}", towerId, levelIndex);
        }

        public static string Bullet(int towerId, int levelIndex)
        {
            return string.Format("Bullet_{0}_{1}", towerId, levelIndex);
        }

        /// <summary>旧版未加前缀的键（如 4_1），与子弹池冲突，应在战斗清理时移除。</summary>
        public static bool IsLegacyNumericKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            int separator = key.IndexOf('_');
            if (separator <= 0 || separator >= key.Length - 1)
            {
                return false;
            }

            for (int i = 0; i < separator; i++)
            {
                if (!char.IsDigit(key[i]))
                {
                    return false;
                }
            }

            for (int i = separator + 1; i < key.Length; i++)
            {
                if (!char.IsDigit(key[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsTowerVisual(GameObject go)
        {
            if (go == null)
            {
                return false;
            }

            Transform root = go.transform;
            if (root.Find("Bullect") != null)
            {
                return false;
            }

            return root.Find("tower") != null
                   || root.Find("towerSet") != null
                   || root.Find("attackRange") != null;
        }

        public static bool IsBulletVisual(GameObject go)
        {
            if (go == null)
            {
                return false;
            }

            return go.transform.Find("Bullect") != null;
        }
    }
}
