using System;
using System.Collections.Generic;
using UnityEngine;

public class UINameTableDic
{
    protected Dictionary<string, GameObject> uiEntries = new Dictionary<string, GameObject>();
    protected Dictionary<string, Dictionary<Type, Component>> cachedComponents = new Dictionary<string, Dictionary<Type, Component>>();

    private Component GetComponentSafely(string name, Type componentType)
    {
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning("物体名字为空");
            return null;
        }

        if (componentType == null)
        {
            Debug.LogError("组件类型不能为空");
            return null;
        }

        GameObject gameObject;
        if (uiEntries.TryGetValue(name, out gameObject) == false)
        {
            Debug.LogWarning($"没有该物体: {name}");
            return null;
        }

        Dictionary<Type, Component> compDic;
        if (cachedComponents.TryGetValue(name, out compDic))
        {
            Component component1;
            if (compDic.TryGetValue(componentType, out component1) && component1 != null)
            {
                return component1;
            }
        }
        else
        {
            compDic = new Dictionary<Type, Component>();
            cachedComponents.Add(name, compDic);
        }

        // 尝试获取组件
        Component component = gameObject.GetComponent(componentType);
        if (component == null)
        {
            Debug.LogError($"类型 {componentType.Name} 获取失败，请检查预制体和代码");
            return null;
        }

        compDic[componentType] = component;
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
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning("物体名字为空");
            return null;
        }

        GameObject gameObject;
        if (uiEntries.TryGetValue(name, out gameObject) == false)
        {
            Debug.LogWarning($"没有该物体: {name}");
            return null;
        }
        return gameObject;
    }

    /// <summary>
    /// 按名称获取 UI 根节点，等价于 <see cref="GetGameObjectSafely"/>。
    /// 示例：<c>nameTableDic["btn_ok"].GetComponent&lt;Button&gt;()</c>
    /// （仅 Unity 自带查找；需要走名称表缓存时请用 <see cref="GetComponentSafely{T}"/>。）
    /// </summary>
    public GameObject this[string name]
    {
        get => GetGameObjectSafely(name);
    }

    public void AddUINameTable(List<UINameEntry> list)
    {
        if (list == null)
        {
            Debug.LogWarning("UINameTable 列表为空，忽略本次添加");
            return;
        }

        foreach (UINameEntry entry in list)
        {
            if (string.IsNullOrEmpty(entry.name))
            {
                Debug.LogError("UINameEntry.name 为空，已跳过");
                continue;
            }

            if (entry.uiReference == null)
            {
                Debug.LogError($"UINameEntry.uiReference 为空，已跳过: {entry.name}");
                continue;
            }

            if (uiEntries.ContainsKey(entry.name))
            {
                Debug.LogError($"实体名称重复，立马修改{entry.name}");
                continue;
            }
            uiEntries.Add(entry.name, entry.uiReference);
        }
    }

    public void ClearAllInfo()
    {
        uiEntries.Clear();
        cachedComponents.Clear();
    }
}
