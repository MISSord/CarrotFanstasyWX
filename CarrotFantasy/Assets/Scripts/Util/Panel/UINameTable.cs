using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct UINameEntry
{
    public string name;
    public GameObject uiReference;
};

public class UINameTable : MonoBehaviour
{

    public List<UINameEntry> uiEntries = new List<UINameEntry>();

    protected Dictionary<string, GameObject> finUiEntries = new Dictionary<string, GameObject>();

    public void ListToDic()
    {
        for (int index = 0; index < uiEntries.Count; index++)
        {
            UINameEntry entry = uiEntries[index];
            finUiEntries.Add(entry.name, entry.uiReference);
        }
    }

    public List<UINameEntry> GetNameTableList()
    {
        return uiEntries;
    }

    ///// <summary>
    ///// 获取物体上的指定类型组件
    ///// </summary>
    ///// <param name="gameObject">目标游戏物体</param>
    ///// <param name="componentType">要获取的组件类型</param>
    ///// <returns>找到的组件，未找到则返回null</returns>
    //public Component GetComponentSafely(string name, Type componentType)
    //{
    //    if(name == null || name.Equals(""))
    //    {
    //        Debug.LogWarning("物体名字为空");
    //        return null;
    //    }

    //    GameObject gameObject;
    //    if(finUiEntries.TryGetValue(name, out gameObject) == false)
    //    {
    //        Debug.LogWarning("没有该物体");
    //        return null;
    //    }

    //    if (componentType == null)
    //    {
    //        Debug.LogError("组件类型不能为空");
    //        return null;
    //    }

    //    if (!typeof(Component).IsAssignableFrom(componentType))
    //    {
    //        Debug.LogError($"类型 {componentType.Name} 不是Unity组件");
    //        return null;
    //    }

    //    // 尝试获取组件
    //    Component component = gameObject.GetComponent(componentType);

    //    // 特殊处理：RectTransform是Transform的子类，需要特殊处理
    //    if (component == null && componentType == typeof(RectTransform))
    //    {
    //        Transform transform = gameObject.transform;
    //        if (transform is RectTransform rectTransform)
    //        {
    //            return rectTransform;
    //        }
    //    }

    //    return component;
    //}

    ///// <summary>
    ///// 泛型版本 - 获取物体上的指定类型组件
    ///// </summary>
    ///// <typeparam name="T">组件类型</typeparam>
    ///// <param name="gameObject">目标游戏物体</param>
    ///// <returns>找到的组件，未找到则返回null</returns>
    //public T GetComponentSafely<T>(string name) where T : Component
    //{
    //    return (T)GetComponentSafely(name, typeof(T));
    //}

    //public GameObject GetGameObjectSafely(string name)
    //{
    //    if (name == null || name.Equals(""))
    //    {
    //        Debug.LogWarning("物体名字为空");
    //        return null;
    //    }

    //    GameObject gameObject;
    //    if (finUiEntries.TryGetValue(name, out gameObject) == false)
    //    {
    //        Debug.LogWarning("没有该物体");
    //        return null;
    //    }
    //    return gameObject;
    //}
}
