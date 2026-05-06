using UnityEngine;
using UnityEngine.UI;

public class XUI : MonoBehaviour
{
    public static void AddButtonListener(Button button, UnityEngine.Events.UnityAction onClickEvent)
    {
        if (button == null || onClickEvent == null) return;
        button.onClick.AddListener(onClickEvent);
    }

    public static void RemoveButtonAllListener(Button button)
    {
        button.onClick.RemoveAllListeners();
    }

    public static void RemoveButtonListener(Button button, UnityEngine.Events.UnityAction onClickEvent)
    {
        button.onClick.RemoveListener(onClickEvent);
    }
}
