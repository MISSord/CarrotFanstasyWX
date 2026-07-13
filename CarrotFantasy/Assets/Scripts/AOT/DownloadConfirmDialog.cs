using System;
using UnityEngine;

/// <summary>
/// 使用 IMGUI 显示下载确认对话框。
/// 挂载到临时 GameObject 上，由 DownloadConfirmState 创建与销毁。
/// </summary>
public class DownloadConfirmDialog : MonoBehaviour
{
    private long _downloadSizeBytes;
    private Action _onDownload;
    private Action _onExit;
    private Texture2D _overlayTexture;

    public void Setup(long downloadSizeBytes, Action onDownload, Action onExit)
    {
        _downloadSizeBytes = downloadSizeBytes;
        _onDownload = onDownload;
        _onExit = onExit;
    }

    private void OnDestroy()
    {
        if (_overlayTexture != null)
        {
            Destroy(_overlayTexture);
            _overlayTexture = null;
        }
    }

    private void OnGUI()
    {
        DrawOverlay();

        const float dialogWidth = 520f;
        const float dialogHeight = 320f;
        float x = (Screen.width - dialogWidth) * 0.5f;
        float y = (Screen.height - dialogHeight) * 0.5f;

        GUI.Box(new Rect(x, y, dialogWidth, dialogHeight), "");

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(x + 20, y + 30, dialogWidth - 40, 50), "资源更新", titleStyle);

        string message = $"发现新版本资源，需要下载 {FormatBytes(_downloadSizeBytes)}。";
        GUIStyle messageStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        GUI.Label(new Rect(x + 30, y + 110, dialogWidth - 60, 80), message, messageStyle);

        float buttonWidth = 160f;
        float buttonHeight = 55f;
        float buttonY = y + dialogHeight - 95f;

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 22
        };

        if (GUI.Button(new Rect(x + 60, buttonY, buttonWidth, buttonHeight), "下载", buttonStyle))
        {
            _onDownload?.Invoke();
        }

        if (GUI.Button(new Rect(x + dialogWidth - buttonWidth - 60, buttonY, buttonWidth, buttonHeight), "退出", buttonStyle))
        {
            _onExit?.Invoke();
        }
    }

    private void DrawOverlay()
    {
        if (_overlayTexture == null)
        {
            _overlayTexture = new Texture2D(1, 1);
            _overlayTexture.SetPixel(0, 0, Color.white);
            _overlayTexture.Apply();
        }

        GUI.DrawTexture(
            new Rect(0, 0, Screen.width, Screen.height),
            _overlayTexture,
            ScaleMode.StretchToFill,
            false,
            0,
            new Color(0, 0, 0, 0.6f),
            0,
            0);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }
}
