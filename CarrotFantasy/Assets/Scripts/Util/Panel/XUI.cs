using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class XUI : MonoBehaviour
{

    public static void AddButtonListener(Button button, UnityEngine.Events.UnityAction onClickEvent){
        button.onClick.AddListener(onClickEvent);
    }
}
