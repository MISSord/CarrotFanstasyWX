using System.Collections.Generic;
using UnityEngine;

public enum GameLogLevel
{
    Off = 0,
    Error = 1,
    Warning = 2,
    Info = 3,
    Verbose = 4
}

/// <summary>
/// 统一日志控制器：
/// 1) 支持全局日志级别
/// 2) 支持模块级日志级别覆盖
/// 3) 可同步到 Unity 全局 logger 过滤
/// </summary>
public static class GameLogController
{
    private const string LogLevelKey = "GameLogController.Level";
    private static readonly Dictionary<string, GameLogLevel> TagLevels = new Dictionary<string, GameLogLevel>();

    public static GameLogLevel GlobalLevel { get; private set; } = GameLogLevel.Info;
    public static bool SyncUnityLoggerFilter { get; set; } = true;

    static GameLogController()
    {
        if (PlayerPrefs.HasKey(LogLevelKey))
        {
            GlobalLevel = (GameLogLevel)PlayerPrefs.GetInt(LogLevelKey, (int)GameLogLevel.Info);
        }
        ApplyUnityFilter();
    }

    public static void SetGlobalLevel(GameLogLevel level, bool persist = true)
    {
        GlobalLevel = level;
        if (persist)
        {
            PlayerPrefs.SetInt(LogLevelKey, (int)level);
            PlayerPrefs.Save();
        }
        ApplyUnityFilter();
    }

    public static void SetTagLevel(string tag, GameLogLevel level)
    {
        if (string.IsNullOrEmpty(tag))
            return;

        TagLevels[tag] = level;
    }

    public static void ClearTagLevel(string tag)
    {
        if (string.IsNullOrEmpty(tag))
            return;

        TagLevels.Remove(tag);
    }

    public static bool IsEnabled(GameLogLevel level, string tag = null)
    {
        if (level == GameLogLevel.Off)
            return false;

        GameLogLevel effectiveLevel = GlobalLevel;
        if (!string.IsNullOrEmpty(tag) && TagLevels.TryGetValue(tag, out GameLogLevel tagLevel))
        {
            effectiveLevel = tagLevel;
        }

        return level <= effectiveLevel;
    }

    public static void Log(string message, string tag = null, Object context = null)
    {
        if (!IsEnabled(GameLogLevel.Info, tag))
            return;
        Write(GameLogLevel.Info, message, tag, context);
    }

    public static void Verbose(string message, string tag = null, Object context = null)
    {
        if (!IsEnabled(GameLogLevel.Verbose, tag))
            return;
        Write(GameLogLevel.Verbose, message, tag, context);
    }

    public static void Warning(string message, string tag = null, Object context = null)
    {
        if (!IsEnabled(GameLogLevel.Warning, tag))
            return;
        Write(GameLogLevel.Warning, message, tag, context);
    }

    public static void Error(string message, string tag = null, Object context = null)
    {
        if (!IsEnabled(GameLogLevel.Error, tag))
            return;
        Write(GameLogLevel.Error, message, tag, context);
    }

    private static void Write(GameLogLevel level, string message, string tag, Object context)
    {
        string finalMessage = string.IsNullOrEmpty(tag) ? message : $"[{tag}] {message}";
        switch (level)
        {
            case GameLogLevel.Error:
                if (context != null) Debug.LogError(finalMessage, context);
                else Debug.LogError(finalMessage);
                break;
            case GameLogLevel.Warning:
                if (context != null) Debug.LogWarning(finalMessage, context);
                else Debug.LogWarning(finalMessage);
                break;
            default:
                if (context != null) Debug.Log(finalMessage, context);
                else Debug.Log(finalMessage);
                break;
        }
    }

    private static void ApplyUnityFilter()
    {
        if (!SyncUnityLoggerFilter)
            return;

        if (GlobalLevel == GameLogLevel.Off)
        {
            Debug.unityLogger.logEnabled = false;
            return;
        }

        Debug.unityLogger.logEnabled = true;
        switch (GlobalLevel)
        {
            case GameLogLevel.Error:
                Debug.unityLogger.filterLogType = LogType.Error;
                break;
            case GameLogLevel.Warning:
                Debug.unityLogger.filterLogType = LogType.Warning;
                break;
            default:
                Debug.unityLogger.filterLogType = LogType.Log;
                break;
        }
    }
}
