using System;
using UnityEngine;

namespace CarrotFantasy
{
    //本地数据保存管理器
    public class LocalStorageManager
    {
        private const string LogTag = "LocalStorageManager";
        private static LocalStorageManager localStorageManager;
        private string account;

        public static LocalStorageManager Instance
        {
            get
            {
                if (localStorageManager == null)
                {
                    localStorageManager = new LocalStorageManager();
                    localStorageManager.account = AccountServer.Instance.GetAccountId();
                }
                return localStorageManager;
            }
        }

        private String GetPlayerStorageData(String value)
        {
            EnsureAccount();
            return String.Format("{0}_{1}", this.account, value);
        }

        public void SetDataToLocal(String key, System.Object value, LocalStorageSaveType valueType)
        {
            if (string.IsNullOrEmpty(key))
            {
                GameLogController.Error("SetDataToLocal失败: key为空", LogTag);
                return;
            }

            if (!TrySetDataToLocalInternal(key, value, valueType))
            {
                GameLogController.Error($"SetDataToLocal失败: key={key}, valueType={valueType}, value={value}", LogTag);
            }
        }

        private bool TrySetDataToLocalInternal(String key, System.Object value, LocalStorageSaveType valueType)
        {
            if (valueType == LocalStorageSaveType.IntType)
            {
                if (value is int intValue)
                {
                    PlayerPrefs.SetInt(key, intValue);
                    return true;
                }
            }
            else if (valueType == LocalStorageSaveType.StringType)
            {
                if (value is String stringValue)
                {
                    PlayerPrefs.SetString(key, stringValue);
                    return true;
                }
            }
            else if (valueType == LocalStorageSaveType.FloatType)
            {
                if (value is float floatValue)
                {
                    PlayerPrefs.SetFloat(key, floatValue);
                    return true;
                }
            }
            else if (valueType == LocalStorageSaveType.BoolType)
            {
                if (value is Boolean boolValue) //判断是不是Boolean类型
                {
                    // 统一使用int存储bool：1=true, 0=false（兼容跨平台与读取性能）
                    PlayerPrefs.SetInt(key, boolValue ? 1 : 0);
                    return true;
                }
            }
            return false;
        }

        public T GetDataFromLocal<T>(String key, System.Object defaultValue, LocalStorageSaveType valueType)
        {
            if (string.IsNullOrEmpty(key))
            {
                GameLogController.Warning("GetDataFromLocal: key为空，返回默认值", LogTag);
                return defaultValue is T castedDefault ? castedDefault : default(T);
            }

            if (valueType == LocalStorageSaveType.StringType)
            {
                if (defaultValue is String value)
                {
                    return (T)(System.Object)PlayerPrefs.GetString(key, value);
                }
            }
            else if (valueType == LocalStorageSaveType.IntType)
            {
                if (defaultValue is int value)
                {
                    return (T)(System.Object)PlayerPrefs.GetInt(key, value);
                }
            }
            else if (valueType == LocalStorageSaveType.FloatType)
            {
                if (defaultValue is float value)
                {
                    return (T)(System.Object)PlayerPrefs.GetFloat(key, value);
                }
            }
            else if (valueType == LocalStorageSaveType.BoolType)
            {
                if (!(defaultValue is bool boolDefaultValue))
                {
                    GameLogController.Warning($"GetDataFromLocal bool默认值类型错误: key={key}", LogTag);
                    return default(T);
                }

                // 新格式：int
                if (PlayerPrefs.HasKey(key))
                {
                    int intBool = PlayerPrefs.GetInt(key, boolDefaultValue ? 1 : 0);
                    return (T)(System.Object)(intBool != 0);
                }

                // 兼容旧格式：字符串 "true"/"false"
                String value = PlayerPrefs.GetString(key, boolDefaultValue ? "true" : "false");
                if (string.Equals(value, "true"))
                {
                    return (T)(System.Object)true;
                }
                else if (string.Equals(value, "false"))
                {
                    return (T)(System.Object)false;
                }
            }

            GameLogController.Warning($"GetDataFromLocal类型不匹配: key={key}, valueType={valueType}, defaultValue={defaultValue}", LogTag);
            return default(T);
        }

        public T GetPlayerInfo<T>(String key, System.Object defaultValue, LocalStorageSaveType valueType)
        {
            if (key == null || defaultValue == null)
            {
                GameLogController.Warning("本地信息获取失败: key或默认值为空", LogTag);
                return default(T);
            }
            return GetDataFromLocal<T>(GetPlayerStorageData(key), defaultValue, valueType);
        }

        public void SetPlayerInfo<T>(String key, System.Object value, LocalStorageSaveType valueType)
        {
            if (key == null || value == null)
            {
                GameLogController.Warning("本地信息设置失败: key或值为空", LogTag);
                return;
            }
            this.SetDataToLocal(GetPlayerStorageData(key), value, valueType);
        }

        public bool HasPlayerInfo(String key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            return PlayerPrefs.HasKey(GetPlayerStorageData(key));
        }

        public void DeletePlayerInfo(String key)
        {
            if (string.IsNullOrEmpty(key))
                return;
            PlayerPrefs.DeleteKey(GetPlayerStorageData(key));
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }

        public void RefreshAccount()
        {
            account = AccountServer.Instance.GetAccountId();
        }

        private void EnsureAccount()
        {
            if (string.IsNullOrEmpty(account))
            {
                RefreshAccount();
            }
        }
    }
}
