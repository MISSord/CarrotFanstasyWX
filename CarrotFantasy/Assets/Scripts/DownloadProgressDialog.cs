using UnityEngine;

/// <summary>
/// 使用 IMGUI 显示下载进度窗口。
/// 挂载到临时 GameObject 上，由 DownloadState 创建与销毁。
/// </summary>
public class DownloadProgressDialog : MonoBehaviour
{
    private AssetBundleDownloader _downloader;
    private Texture2D _overlayTexture;
    private Texture2D _progressTexture;
    private Texture2D _progressBgTexture;

    public void Setup(AssetBundleDownloader downloader)
    {
        _downloader = downloader;
    }

    private void OnDestroy()
    {
        if (_overlayTexture != null)
        {
            Destroy(_overlayTexture);
            _overlayTexture = null;
        }

        if (_progressTexture != null)
        {
            Destroy(_progressTexture);
            _progressTexture = null;
        }

        if (_progressBgTexture != null)
        {
            Destroy(_progressBgTexture);
            _progressBgTexture = null;
        }
    }

    private void OnGUI()
    {
        if (_downloader == null)
        {
            return;
        }

        DrawOverlay();

        const float dialogWidth = 600f;
        const float dialogHeight = 260f;
        float x = (Screen.width - dialogWidth) * 0.5f;
        float y = (Screen.height - dialogHeight) * 0.5f;

        GUI.Box(new Rect(x, y, dialogWidth, dialogHeight), "");

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(x + 20, y + 25, dialogWidth - 40, 40), "资源下载中", titleStyle);

        LoaderState loaderState = _downloader.GetLoaderState();
        bool isConverting = loaderState == LoaderState.Convert;
        bool isIdle = loaderState == LoaderState.Idle;

        if (isConverting || isIdle)
        {
            GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter
            };
            string status = isIdle ? "下载完成，准备进入游戏" : "解压中，请稍候...";
            GUI.Label(new Rect(x + 20, y + 90, dialogWidth - 40, 40), status, statusStyle);
        }
        else
        {
            float progress = _downloader.GetTotalProgress();
            long downloaded = _downloader.GetDownloadedBytes();
            long total = _downloader.GetTotalDownloadSize();
            string speedText = _downloader.GetDownloadSpeedText();

            DrawProgressBar(x + 40, y + 90, dialogWidth - 80, 32, progress);

            GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            string info = $"{FormatBytes(downloaded)} / {FormatBytes(total)}   {speedText}";
            GUI.Label(new Rect(x + 20, y + 140, dialogWidth - 40, 30), info, infoStyle);
        }
    }

    private void DrawProgressBar(float x, float y, float width, float height, float progress)
    {
        if (_progressBgTexture == null)
        {
            _progressBgTexture = new Texture2D(1, 1);
            _progressBgTexture.SetPixel(0, 0, new Color(0.2f, 0.2f, 0.2f, 1f));
            _progressBgTexture.Apply();
        }

        if (_progressTexture == null)
        {
            _progressTexture = new Texture2D(1, 1);
            _progressTexture.SetPixel(0, 0, new Color(0.2f, 0.8f, 0.2f, 1f));
            _progressTexture.Apply();
        }

        GUI.DrawTexture(new Rect(x, y, width, height), _progressBgTexture, ScaleMode.StretchToFill, false, 0, Color.white, 0, 0);

        float fillWidth = Mathf.Max(0f, width * progress);
        if (fillWidth > 0)
        {
            GUI.DrawTexture(new Rect(x, y, fillWidth, height), _progressTexture, ScaleMode.StretchToFill, false, 0, Color.white, 0, 0);
        }

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        GUI.Label(new Rect(x, y, width, height), $"{(progress * 100):F1}%", labelStyle);
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
