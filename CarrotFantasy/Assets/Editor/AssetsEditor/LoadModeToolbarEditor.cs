#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 游戏加载模式枚举
/// </summary>
public enum GameLoadMode
{
    Development = 0,    // 开发模式
    Production = 1,     // 生产模式
    Testing = 2,        // 测试模式
    Demo = 3,           // 演示模式
    DebugMode = 4       // 调试模式
}

/// <summary>
/// 加载模式快捷工具栏编辑器窗口
/// </summary>
public class LoadModeToolbarEditor : EditorWindow
{
    private const string PREFS_KEY = "GameLoadMode";
    private GameLoadMode currentMode;
    private Vector2 scrollPosition;

    [MenuItem("Tools/游戏加载模式设置")]
    public static void ShowWindow()
    {
        LoadModeToolbarEditor window = GetWindow<LoadModeToolbarEditor>("加载模式");
        window.minSize = new Vector2(300, 250);
        window.Show();
    }

    private void OnEnable()
    {
        // 加载保存的模式
        currentMode = (GameLoadMode)EditorPrefs.GetInt(PREFS_KEY, 0);
    }

    private void OnGUI()
    {
        DrawHeader();
        DrawModeSelection();
        DrawCurrentModeInfo();
        //DrawQuickActions();
    }

    /// <summary>
    /// 绘制窗口头部
    /// </summary>
    private void DrawHeader()
    {
        GUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUIStyle titleStyle = new GUIStyle(EditorStyles.largeLabel)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        EditorGUILayout.LabelField("🎮 游戏加载模式设置", titleStyle, GUILayout.Width(200));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }

    /// <summary>
    /// 绘制模式选择区域
    /// </summary>
    private void DrawModeSelection()
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("选择加载模式:", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // 创建模式选择按钮
        DrawModeButton(GameLoadMode.Development, "🛠️ 开发模式", "启用所有调试工具和日志");
        DrawModeButton(GameLoadMode.Production, "🚀 生产模式", "优化性能，适合发布版本");
        DrawModeButton(GameLoadMode.Testing, "🧪 测试模式", "专门的测试环境配置，测试本地AB包");
        DrawModeButton(GameLoadMode.Demo, "🎯 演示模式", "平衡性能和功能的演示版本");
        DrawModeButton(GameLoadMode.DebugMode, "🐛 调试模式", "详细的调试信息和日志");

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制单个模式按钮
    /// </summary>
    private void DrawModeButton(GameLoadMode mode, string label, string description)
    {
        bool isSelected = currentMode == mode;

        EditorGUILayout.BeginHorizontal();

        // 选择按钮
        if (GUILayout.Toggle(isSelected, label, "Button", GUILayout.Height(30)))
        {
            if (!isSelected)
            {
                SetLoadMode(mode);
            }
        }

        // 模式描述
        EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel, GUILayout.Width(200));

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(2);
    }

    /// <summary>
    /// 绘制当前模式信息
    /// </summary>
    private void DrawCurrentModeInfo()
    {
        GUILayout.Space(10);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("当前模式信息:", EditorStyles.boldLabel);
        GUILayout.Space(5);

        string modeColor = currentMode switch
        {
            GameLoadMode.Development => "00FF00",
            GameLoadMode.Production => "FF0000",
            GameLoadMode.Testing => "FFFF00",
            GameLoadMode.Demo => "00FFFF",
            GameLoadMode.DebugMode => "FF00FF",
            _ => "FFFFFF"
        };

        EditorGUILayout.LabelField($"模式: <color=#{modeColor}>{currentMode}</color>", new GUIStyle(EditorStyles.label) { richText = true });
        EditorGUILayout.LabelField($"数值: {(int)currentMode}");

        string description = currentMode switch
        {
            GameLoadMode.Development => "开发环境，启用所有调试功能",
            GameLoadMode.Production => "生产环境，优化性能禁用调试",
            GameLoadMode.Testing => "测试环境，专门的测试配置",
            GameLoadMode.Demo => "演示环境，平衡性能功能",
            GameLoadMode.DebugMode => "调试环境，详细日志信息",
            _ => "未知模式"
        };

        EditorGUILayout.HelpBox(description, MessageType.Info);

        EditorGUILayout.EndVertical();
    }

    ///// <summary>
    ///// 绘制快速操作区域
    ///// </summary>
    //private void DrawQuickActions()
    //{
    //    GUILayout.Space(10);
    //    EditorGUILayout.BeginVertical("box");

    //    EditorGUILayout.LabelField("快速操作:", EditorStyles.boldLabel);
    //    GUILayout.Space(5);

