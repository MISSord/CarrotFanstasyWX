using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 在 Inspector 中维护「字符串键 → Unity 对象引用」列表，供运行时按 key 取用。
/// </summary>
[Serializable]
public class ReferenceCollectorData
{
    public string key;
    public UnityEngine.Object gameObject;
}

public class ReferenceCollector : MonoBehaviour
{
    public List<ReferenceCollectorData> data = new List<ReferenceCollectorData>();

    public T Get<T>(string key) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        for (int i = 0; i < this.data.Count; i++)
        {
            ReferenceCollectorData item = this.data[i];
            if (item.key != key || item.gameObject == null)
            {
                continue;
            }

            T match = item.gameObject as T;
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    public void Remove(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        for (int i = this.data.Count - 1; i >= 0; i--)
        {
            if (this.data[i].key == key)
            {
                this.data.RemoveAt(i);
            }
        }
    }

    public void Sort()
    {
        this.data.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
    }
}
