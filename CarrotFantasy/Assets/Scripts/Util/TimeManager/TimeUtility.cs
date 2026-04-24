using UnityEngine;
using System;
using System.Collections.Generic;

public class TimeUtility : MonoBehaviour
{
    private static TimeUtility _instance;
    public static TimeUtility Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("TimeUtility");
                _instance = go.AddComponent<TimeUtility>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    #region 时间数据结构

    // 延迟任务数据结构
    private class DelayTask
    {
        public float delayTime;
        public float elapsedTime;
        public Action callback;
        public bool useRealTime;
        public string taskid;
    }

    // 间隔任务数据结构
    private class IntervalTask
    {
        public string taskId;
        public float interval;
        public float elapsedTime;
        public Action callback;
        public bool useRealTime;
        public bool isActive = true;

        // 新增：执行次数相关字段
        public int executeCount;      // 已执行次数
        public int maxExecuteCount;   // 最大执行次数（0表示无限次）
        public Action onComplete;     // 所有次数执行完成后的回调
    }

    // 定点任务数据结构
    private class ScheduledTask
    {
        public string taskId;
        public int targetHour;
        public int targetMinute;
        public int targetSecond;
        public Action callback;
        public bool isDaily; // 是否每日重复
        public DateTime lastTriggerDate;
        public bool isActive = true;
    }

    #endregion

    #region 任务容器

    private List<DelayTask> delayTasks = new List<DelayTask>();
    private Dictionary<string, IntervalTask> intervalTasks = new Dictionary<string, IntervalTask>();
    private Dictionary<string, ScheduledTask> scheduledTasks = new Dictionary<string, ScheduledTask>();

    // 用于记录真实时间（不受Time.timeScale影响）
    private float lastRealTime;

    #endregion

    #region 初始化

    private void Awake()
    {
        lastRealTime = Time.realtimeSinceStartup;
    }

    private void Update()
    {
        float currentRealTime = Time.realtimeSinceStartup;
        float realDeltaTime = currentRealTime - lastRealTime;
        lastRealTime = currentRealTime;

        UpdateDelayTasks(realDeltaTime);
        UpdateIntervalTasks(realDeltaTime);
        UpdateScheduledTasks();
    }

    #endregion

    #region 公共属性

    /// <summary>
    /// 获取当前系统时间
    /// </summary>
    public DateTime CurrentTime => DateTime.Now;

    /// <summary>
    /// 获取当前UTC时间
    /// </summary>
    public DateTime CurrentUTCTime => DateTime.UtcNow;

    /// <summary>
    /// 获取游戏运行时间（从游戏开始到现在的秒数）
    /// </summary>
    public float GameTime => Time.time;

    /// <summary>
    /// 获取实时时间（即使游戏暂停也会继续增加）
    /// </summary>
    public float RealTimeSinceStartup => Time.realtimeSinceStartup;

    #endregion

    #region 延迟回调实现

    /// <summary>
    /// 延迟回调
    /// </summary>
    /// <param name="delaySeconds">延迟时间（秒）</param>
    /// <param name="callback">回调函数</param>
    /// <param name="useRealTime">是否使用真实时间（不受Time.timeScale影响）</param>
    public void SetTimeout(float delaySeconds, Action callback, bool useRealTime = false, string taskid = null)
    {
        var task = new DelayTask
        {
            delayTime = delaySeconds,
            elapsedTime = 0f,
            callback = callback,
            useRealTime = useRealTime,
            taskid = taskid
        };
        delayTasks.Add(task);
    }

