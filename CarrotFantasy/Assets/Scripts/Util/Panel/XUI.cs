using UnityEngine;
using UnityEngine.UI;

public class XUI : MonoBehaviour
{

    public static void AddButtonListener(Button button, UnityEngine.Events.UnityAction onClickEvent)
    {
        button.onClick.AddListener(onClickEvent);
    }
}
