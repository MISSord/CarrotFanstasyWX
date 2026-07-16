using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// PC 运行时 Log 面板（IMGUI），布局贴近 Unity Editor Console：
    /// 顶部 Log/Warn/Error 筛选，上半列表，下半选中项详情（含堆栈）。
    /// </summary>
    public sealed class RuntimeLogConsole : MonoBehaviour
    {
        private const int MaxEntries = 800;
        private const float OpenButtonWidth = 72f;
        private const float OpenButtonHeight = 36f;
        private const float RowHeight = 22f;
        private const float SplitterHeight = 6f;
        private const float MinPaneHeight = 80f;

        private struct LogEntry
        {
            public string Message;
            public string StackTrace;
            public LogType Type;
            public string TimeText;
        }

        private static RuntimeLogConsole instance;

        private readonly List<LogEntry> entries = new List<LogEntry>(256);
        private readonly object entriesLock = new object();
        private readonly List<int> filteredIndices = new List<int>(256);
        private readonly StringBuilder lineBuilder = new StringBuilder(256);

        private bool panelOpen;
        private Vector2 listScroll;
        private Vector2 detailScroll;
        private bool autoScroll = true;
        private int selectedAbsoluteIndex = -1;

        private bool showLog = true;
        private bool showWarning = true;
        private bool showError = true;

        /// <summary>上半列表高度占比（相对可用内容区）。</summary>
        private float listPaneRatio = 0.58f;
        private bool draggingSplitter;

        private GUIStyle openButtonStyle;
        private GUIStyle toolbarButtonStyle;
        private GUIStyle filterOnStyle;
        private GUIStyle filterOffStyle;
        private GUIStyle rowLabelStyle;
        private GUIStyle detailLabelStyle;
        private GUIStyle panelBoxStyle;
        private GUIStyle paneBoxStyle;
        private GUIStyle selectedRowStyle;
        private bool stylesReady;

        private Texture2D panelBg;
        private Texture2D paneBg;
        private Texture2D selectedBg;
        private Texture2D filterOnBg;
        private Texture2D filterOffBg;

#if CF_DEV_TOOLS || UNITY_EDITOR
        public static void EnsureInstalled()
        {
            if (instance != null)
            {
                return;
            }

            GameObject go = new GameObject("RuntimeLogConsole");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RuntimeLogConsole>();
        }
#else
        public static void EnsureInstalled()
        {
        }
#endif

        private void OnEnable()
        {
            Application.logMessageReceived += this.HandleLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= this.HandleLog;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            DestroyTexture(ref this.panelBg);
            DestroyTexture(ref this.paneBg);
            DestroyTexture(ref this.selectedBg);
            DestroyTexture(ref this.filterOnBg);
            DestroyTexture(ref this.filterOffBg);
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            LogEntry entry = new LogEntry
            {
                Message = condition ?? string.Empty,
                StackTrace = stackTrace ?? string.Empty,
                Type = type,
                TimeText = DateTime.Now.ToString("HH:mm:ss.fff"),
            };

            lock (this.entriesLock)
            {
                this.entries.Add(entry);
                while (this.entries.Count > MaxEntries)
                {
                    this.entries.RemoveAt(0);
                    if (this.selectedAbsoluteIndex >= 0)
                    {
                        this.selectedAbsoluteIndex--;
                    }
                }
            }

            if (this.autoScroll)
            {
                this.listScroll.y = float.MaxValue;
            }
        }

        private void OnGUI()
        {
#if !CF_DEV_TOOLS && !UNITY_EDITOR
            return;
#else
            this.EnsureStyles();
            GUI.depth = -1000;

            if (this.panelOpen)
            {
                this.DrawPanel();
            }
            else
            {
                this.DrawOpenButton();
            }
#endif
        }

        private void DrawOpenButton()
        {
            float x = Screen.width - OpenButtonWidth - 12f;
            float y = Screen.height - OpenButtonHeight - 12f;
            if (GUI.Button(new Rect(x, y, OpenButtonWidth, OpenButtonHeight), "Log", this.openButtonStyle))
            {
                this.panelOpen = true;
                if (this.autoScroll)
                {
                    this.listScroll.y = float.MaxValue;
                }
            }
        }

        private void DrawPanel()
        {
            float margin = 20f;
            float panelWidth = Mathf.Min(Screen.width - margin * 2f, 1100f);
            float panelHeight = Mathf.Min(Screen.height - margin * 2f, 720f);
            float x = (Screen.width - panelWidth) * 0.5f;
            float y = (Screen.height - panelHeight) * 0.5f;
            float pad = 10f;

            GUI.Box(new Rect(x, y, panelWidth, panelHeight), GUIContent.none, this.panelBoxStyle);

            LogEntry[] snapshot;
            lock (this.entriesLock)
            {
                snapshot = this.entries.ToArray();
            }

            this.RebuildFilteredIndices(snapshot);
            this.CountByType(snapshot, out int logCount, out int warnCount, out int errorCount);

            float toolbarY = y + 8f;
            float toolbarH = 32f;
            this.DrawToolbar(x, toolbarY, panelWidth, toolbarH, pad, logCount, warnCount, errorCount);
            if (!this.panelOpen)
            {
                return;
            }

            float contentTop = toolbarY + toolbarH + 8f;
            float contentBottom = y + panelHeight - pad;
            float contentHeight = contentBottom - contentTop;
            float contentLeft = x + pad;
            float contentWidth = panelWidth - pad * 2f;

            float listHeight = Mathf.Clamp(
                contentHeight * this.listPaneRatio,
                MinPaneHeight,
                contentHeight - MinPaneHeight - SplitterHeight);
            float detailHeight = contentHeight - listHeight - SplitterHeight;

            Rect listRect = new Rect(contentLeft, contentTop, contentWidth, listHeight);
            Rect splitterRect = new Rect(contentLeft, contentTop + listHeight, contentWidth, SplitterHeight);
            Rect detailRect = new Rect(contentLeft, contentTop + listHeight + SplitterHeight, contentWidth, detailHeight);

            this.DrawListPane(listRect, snapshot);
            this.DrawSplitter(splitterRect, contentTop, contentHeight);
            this.DrawDetailPane(detailRect, snapshot);
        }

        private void DrawToolbar(
            float x,
            float toolbarY,
            float panelWidth,
            float toolbarH,
            float pad,
            int logCount,
            int warnCount,
            int errorCount)
        {
            float left = x + pad;
            float filterW = 92f;

            this.showLog = this.DrawFilterToggle(
                new Rect(left, toolbarY, filterW, toolbarH),
                $"Log {logCount}",
                this.showLog,
                new Color(0.75f, 0.75f, 0.78f));
            left += filterW + 6f;

            this.showWarning = this.DrawFilterToggle(
                new Rect(left, toolbarY, filterW, toolbarH),
                $"Warn {warnCount}",
                this.showWarning,
                new Color(1f, 0.85f, 0.35f));
            left += filterW + 6f;

            this.showError = this.DrawFilterToggle(
                new Rect(left, toolbarY, filterW + 8f, toolbarH),
                $"Error {errorCount}",
                this.showError,
                new Color(1f, 0.55f, 0.55f));
            left += filterW + 16f;

            float actionW = 72f;
            string autoLabel = this.autoScroll ? "滚动:开" : "滚动:关";
            if (GUI.Button(new Rect(left, toolbarY, actionW + 8f, toolbarH), autoLabel, this.toolbarButtonStyle))
            {
                this.autoScroll = !this.autoScroll;
            }

            float right = x + panelWidth - pad;
            float btnW = 72f;
            if (GUI.Button(new Rect(right - btnW, toolbarY, btnW, toolbarH), "关闭", this.toolbarButtonStyle))
            {
                this.panelOpen = false;
                return;
            }

            right -= btnW + 8f;
            if (GUI.Button(new Rect(right - btnW, toolbarY, btnW, toolbarH), "清空", this.toolbarButtonStyle))
            {
                lock (this.entriesLock)
                {
                    this.entries.Clear();
                }

                this.selectedAbsoluteIndex = -1;
                this.filteredIndices.Clear();
            }
        }

        private bool DrawFilterToggle(Rect rect, string label, bool isOn, Color onTextColor)
        {
            GUIStyle style = isOn ? this.filterOnStyle : this.filterOffStyle;
            Color old = style.normal.textColor;
            if (isOn)
            {
                style.normal.textColor = onTextColor;
            }

            bool clicked = GUI.Button(rect, label, style);
            style.normal.textColor = old;

            if (clicked)
            {
                bool next = !isOn;
                if (!next && this.CountActiveFilters() <= 1)
                {
                    return true;
                }

                return next;
            }

            return isOn;
        }

        private int CountActiveFilters()
        {
            int n = 0;
            if (this.showLog)
            {
                n++;
            }

            if (this.showWarning)
            {
                n++;
            }

            if (this.showError)
            {
                n++;
            }

            return n;
        }

        private void DrawListPane(Rect listRect, LogEntry[] snapshot)
        {
            GUI.Box(listRect, GUIContent.none, this.paneBoxStyle);

            float contentWidth = listRect.width - 18f;
            float contentHeight = Mathf.Max(this.filteredIndices.Count * RowHeight, listRect.height);

            this.listScroll = GUI.BeginScrollView(
                listRect,
                this.listScroll,
                new Rect(0f, 0f, contentWidth, contentHeight));

            for (int i = 0; i < this.filteredIndices.Count; i++)
            {
                int absIndex = this.filteredIndices[i];
                if (absIndex < 0 || absIndex >= snapshot.Length)
                {
                    continue;
                }

                LogEntry entry = snapshot[absIndex];
                Rect rowRect = new Rect(0f, i * RowHeight, contentWidth, RowHeight);
                bool selected = absIndex == this.selectedAbsoluteIndex;

                if (selected)
                {
                    GUI.Box(rowRect, GUIContent.none, this.selectedRowStyle);
                }

                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                {
                    this.selectedAbsoluteIndex = absIndex;
                    this.detailScroll = Vector2.zero;
                }

                this.lineBuilder.Length = 0;
                this.lineBuilder.Append(GetTypePrefix(entry.Type))
                    .Append(' ')
                    .Append(entry.TimeText)
                    .Append("  ")
                    .Append(CollapseFirstLine(entry.Message));

                this.rowLabelStyle.normal.textColor = this.GetTypeTextColor(entry.Type);
                GUI.Label(
                    new Rect(rowRect.x + 6f, rowRect.y + 1f, rowRect.width - 10f, rowRect.height - 2f),
                    this.lineBuilder.ToString(),
                    this.rowLabelStyle);
            }

            GUI.EndScrollView();

            if (this.autoScroll && Event.current.type == EventType.Repaint)
            {
                this.listScroll.y = float.MaxValue;
            }
        }

        private void DrawSplitter(Rect splitterRect, float contentTop, float contentHeight)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.35f, 0.35f, 0.4f, 1f);
            GUI.Box(splitterRect, GUIContent.none);
            GUI.color = old;

            Event e = Event.current;
            if (e.type == EventType.MouseDown && splitterRect.Contains(e.mousePosition))
            {
                this.draggingSplitter = true;
                e.Use();
            }

            if (this.draggingSplitter && (e.type == EventType.MouseDrag || e.type == EventType.MouseMove))
            {
                float newListH = Mathf.Clamp(
                    e.mousePosition.y - contentTop,
                    MinPaneHeight,
                    contentHeight - MinPaneHeight - SplitterHeight);
                this.listPaneRatio = newListH / contentHeight;
                e.Use();
            }

            if (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp)
            {
                this.draggingSplitter = false;
            }
        }

        private void DrawDetailPane(Rect detailRect, LogEntry[] snapshot)
        {
            GUI.Box(detailRect, GUIContent.none, this.paneBoxStyle);

            if (this.selectedAbsoluteIndex < 0 || this.selectedAbsoluteIndex >= snapshot.Length)
            {
                GUI.Label(
                    new Rect(detailRect.x + 8f, detailRect.y + 8f, detailRect.width - 16f, 24f),
                    "选中上方一条日志以查看详情与堆栈",
                    this.detailLabelStyle);
                return;
            }

            LogEntry entry = snapshot[this.selectedAbsoluteIndex];
            this.lineBuilder.Length = 0;
            this.lineBuilder.Append('[').Append(entry.TimeText).Append("][")
                .Append(entry.Type).Append("]\n")
                .Append(entry.Message);

            if (!string.IsNullOrEmpty(entry.StackTrace))
            {
                this.lineBuilder.Append("\n\n").Append(entry.StackTrace);
            }

            string detailText = this.lineBuilder.ToString();
            float textWidth = detailRect.width - 24f;
            float textHeight = this.detailLabelStyle.CalcHeight(new GUIContent(detailText), textWidth);
            textHeight = Mathf.Max(textHeight, detailRect.height);

            this.detailScroll = GUI.BeginScrollView(
                detailRect,
                this.detailScroll,
                new Rect(0f, 0f, textWidth, textHeight));

            this.detailLabelStyle.normal.textColor = this.GetTypeTextColor(entry.Type);
            GUI.Label(new Rect(8f, 6f, textWidth - 8f, textHeight), detailText, this.detailLabelStyle);

            GUI.EndScrollView();
        }

        private void RebuildFilteredIndices(LogEntry[] snapshot)
        {
            this.filteredIndices.Clear();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (this.PassFilter(snapshot[i].Type))
                {
                    this.filteredIndices.Add(i);
                }
            }

            if (this.selectedAbsoluteIndex >= 0
                && this.selectedAbsoluteIndex < snapshot.Length
                && !this.PassFilter(snapshot[this.selectedAbsoluteIndex].Type))
            {
                // 选中项被筛掉时保留详情仍可看；列表不高亮即可
            }
        }

        private bool PassFilter(LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    return this.showError;
                case LogType.Warning:
                    return this.showWarning;
                default:
                    return this.showLog;
            }
        }

        private void CountByType(LogEntry[] snapshot, out int logCount, out int warnCount, out int errorCount)
        {
            logCount = 0;
            warnCount = 0;
            errorCount = 0;
            for (int i = 0; i < snapshot.Length; i++)
            {
                switch (snapshot[i].Type)
                {
                    case LogType.Error:
                    case LogType.Exception:
                    case LogType.Assert:
                        errorCount++;
                        break;
                    case LogType.Warning:
                        warnCount++;
                        break;
                    default:
                        logCount++;
                        break;
                }
            }
        }

        private static string GetTypePrefix(LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    return "[E]";
                case LogType.Warning:
                    return "[W]";
                default:
                    return "[L]";
            }
        }

        private static string CollapseFirstLine(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            int idx = message.IndexOf('\n');
            return idx >= 0 ? message.Substring(0, idx) : message;
        }

        private Color GetTypeTextColor(LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    return new Color(1f, 0.55f, 0.55f);
                case LogType.Warning:
                    return new Color(1f, 0.85f, 0.35f);
                default:
                    return new Color(0.9f, 0.9f, 0.92f);
            }
        }

        private void EnsureStyles()
        {
            if (this.stylesReady)
            {
                return;
            }

            this.panelBg = MakeColorTexture(new Color(0.12f, 0.12f, 0.14f, 0.96f));
            this.paneBg = MakeColorTexture(new Color(0.16f, 0.16f, 0.18f, 1f));
            this.selectedBg = MakeColorTexture(new Color(0.24f, 0.36f, 0.55f, 1f));
            this.filterOnBg = MakeColorTexture(new Color(0.28f, 0.28f, 0.32f, 1f));
            this.filterOffBg = MakeColorTexture(new Color(0.18f, 0.18f, 0.2f, 1f));

            this.panelBoxStyle = new GUIStyle(GUI.skin.box);
            this.panelBoxStyle.normal.background = this.panelBg;

            this.paneBoxStyle = new GUIStyle(GUI.skin.box);
            this.paneBoxStyle.normal.background = this.paneBg;

            this.selectedRowStyle = new GUIStyle(GUI.skin.box);
            this.selectedRowStyle.normal.background = this.selectedBg;

            this.openButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
            };

            this.toolbarButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
            };

            this.filterOnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
            };
            this.filterOnStyle.normal.background = this.filterOnBg;
            this.filterOnStyle.normal.textColor = Color.white;

            this.filterOffStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
            };
            this.filterOffStyle.normal.background = this.filterOffBg;
            this.filterOffStyle.normal.textColor = new Color(0.55f, 0.55f, 0.58f);

            this.rowLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                wordWrap = false,
                richText = false,
            };

            this.detailLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                richText = false,
            };

            this.stylesReady = true;
        }

        private static Texture2D MakeColorTexture(Color color)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private static void DestroyTexture(ref Texture2D tex)
        {
            if (tex == null)
            {
                return;
            }

            Destroy(tex);
            tex = null;
        }
    }
}
