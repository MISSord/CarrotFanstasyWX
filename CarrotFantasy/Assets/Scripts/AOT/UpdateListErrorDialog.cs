using System;
using UnityEngine;

/// <summary>
/// 获取热更新清单失败时的 IMGUI 提示：退出 / 重启（重启逻辑暂未实现）。
/// </summary>
public class UpdateListErrorDialog : MonoBehaviour
{
    private Action _onExit;
    private Action _onRestart;
    private Texture2D _overlayTexture;

    public void Setup(Action onExit, Action onRestart)
    {
        _onExit = onExit;
        _onRestart = onRestart;
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
            "获取热更新列表有问题，请重启游戏。",
            messageStyle);

        float buttonWidth = 170f;
        float buttonHeight = 55f;
        float buttonY = y + dialogHeight - 95f;
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 22
        };

        if (GUI.Button(new Rect(x + 60, buttonY, buttonWidth, buttonHeight), "退出游戏", buttonStyle))
        {
            _onExit?.Invoke();
        }

        if (GUI.Button(
                new Rect(x + dialogWidth - buttonWidth - 60, buttonY, buttonWidth, buttonHeight),
                "重启游戏",
                buttonStyle))
        {
            // 重启逻辑尚未实现，先占位无反应。
            _onRestart?.Invoke();
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