    public void RemoveTimeout(string id)
    {
        if (id == null || id == "") return;
        for(int i = delayTasks.Count - 1; i >= 0; --i)
        {
            if (delayTasks[i].taskid == id)
            {
                //一次只移除一个，理论上名字也应该是唯一的
                delayTasks.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>
    /// 更新延迟任务
    /// </summary>
    private void UpdateDelayTasks(float realDeltaTime)
    {
        float deltaTime = Time.deltaTime;

        for (int i = delayTasks.Count - 1; i >= 0; i--)
        {
            var task = delayTasks[i];

            // 根据任务类型选择时间增量
            float timeIncrement = task.useRealTime ? realDeltaTime : deltaTime;
            task.elapsedTime += timeIncrement;

            // 检查是否到达延迟时间
            if (task.elapsedTime >= task.delayTime)
            {
                task.callback?.Invoke();
                delayTasks.RemoveAt(i);
            }
        }
    }

    #endregion

    #region 间隔回调实现（增强版）

    /// <summary>
    /// 添加间隔回调任务（无限次执行）
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="intervalSeconds">间隔时间（秒）</param>
    /// <param name="callback">回调函数</param>
    /// <param name="useRealTime">是否使用真实时间</param>
    public void AddIntervalTask(string taskId, float intervalSeconds, Action callback, bool useRealTime = false)
    {
        AddIntervalTask(taskId, intervalSeconds, callback, 0, null, useRealTime);
    }

    /// <summary>
    /// 添加间隔回调任务（指定执行次数）
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="intervalSeconds">间隔时间（秒）</param>
    /// <param name="callback">每次间隔执行的回调</param>
    /// <param name="executeCount">执行次数（0表示无限次）</param>
    /// <param name="onComplete">所有次数执行完成后的回调（仅当executeCount>0时有效）</param>
    /// <param name="useRealTime">是否使用真实时间</param>
    public void AddIntervalTask(string taskId, float intervalSeconds, Action callback, int executeCount, Action onComplete = null, bool useRealTime = false)
    {
        if (intervalTasks.ContainsKey(taskId))
        {
            Debug.LogWarning($"Interval task with ID {taskId} already exists!");
            return;
        }

        var task = new IntervalTask
        {
            taskId = taskId,
            interval = intervalSeconds,
            elapsedTime = 0f,
            callback = callback,
            useRealTime = useRealTime,
            executeCount = 0,
            maxExecuteCount = executeCount,
            onComplete = onComplete
        };
        intervalTasks[taskId] = task;
    }

    /// <summary>
    /// 获取间隔任务的已执行次数
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <returns>已执行次数，如果任务不存在返回-1</returns>
    public int GetIntervalTaskExecuteCount(string taskId)
    {
        if (intervalTasks.TryGetValue(taskId, out IntervalTask task))
        {
            return task.executeCount;
        }
        return -1;
    }

    /// <summary>
    /// 获取间隔任务的剩余执行次数
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <returns>剩余执行次数，如果任务不存在或无限次返回-1</returns>
    public int GetIntervalTaskRemainingCount(string taskId)
    {
        if (intervalTasks.TryGetValue(taskId, out IntervalTask task))
        {
            if (task.maxExecuteCount == 0) return -1; // 无限次
            return task.maxExecuteCount - task.executeCount;
        }
        return -1;
    }

    /// <summary>
    /// 重置间隔任务的执行计数
    /// </summary>
    /// <param name="taskId">任务ID</param>
    public void ResetIntervalTaskCount(string taskId)
    {
        if (intervalTasks.TryGetValue(taskId, out IntervalTask task))
        {
            task.executeCount = 0;
        }
    }

    /// <summary>
    /// 移除间隔回调任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    public void RemoveIntervalTask(string taskId)
    {
        if (intervalTasks.ContainsKey(taskId))
        {
            intervalTasks[taskId].isActive = false;
            intervalTasks.Remove(taskId);
        }
    }

    /// <summary>
    /// 更新间隔任务
    /// </summary>
    private void UpdateIntervalTasks(float realDeltaTime)
    {
        float deltaTime = Time.deltaTime;
        var tasksToRemove = new List<string>();

        foreach (var kvp in intervalTasks)
        {
            var task = kvp.Value;
            if (!task.isActive)
            {
                tasksToRemove.Add(task.taskId);
                continue;
            }

            // 检查是否达到最大执行次数（对于有限次数的任务）
            if (task.maxExecuteCount > 0 && task.executeCount >= task.maxExecuteCount)
            {
                task.onComplete?.Invoke();
                tasksToRemove.Add(task.taskId);
                continue;
            }

            // 根据任务类型选择时间增量
            float timeIncrement = task.useRealTime ? realDeltaTime : deltaTime;
            task.elapsedTime += timeIncrement;

            // 检查是否到达间隔时间
            if (task.elapsedTime >= task.interval)
            {
                task.callback?.Invoke();
                task.executeCount++;
                task.elapsedTime = 0f; // 重置计时器

                // 如果是有限次数任务且达到最大次数，标记为待移除
                if (task.maxExecuteCount > 0 && task.executeCount >= task.maxExecuteCount)
                {
                    task.onComplete?.Invoke();
                    tasksToRemove.Add(task.taskId);
                }
            }
        }

        // 移除需要删除的任务
        foreach (var taskId in tasksToRemove)
        {
            intervalTasks.Remove(taskId);
        }
    }

    #endregion

    #region 定点回调实现

    /// <summary>
    /// 添加定点回调任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="hour">时</param>
    /// <param name="minute">分</param>
    /// <param name="second">秒</param>
    /// <param name="callback">回调函数</param>
    /// <param name="isDaily">是否每日重复</param>
    public void AddScheduledTask(string taskId, int hour, int minute, int second, Action callback, bool isDaily = true)
    {
        if (scheduledTasks.ContainsKey(taskId))
        {
            Debug.LogWarning($"Scheduled task with ID {taskId} already exists!");
            return;
        }

        var task = new ScheduledTask
        {
            taskId = taskId,
            targetHour = hour,
            targetMinute = minute,
            targetSecond = second,
            callback = callback,
            isDaily = isDaily,
            lastTriggerDate = DateTime.MinValue
        };
        scheduledTasks[taskId] = task;
    }

    /// <summary>
    /// 移除定点回调任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    public void RemoveScheduledTask(string taskId)
    {
        if (scheduledTasks.ContainsKey(taskId))
        {
            scheduledTasks[taskId].isActive = false;
            scheduledTasks.Remove(taskId);
        }
    }

    /// <summary>
    /// 更新定点任务
    /// </summary>
    private void UpdateScheduledTasks()
    {
        DateTime now = DateTime.Now;
        var tasksToRemove = new List<string>();

        foreach (var kvp in scheduledTasks)
        {
            var task = kvp.Value;
            if (!task.isActive)
            {
                tasksToRemove.Add(task.taskId);
                continue;
            }

            // 检查当前时间是否达到目标时间
            if (now.Hour == task.targetHour &&
                now.Minute == task.targetMinute &&
                now.Second == task.targetSecond)
            {
                // 防止同一秒内重复触发
                if (task.lastTriggerDate.Date != now.Date ||
                    (task.lastTriggerDate.Hour != now.Hour ||
                     task.lastTriggerDate.Minute != now.Minute ||
                     task.lastTriggerDate.Second != now.Second))
                {
                    task.callback?.Invoke();
                    task.lastTriggerDate = now;

                    // 如果不是每日任务，执行一次后移除
                    if (!task.isDaily)
                    {
                        tasksToRemove.Add(task.taskId);
                    }
                }
            }
        }

        // 移除需要删除的任务
        foreach (var taskId in tasksToRemove)
        {
            scheduledTasks.Remove(taskId);
        }
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 清理所有任务
    /// </summary>
    public void ClearAllTasks()
    {
        delayTasks.Clear();
        intervalTasks.Clear();
        scheduledTasks.Clear();
    }

    /// <summary>
    /// 获取活跃任务数量
    /// </summary>
    public void GetActiveTaskCount(out int delayCount, out int intervalCount, out int scheduledCount)
    {
        delayCount = delayTasks.Count;
        intervalCount = intervalTasks.Count;
        scheduledCount = scheduledTasks.Count;
    }

    #endregion
}