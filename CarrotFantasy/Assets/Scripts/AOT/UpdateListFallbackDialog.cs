using System;
using UnityEngine;

/// <summary>
/// 远程热更新列表失败、但本地有缓存时：询问是否继续游戏。
/// </summary>
public class UpdateListFallbackDialog : MonoBehaviour
{
    private Action _onContinue;
    private Action _onExit;
    private Texture2D _overlayTexture;

    public void Setup(Action onContinue, Action onExit)
    {
        _onContinue = onContinue;
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

        const float dialogWidth = 560f;
        const float dialogHeight = 340f;
        float x = (Screen.width - dialogWidth) * 0.5f;
        float y = (Screen.height - dialogHeight) * 0.5f;

        GUI.Box(new Rect(x, y, dialogWidth, dialogHeight), "");

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(x + 20, y + 28, dialogWidth - 40, 50), "热更新异常", titleStyle);

        GUIStyle messageStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        GUI.Label(
            new Rect(x + 30, y + 100, dialogWidth - 60, 100),
            "拉取最新资源失败，是否依然进行游戏？",
            messageStyle);

        float buttonWidth = 170f;
        float buttonHeight = 55f;
        float buttonY = y + dialogHeight - 95f;
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 22
        };

        // 左：继续游戏；右：退出游戏
        if (GUI.Button(new Rect(x + 60, buttonY, buttonWidth, buttonHeight), "继续游戏", buttonStyle))
        {
            _onContinue?.Invoke();
        }

        if (GUI.Button(
                new Rect(x + dialogWidth - buttonWidth - 60, buttonY, buttonWidth, buttonHeight),
                "退出游戏",
                buttonStyle))
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
}
