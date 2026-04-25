using System;
using System.Collections.Generic;
using UnityEngine;


public class UINameTableDic
{
    protected Dictionary<string, GameObject> finUiEntries = new Dictionary<string, GameObject>();
    protected Dictionary<string, Dictionary<Type, Component>> cacheCompent = new Dictionary<string, Dictionary<Type, Component>>();

    private Component GetComponentSafely(string name, Type componentType)
    {
        if (name == null || name.Equals(""))
        {
            Debug.LogWarning("物体名字为空");
            return null;
        }

        GameObject gameObject;
        if (finUiEntries.TryGetValue(name, out gameObject) == false)
        {
            Debug.LogWarning("没有该物体");
            return null;
        }

        Dictionary<Type, Component> compDic;
        if (cacheCompent.TryGetValue(name, out compDic))
        {
            Component component1 = compDic[componentType];
            if (component1 != null) return component1;
        }
        else
        {
            compDic = new Dictionary<Type, Component>();
            cacheCompent.Add(name, compDic);
        }

        if (componentType == null)
        {
            Debug.LogError("组件类型不能为空");
            return null;
        }

        if (!typeof(Component).IsAssignableFrom(componentType))
        {
            Debug.LogError($"类型 {componentType.Name} 不是Unity组件");
            return null;
        }

        // 尝试获取组件
        Component component = gameObject.GetComponent(componentType);
        if (component == null)
        {
            Debug.LogError($"类型 {componentType.Name} 获取失败，请检查预制体和代码");
            return null;
        }

        // 特殊处理：RectTransform是Transform的子类，需要特殊处理
        if (component == null && componentType == typeof(RectTransform))
        {
            Transform transform = gameObject.transform;
            if (transform is RectTransform rectTransform)
            {
                compDic.Add(componentType, rectTransform);
                return rectTransform;
            }
        }

        compDic.Add(componentType, component);
        return component;
    }

    /// <summary>
    /// 泛型版本 - 获取物体上的指定类型组件
    /// </summary>
    /// <typeparam name="T">组件类型</typeparam>
    /// <param name="gameObject">目标游戏物体</param>
    /// <returns>找到的组件，未找到则返回null</returns>
    public T GetComponentSafely<T>(string name) where T : Component
    {
        return (T)GetComponentSafely(name, typeof(T));
    }

    public GameObject GetGameObjectSafely(string name)
    {
        if (name == null || name.Equals(""))
        {
            Debug.LogWarning("物体名字为空");
            return null;
        }

        GameObject gameObject;
        if (finUiEntries.TryGetValue(name, out gameObject) == false)
        {
            Debug.LogWarning("没有该物体");
            return null;
        }
        return gameObject;
    }

    public void AddUINameTable(List<UINameEntry> list)
    {
        foreach (UINameEntry entry in list)
        {
            if (finUiEntries.ContainsKey(entry.name))
            {
                Debug.LogError($"实体名称重复，立马修改{entry.name}");
                continue;
            }
            finUiEntries.Add(entry.name, entry.uiReference);
        }
    }

    public void ClearAllInfo()
    {
        finUiEntries.Clear();
        cacheCompent.Clear();
    }
}