    //    EditorGUILayout.BeginHorizontal();

    //    if (GUILayout.Button("应用到当前场景", GUILayout.Height(25)))
    //    {
    //        ApplyToCurrentScene();
    //    }

    //    if (GUILayout.Button("重置为默认", GUILayout.Height(25)))
    //    {
    //        ResetToDefault();
    //    }

    //    EditorGUILayout.EndHorizontal();

    //    if (GUILayout.Button("在控制台打印模式信息", GUILayout.Height(25)))
    //    {
    //        PrintModeInfo();
    //    }

    //    EditorGUILayout.EndVertical();
    //}

    /// <summary>
    /// 设置加载模式
    /// </summary>
    private void SetLoadMode(GameLoadMode newMode)
    {
        currentMode = newMode;
        EditorPrefs.SetInt(PREFS_KEY, (int)newMode);

        Debug.Log($"加载模式已切换为: {newMode} (值: {(int)newMode})");

        //// 如果正在运行，立即应用设置
        //if (Application.isPlaying)
        //{
        //    ApplyToCurrentScene();
        //}

        Repaint(); // 刷新界面
    }

    ///// <summary>
    ///// 应用到当前场景
    ///// </summary>
    //private void ApplyToCurrentScene()
    //{
    //    if (Application.isPlaying)
    //    {
    //        GameLoadModeManager manager = GameLoadModeManager.Instance;
    //        manager.CurrentLoadMode = currentMode;
    //        manager.ApplyLoadModeSettings();
    //        Debug.Log($"已应用加载模式到当前场景: {currentMode}");
    //    }
    //    else
    //    {
    //        Debug.Log("请先进入播放模式以应用设置");
    //    }
    //}

    ///// <summary>
    ///// 重置为默认模式
    ///// </summary>
    //private void ResetToDefault()
    //{
    //    if (EditorUtility.DisplayDialog("重置确认", "确定要重置为默认模式吗？", "确定", "取消"))
    //    {
    //        SetLoadMode(GameLoadMode.Development);
    //    }
    //}

    ///// <summary>
    ///// 打印模式信息
    ///// </summary>
    //private void PrintModeInfo()
    //{
    //    Debug.Log($"=== 当前加载模式信息 ===");
    //    Debug.Log($"模式: {currentMode}");
    //    Debug.Log($"数值: {(int)currentMode}");
    //    Debug.Log($"保存键: {PREFS_KEY}");
    //    Debug.Log($"保存值: {EditorPrefs.GetInt(PREFS_KEY, 0)}");
    //    Debug.Log($"======================");
    //}

    ///// <summary>
    ///// 添加菜单项快速切换模式
    ///// </summary>
    //[MenuItem("Tools/加载模式/切换到开发模式 %&1")]
    //private static void SwitchToDevelopment() => SwitchMode(GameLoadMode.Development);

    //[MenuItem("Tools/加载模式/切换到生产模式 %&2")]
    //private static void SwitchToProduction() => SwitchMode(GameLoadMode.Production);

    //[MenuItem("Tools/加载模式/切换到测试模式 %&3")]
    //private static void SwitchToTesting() => SwitchMode(GameLoadMode.Testing);

    //[MenuItem("Tools/加载模式/切换到演示模式 %&4")]
    //private static void SwitchToDemo() => SwitchMode(GameLoadMode.Demo);

    //[MenuItem("Tools/加载模式/切换到调试模式 %&5")]
    //private static void SwitchToDebug() => SwitchMode(GameLoadMode.DebugMode);

    //private static void SwitchMode(GameLoadMode mode)
    //{
    //    EditorPrefs.SetInt(PREFS_KEY, (int)mode);
    //    Debug.Log($"已快速切换到: {mode}");

    //    // 刷新所有打开的窗口
    //    LoadModeToolbarEditor[] windows = Resources.FindObjectsOfTypeAll<LoadModeToolbarEditor>();
    //    foreach (var window in windows)
    //    {
    //        window.Repaint();
    //    }
    //}

    ///// <summary>
    ///// 验证菜单项状态
    ///// </summary>
    //[MenuItem("Tools/加载模式/切换到开发模式", true)]
    //[MenuItem("Tools/加载模式/切换到生产模式", true)]
    //[MenuItem("Tools/加载模式/切换到测试模式", true)]
    //[MenuItem("Tools/加载模式/切换到演示模式", true)]
    //[MenuItem("Tools/加载模式/切换到调试模式", true)]
    //private static bool ValidateSwitchMode()
    //{
    //    return true;
    //}
}
#endif