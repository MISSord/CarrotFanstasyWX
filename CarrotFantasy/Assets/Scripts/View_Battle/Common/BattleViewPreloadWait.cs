using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 战斗视图预加载批次等待：纯异步计数 + 超时汇总失败项，不干预 <see cref="AssetBundleManager"/>。
    /// </summary>
    public sealed class BattleViewPreloadWait
    {
        public const float DefaultTimeoutSeconds = 15f;

        readonly string tag;
        readonly float timeoutSeconds;
        readonly Action<bool> onComplete;
        readonly HashSet<string> pendingKeys = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> failedKeys = new List<string>();
        string timeoutTaskId;
        bool completed;

        public IReadOnlyList<string> FailedKeys
        {
            get { return this.failedKeys; }
        }

        public BattleViewPreloadWait(string batchTag, float waitTimeoutSeconds, Action<bool> completeCallback)
        {
            this.tag = batchTag;
            this.timeoutSeconds = waitTimeoutSeconds > 0f ? waitTimeoutSeconds : DefaultTimeoutSeconds;
            this.onComplete = completeCallback;
        }

        public static string MakeKey(string bundleName, string assetName)
        {
            return bundleName + "|" + assetName;
        }

        public void Track(string bundleName, string assetName)
        {
            if (this.completed)
            {
                return;
            }

            this.pendingKeys.Add(MakeKey(bundleName, assetName));
        }

        public void NotifyFinished(string bundleName, string assetName, bool success)
        {
            if (this.completed)
            {
                return;
            }

            string key = MakeKey(bundleName, assetName);
            if (!this.pendingKeys.Remove(key))
            {
                return;
            }

            if (!success)
            {
                this.failedKeys.Add(key);
            }

            if (this.pendingKeys.Count <= 0)
            {
                this.TryComplete(false);
            }
        }

        public void Start()
        {
            if (this.completed)
            {
                return;
            }

            if (this.pendingKeys.Count <= 0)
            {
                Debug.LogWarning("[" + this.tag + "] 预加载批次无 pending 项，立即完成。");
                this.TryComplete(false);
                return;
            }

            this.timeoutTaskId = "BattleViewPreloadWait_" + this.tag + "_" + Guid.NewGuid().ToString("N");
            TimeUtility.Instance.SetTimeout(
                this.timeoutSeconds,
                this.OnTimeout,
                useRealTime: true,
                this.timeoutTaskId);
        }

        void OnTimeout()
        {
            if (this.completed)
            {
                return;
            }

            foreach (string key in this.pendingKeys)
            {
                this.failedKeys.Add(key + " (timeout)");
            }

            this.pendingKeys.Clear();
            this.LogFailures(true);
            this.TryComplete(true);
        }

        void TryComplete(bool fromTimeout)
        {
            if (this.completed)
            {
                return;
            }

            this.completed = true;
            if (!string.IsNullOrEmpty(this.timeoutTaskId))
            {
                TimeUtility.Instance.RemoveTimeout(this.timeoutTaskId);
                this.timeoutTaskId = null;
            }

            if (!fromTimeout)
            {
                this.LogFailures(false);
            }

            this.onComplete?.Invoke(this.failedKeys.Count <= 0);
        }

        void LogFailures(bool fromTimeout)
        {
            if (this.failedKeys.Count <= 0)
            {
                return;
            }

            var sb = new StringBuilder(256);
            sb.Append("[");
            sb.Append(this.tag);
            sb.Append("] 预加载");
            sb.Append(fromTimeout ? "超时" : "完成");
            sb.Append(" (");
            sb.Append(this.timeoutSeconds);
            sb.Append("s)，以下资源未就绪 (bundle|asset):\n");
            for (int i = 0; i < this.failedKeys.Count; i++)
            {
                sb.Append("  - ");
                sb.Append(this.failedKeys[i]);
                sb.Append('\n');
            }

            Debug.LogError(sb.ToString());
        }
    }
}
