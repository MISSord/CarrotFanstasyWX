using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// PC 运行时 GM 面板（IMGUI），复用 <see cref="GmCommandDispatcher"/>，交互对齐 Editor GM 工具。
    /// 依赖热更程序集，由 <see cref="HotUpdateEntry"/> 安装。
    /// </summary>
    public sealed class RuntimeGmConsole : MonoBehaviour
    {
        private const string PrefsInputKey = "CarrotFantasy.RuntimeGm.LastInput";
        private const string PrefsUserIdKey = "CarrotFantasy.RuntimeGm.UserId";
        private const int MaxLogLines = 40;
        private const float OpenButtonWidth = 72f;
        private const float OpenButtonHeight = 36f;

        private struct GmShortcut
        {
            public string Label;
            public string Command;

            public GmShortcut(string label, string command)
            {
                this.Label = label;
                this.Command = command;
            }
        }

        private static readonly GmShortcut[] ProgressShortcuts =
        {
            new GmShortcut("解锁 1-1", "/gm changeData 0 1"),
            new GmShortcut("解锁 1-5", "/gm changeData 0 5"),
            new GmShortcut("解锁 2-1", "/gm changeData 1 1"),
            new GmShortcut("解锁 2-5", "/gm changeData 1 5"),
            new GmShortcut("解锁 3-1", "/gm changeData 2 1"),
            new GmShortcut("解锁 3-5", "/gm changeData 2 5"),
            new GmShortcut("解锁全部", "/gm unlockAll"),
            new GmShortcut("重置初始档", "/gm reset"),
        };

        private static readonly GmShortcut[] ArchiveShortcuts =
        {
            new GmShortcut("加载存档", "/gm load"),
            new GmShortcut("删除存档", "/gm delete"),
            new GmShortcut("预览快照", "/gm preview"),
            new GmShortcut("帮助", "/gm help"),
        };

        private static readonly GmShortcut[] RuntimeShortcuts =
        {
            new GmShortcut("战斗 1-1", "/gm startBattle 1 1"),
            new GmShortcut("战斗 2-3", "/gm startBattle 2 3"),
            new GmShortcut("选关 1-1", "/gm openMap 1 1"),
            new GmShortcut("选关 2-3", "/gm openMap 2 3"),
        };

        private static RuntimeGmConsole instance;

        private readonly GmCommandContext commandContext = new GmCommandContext();
        private readonly List<string> logLines = new List<string>();

        private bool panelOpen;
        private string commandInput = "/gm changeData 0 1";
        private string userIdInput = "1";
        private Vector2 panelScroll;
        private Vector2 logScroll;
        private bool showHelp;

        private GUIStyle openButtonStyle;
        private GUIStyle toolbarButtonStyle;
        private GUIStyle panelBoxStyle;
        private GUIStyle textFieldStyle;
        private GUIStyle logLabelStyle;
        private bool stylesReady;
        private Texture2D panelBg;

#if UNITY_STANDALONE || UNITY_EDITOR
        public static void EnsureInstalled()
        {
            if (instance != null)
            {
                return;
            }

            GameObject go = new GameObject("RuntimeGmConsole");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RuntimeGmConsole>();
        }
#else
        public static void EnsureInstalled()
        {
        }
#endif

        private void Awake()
        {
            this.commandInput = PlayerPrefs.GetString(PrefsInputKey, "/gm changeData 0 1");
            long userId = PlayerPrefs.GetInt(PrefsUserIdKey, (int)StandaloneBackendMock.DefaultUserId);
            if (userId <= 0)
            {
                userId = StandaloneBackendMock.DefaultUserId;
            }

            this.commandContext.UserId = userId;
            this.userIdInput = userId.ToString();
            this.AppendLog("GM 就绪。输入 /gm help 查看指令。");
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            PlayerPrefs.SetString(PrefsInputKey, this.commandInput ?? string.Empty);
            PlayerPrefs.SetInt(PrefsUserIdKey, (int)this.commandContext.UserId);
            PlayerPrefs.Save();

            if (this.panelBg != null)
            {
                Destroy(this.panelBg);
                this.panelBg = null;
            }
        }

        private void OnGUI()
        {
#if !UNITY_STANDALONE && !UNITY_EDITOR
            return;
#else
            this.EnsureStyles();
            GUI.depth = -999;

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
            // 左下角，避免与右下角 Log 按钮重叠
            float x = 12f;
            float y = Screen.height - OpenButtonHeight - 12f;
            if (GUI.Button(new Rect(x, y, OpenButtonWidth, OpenButtonHeight), "GM", this.openButtonStyle))
            {
                this.panelOpen = true;
                this.TrySyncAccountUserId(silent: true);
            }
        }

        private void DrawPanel()
        {
            float margin = 20f;
            float panelWidth = Mathf.Min(Screen.width - margin * 2f, 720f);
            float panelHeight = Mathf.Min(Screen.height - margin * 2f, 640f);
            float x = (Screen.width - panelWidth) * 0.5f;
            float y = (Screen.height - panelHeight) * 0.5f;
            float pad = 10f;

            GUI.Box(new Rect(x, y, panelWidth, panelHeight), GUIContent.none, this.panelBoxStyle);

            Rect contentRect = new Rect(x + pad, y + pad, panelWidth - pad * 2f, panelHeight - pad * 2f);
            GUILayout.BeginArea(contentRect);
            this.panelScroll = GUILayout.BeginScrollView(this.panelScroll);

            GUILayout.BeginHorizontal();
            GUILayout.Label("运行时 GM", this.toolbarButtonStyle, GUILayout.Height(28f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("关闭", this.toolbarButtonStyle, GUILayout.Width(72f), GUILayout.Height(28f)))
            {
                this.panelOpen = false;
                GUILayout.EndHorizontal();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            this.DrawUserIdRow();
            GUILayout.Space(6f);
            this.DrawCommandRow();
            GUILayout.Space(8f);

            this.DrawShortcutGroup("关卡进度", ProgressShortcuts);
            this.DrawShortcutGroup("存档 / 其他", ArchiveShortcuts);
            this.DrawShortcutGroup("运行时跳转", RuntimeShortcuts);
            GUILayout.Space(6f);

            this.DrawLogSection();
            GUILayout.Space(6f);

            this.showHelp = GUILayout.Toggle(this.showHelp, "显示指令帮助");
            if (this.showHelp)
            {
                GUILayout.TextArea(GmCommandDispatcher.GetHelpText(), GUILayout.MinHeight(120f));
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawUserIdRow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("UserId", GUILayout.Width(56f));
            this.userIdInput = GUILayout.TextField(this.userIdInput, this.textFieldStyle, GUILayout.Width(100f));
            if (GUILayout.Button("应用", this.toolbarButtonStyle, GUILayout.Width(56f)))
            {
                if (long.TryParse(this.userIdInput, out long userId) && userId > 0)
                {
                    this.commandContext.UserId = userId;
                    this.AppendLog("已设置 UserId=" + userId);
                }
                else
                {
                    this.AppendLog("[FAIL] UserId 无效");
                }
            }

            if (GUILayout.Button("同步账号", this.toolbarButtonStyle, GUILayout.Width(80f)))
            {
                this.TrySyncAccountUserId(silent: false);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawCommandRow()
        {
            GUILayout.Label("GM 指令");
            GUILayout.BeginHorizontal();
            this.commandInput = GUILayout.TextField(this.commandInput ?? string.Empty, this.textFieldStyle, GUILayout.MinHeight(26f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("执行", this.toolbarButtonStyle, GUILayout.Height(30f)))
            {
                this.ExecuteCommand();
            }

            if (GUILayout.Button("清空缓存", this.toolbarButtonStyle, GUILayout.Width(90f), GUILayout.Height(30f)))
            {
                AssetBundleCacheCleaner.ClearAll();
                this.AppendLog("已清空本地下载缓存。");
                Debug.Log("[GM] 已清空本地下载缓存");
            }

            if (GUILayout.Button("清空日志", this.toolbarButtonStyle, GUILayout.Width(90f), GUILayout.Height(30f)))
            {
                this.logLines.Clear();
            }

            GUILayout.EndHorizontal();

            Event e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                this.ExecuteCommand();
                e.Use();
            }
        }

        private void DrawShortcutGroup(string title, GmShortcut[] shortcuts)
        {
            GUILayout.Label(title);
            const int columns = 4;
            int col = 0;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < shortcuts.Length; i++)
            {
                GmShortcut item = shortcuts[i];
                if (GUILayout.Button(item.Label, this.toolbarButtonStyle, GUILayout.MinHeight(26f)))
                {
                    this.commandInput = item.Command;
                    this.AppendLog("已填入: " + item.Command);
                }

                col++;
                if (col >= columns && i < shortcuts.Length - 1)
                {
                    col = 0;
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }

        private void DrawLogSection()
        {
            GUILayout.Label("执行日志");
            this.logScroll = GUILayout.BeginScrollView(this.logScroll, GUILayout.MinHeight(110f), GUILayout.MaxHeight(160f));
            if (this.logLines.Count == 0)
            {
                GUILayout.Label("(无)", this.logLabelStyle);
            }
            else
            {
                for (int i = 0; i < this.logLines.Count; i++)
                {
                    GUILayout.Label(this.logLines[i], this.logLabelStyle);
                }
            }

            GUILayout.EndScrollView();
        }

        private void ExecuteCommand()
        {
            if (long.TryParse(this.userIdInput, out long userId) && userId > 0)
            {
                this.commandContext.UserId = userId;
            }

            GmCommandResult result = GmCommandDispatcher.Execute(this.commandInput, this.commandContext);
            string prefix = result.Success ? "[OK] " : "[FAIL] ";
            this.AppendLog(prefix + result.Message);
            Debug.Log("[GM] " + prefix + result.Message);

            PlayerPrefs.SetString(PrefsInputKey, this.commandInput ?? string.Empty);
            PlayerPrefs.SetInt(PrefsUserIdKey, (int)this.commandContext.UserId);
            PlayerPrefs.Save();
        }

        private void TrySyncAccountUserId(bool silent)
        {
            if (AccountServer.Instance != null && AccountServer.Instance.userId > 0)
            {
                this.commandContext.UserId = AccountServer.Instance.userId;
                this.userIdInput = this.commandContext.UserId.ToString();
                if (!silent)
                {
                    this.AppendLog("UserId 已同步为 " + this.commandContext.UserId);
                }

                return;
            }

            if (!silent)
            {
                this.AppendLog("当前无运行中账号。");
            }
        }

        private void AppendLog(string line)
        {
            this.logLines.Add(line ?? string.Empty);
            while (this.logLines.Count > MaxLogLines)
            {
                this.logLines.RemoveAt(0);
            }

            this.logScroll.y = float.MaxValue;
        }

        private void EnsureStyles()
        {
            if (this.stylesReady)
            {
                return;
            }

            this.panelBg = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            this.panelBg.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.12f, 0.96f));
            this.panelBg.Apply();

            this.panelBoxStyle = new GUIStyle(GUI.skin.box);
            this.panelBoxStyle.normal.background = this.panelBg;

            this.openButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
            };

            this.toolbarButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
            };

            this.textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 14,
            };

            this.logLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
            };

            this.stylesReady = true;
        }
    }
}
