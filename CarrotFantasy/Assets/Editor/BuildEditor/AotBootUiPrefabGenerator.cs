using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using CarrotFantasy;

/// <summary>
/// 生成 AOT 启动期 Resources 预制体（带 UINameTable），供 AotResourcesView 加载。
/// </summary>
public static class AotBootUiPrefabGenerator
{
    const string ResourcesFolder = "Assets/Resources/AotUI";

    [MenuItem("Tools/AOT UI/生成启动界面预制体", priority = 200)]
    public static void GenerateAll()
    {
        EnsureFolder(ResourcesFolder);

        CreateTwoButtonDialog(
            "DownloadConfirm",
            "资源更新",
            "发现新版本资源，需要下载。",
            "下载",
            "退出");

        CreateProgressDialog("DownloadProgress");

        CreateTwoButtonDialog(
            "UpdateListError",
            "热更新异常",
            "获取热更新列表有问题，请重启游戏。",
            "退出游戏",
            "重启游戏");

        CreateTwoButtonDialog(
            "UpdateListFallback",
            "热更新异常",
            "拉取最新资源失败，是否依然进行游戏？",
            "继续游戏",
            "退出游戏");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "AOT UI",
            "已生成 Resources/AotUI 下启动界面预制体。\n可在 Hierarchy 中微调样式后重新生成或直接改 prefab。",
            "确定");
    }

    static void CreateTwoButtonDialog(
        string assetName,
        string title,
        string message,
        string confirmLabel,
        string cancelLabel)
    {
        GameObject root = CreateCanvasRoot(assetName);
        RectTransform panel = CreatePanel(root.transform, new Vector2(560f, 340f));

        Text titleText = CreateText(panel, "Title", title, 28, FontStyle.Bold, new Vector2(0f, 110f), new Vector2(480f, 48f));
        Text messageText = CreateText(panel, "Message", message, 20, FontStyle.Normal, new Vector2(0f, 20f), new Vector2(480f, 100f));
        messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
        messageText.verticalOverflow = VerticalWrapMode.Overflow;

        Button confirm = CreateButton(panel, "Btn_Confirm", confirmLabel, new Vector2(-120f, -110f), new Vector2(170f, 56f));
        Button cancel = CreateButton(panel, "Btn_Cancel", cancelLabel, new Vector2(120f, -110f), new Vector2(170f, 56f));

        BindNameTable(
            root,
            new UINameEntry { name = "Title", uiReference = titleText.gameObject },
            new UINameEntry { name = "Message", uiReference = messageText.gameObject },
            new UINameEntry { name = "Btn_Confirm", uiReference = confirm.gameObject },
            new UINameEntry { name = "Btn_Cancel", uiReference = cancel.gameObject });

        SavePrefab(root, assetName);
    }

    static void CreateProgressDialog(string assetName)
    {
        GameObject root = CreateCanvasRoot(assetName);
        RectTransform panel = CreatePanel(root.transform, new Vector2(600f, 260f));

        Text titleText = CreateText(panel, "Title", "资源下载中", 26, FontStyle.Bold, new Vector2(0f, 80f), new Vector2(520f, 40f));
        Text statusText = CreateText(panel, "Status", "0%", 22, FontStyle.Normal, new Vector2(0f, 20f), new Vector2(520f, 36f));
        Text infoText = CreateText(panel, "Info", "", 18, FontStyle.Normal, new Vector2(0f, -30f), new Vector2(520f, 30f));

        GameObject progressBg = new GameObject("Progress", typeof(RectTransform), typeof(Image));
        progressBg.transform.SetParent(panel, false);
        RectTransform progressRt = progressBg.GetComponent<RectTransform>();
        progressRt.anchorMin = new Vector2(0.5f, 0.5f);
        progressRt.anchorMax = new Vector2(0.5f, 0.5f);
        progressRt.pivot = new Vector2(0.5f, 0.5f);
        progressRt.sizeDelta = new Vector2(480f, 28f);
        progressRt.anchoredPosition = new Vector2(0f, -80f);
        Image bgImage = progressBg.GetComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        GameObject fillGo = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(progressBg.transform, false);
        RectTransform fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        Image fillImage = fillGo.GetComponent<Image>();
        fillImage.color = new Color(0.25f, 0.8f, 0.3f, 1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0f;

        BindNameTable(
            root,
            new UINameEntry { name = "Title", uiReference = titleText.gameObject },
            new UINameEntry { name = "Status", uiReference = statusText.gameObject },
            new UINameEntry { name = "Info", uiReference = infoText.gameObject },
            new UINameEntry { name = "ProgressFill", uiReference = fillGo });

        SavePrefab(root, assetName);
    }

    static GameObject CreateCanvasRoot(string name)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UINameTable));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 半透明遮罩
        GameObject overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(root.transform, false);
        RectTransform overlayRt = overlay.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        // EventSystem 由运行时 AotBootUi.EnsureEventSystem 保证
        return root;
    }

    static RectTransform CreatePanel(Transform parent, Vector2 size)
    {
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.96f);
        return rt;
    }

    static Text CreateText(
        Transform parent,
        string name,
        string content,
        int fontSize,
        FontStyle style,
        Vector2 anchoredPos,
        Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        Text text = go.GetComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return text;
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.25f, 0.55f, 0.95f, 1f);

        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.35f, 0.65f, 1f, 1f);
        colors.pressedColor = new Color(0.2f, 0.45f, 0.85f, 1f);
        button.colors = colors;

        Text text = CreateText(go.transform, "Label", label, 22, FontStyle.Bold, Vector2.zero, size);
        RectTransform textRt = text.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        return button;
    }

    static void BindNameTable(GameObject root, params UINameEntry[] entries)
    {
        UINameTable table = root.GetComponent<UINameTable>();
        table.uiEntries = new List<UINameEntry>(entries);
    }

    static void SavePrefab(GameObject root, string assetName)
    {
        string path = ResourcesFolder + "/" + assetName + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log("[AotBootUiPrefabGenerator] 已生成: " + path);
    }

    static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
        string name = Path.GetFileName(assetFolder);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }
}
