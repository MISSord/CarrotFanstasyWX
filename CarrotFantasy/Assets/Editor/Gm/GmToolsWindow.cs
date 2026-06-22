using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CarrotFantasy.EditorTools
{
    /// <summary>CarrotFantasy GM 工具：/gm 文本指令输入。</summary>
    public sealed class GmToolsWindow : EditorWindow
    {
        const string MenuPath = "Tools/CarrotFantasy/GM 工具";
        const string PrefsInputKey = "CarrotFantasy.GmTools.LastInput";
        const string PrefsUserIdKey = "CarrotFantasy.GmTools.UserId";
        const int MaxLogLines = 12;

        readonly GmCommandContext commandContext = new GmCommandContext();
        readonly List<string> logLines = new List<string>();

        string commandInput = "/gm changeData 0 1";
        Vector2 scroll;
        Vector2 logScroll;
        bool showHelp = true;
        bool showPreview;

        string previewSnapshot = string.Empty;

        struct GmShortcut
        {
            public string Label;
            public string Command;
            public bool RequirePlay;

            public GmShortcut(string label, string command, bool requirePlay = false)
            {
                this.Label = label;
                this.Command = command;
                this.RequirePlay = requirePlay;
            }
        }

        static readonly GmShortcut[] ProgressShortcuts =
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

        static readonly GmShortcut[] ArchiveShortcuts =
        {
            new GmShortcut("加载存档", "/gm load"),
            new GmShortcut("删除存档", "/gm delete"),
            new GmShortcut("预览快照", "/gm preview"),
            new GmShortcut("帮助", "/gm help"),
        };

        static readonly GmShortcut[] RuntimeShortcuts =
        {
            new GmShortcut("战斗 1-1", "/gm startBattle 1 1", true),
            new GmShortcut("战斗 2-3", "/gm startBattle 2 3", true),
            new GmShortcut("选关 1-1", "/gm openMap 1 1", true),
            new GmShortcut("选关 2-3", "/gm openMap 2 3", true),
        };

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<GmToolsWindow>("CarrotFantasy GM");
            window.minSize = new Vector2(480f, 420f);
        }

        void OnEnable()
        {
            commandInput = EditorPrefs.GetString(PrefsInputKey, "/gm changeData 0 1");
            commandContext.UserId = EditorPrefs.GetInt(PrefsUserIdKey, (int)StandaloneBackendMock.DefaultUserId);
            if (commandContext.UserId <= 0)
            {
                commandContext.UserId = StandaloneBackendMock.DefaultUserId;
            }

            RefreshPreview();
            AppendLog("就绪。输入 /gm help 查看指令。");
        }

        void OnDisable()
        {
            EditorPrefs.SetString(PrefsInputKey, commandInput);
            EditorPrefs.SetInt(PrefsUserIdKey, (int)commandContext.UserId);
        }

        void OnGUI()
        {
            DrawCommandInputBar();
            EditorGUILayout.Space(4f);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawStatusSection();
            EditorGUILayout.Space(6f);
            DrawShortcutSection();
            EditorGUILayout.Space(6f);
            DrawLogSection();
            EditorGUILayout.Space(6f);
            DrawHelpSection();
            EditorGUILayout.Space(6f);
            DrawPreviewSection();

            EditorGUILayout.EndScrollView();

            if (Event.current.type == EventType.KeyDown
                && Event.current.keyCode == KeyCode.Return
                && GUI.GetNameOfFocusedControl() == "GmCommandInput")
            {
                ExecuteCommand();
                Event.current.Use();
            }
        }

        void DrawStatusSection()
        {
            EditorGUILayout.LabelField("运行状态", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Play", Application.isPlaying ? "运行中" : "未运行", GUILayout.Width(160f));
            EditorGUILayout.LabelField("单机", StandaloneGameConfig.EnableStandaloneMode ? "开" : "关");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            long newUserId = EditorGUILayout.LongField("User Id", commandContext.UserId);
            if (newUserId != commandContext.UserId)
            {
                commandContext.UserId = newUserId > 0 ? newUserId : StandaloneBackendMock.DefaultUserId;
            }

            if (GUILayout.Button("同步当前账号", GUILayout.Width(100f)))
            {
                if (Application.isPlaying && AccountServer.Instance != null && AccountServer.Instance.userId > 0)
                {
                    commandContext.UserId = AccountServer.Instance.userId;
                    AppendLog("UserId 已同步为 " + commandContext.UserId);
                }
                else
                {
                    AppendLog("当前无运行中账号。");
                }
            }
            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying && AccountServer.Instance != null && AccountServer.Instance.userId > 0)
            {
                EditorGUILayout.LabelField("账号 UserId", AccountServer.Instance.userId.ToString());
            }
        }

        void DrawShortcutSection()
        {
            EditorGUILayout.LabelField("快捷填入", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "点击按钮填入命令；格式 /gm <指令> [参数...]，例: /gm changeData 0 1",
                MessageType.None);

            DrawShortcutGroup("关卡进度", ProgressShortcuts);
            DrawShortcutGroup("存档 / 其他", ArchiveShortcuts);
            DrawShortcutGroup("运行时（需 Play）", RuntimeShortcuts);
        }

        void DrawShortcutGroup(string title, GmShortcut[] shortcuts)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            const int columns = 4;
            int col = 0;
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < shortcuts.Length; i++)
            {
                GmShortcut item = shortcuts[i];
                using (new EditorGUI.DisabledScope(item.RequirePlay && !Application.isPlaying))
                {
                    if (GUILayout.Button(item.Label, GUILayout.MinHeight(22f)))
                    {
                        FillCommandInput(item.Command);
                    }
                }

                col++;
                if (col >= columns)
                {
                    col = 0;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);
        }

        void FillCommandInput(string command)
        {
            commandInput = command;
            GUI.FocusControl("GmCommandInput");
            AppendLog("已填入: " + command);
            Repaint();
        }

        void DrawCommandInputBar()
        {
            EditorGUILayout.LabelField("GM 指令", EditorStyles.boldLabel);

            GUI.SetNextControlName("GmCommandInput");
            commandInput = EditorGUILayout.TextField("输入", commandInput);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("执行", GUILayout.Height(28f)))
            {
                ExecuteCommand();
            }

            if (GUILayout.Button("清空日志", GUILayout.Width(80f), GUILayout.Height(28f)))
            {
                logLines.Clear();
            }

            if (GUILayout.Button("刷新预览", GUILayout.Width(80f), GUILayout.Height(28f)))
            {
                RefreshPreview();
                AppendLog("快照预览已刷新。");
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawLogSection()
        {
            EditorGUILayout.LabelField("执行日志", EditorStyles.boldLabel);
            logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.MinHeight(100f), GUILayout.MaxHeight(160f));
            if (logLines.Count == 0)
            {
                EditorGUILayout.LabelField("(无)", EditorStyles.miniLabel);
            }
            else
            {
                for (int i = 0; i < logLines.Count; i++)
                {
                    EditorGUILayout.LabelField(logLines[i], EditorStyles.wordWrappedLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawHelpSection()
        {
            showHelp = EditorGUILayout.Foldout(showHelp, "指令帮助", true);
            if (!showHelp)
            {
                return;
            }

            EditorGUILayout.TextArea(GmCommandDispatcher.GetHelpText(), GUILayout.MinHeight(140f));
        }

        void DrawPreviewSection()
        {
            showPreview = EditorGUILayout.Foldout(showPreview, "地图快照预览", true);
            if (!showPreview)
            {
                return;
            }

            EditorGUILayout.TextArea(previewSnapshot, GUILayout.MinHeight(64f));
        }

        void ExecuteCommand()
        {
            GmCommandResult result = GmCommandDispatcher.Execute(commandInput, commandContext);
            string prefix = result.Success ? "[OK] " : "[FAIL] ";
            AppendLog(prefix + result.Message);
            Debug.Log("[GM] " + prefix + result.Message);

            if (result.Success)
            {
                RefreshPreview();
            }

            Repaint();
        }

        void AppendLog(string line)
        {
            logLines.Add(line);
            while (logLines.Count > MaxLogLines)
            {
                logLines.RemoveAt(0);
            }
        }

        void RefreshPreview()
        {
            previewSnapshot = GmMapProgressService.GetLiveSnapshotOrPersisted(commandContext.UserId);
        }
    }
}
