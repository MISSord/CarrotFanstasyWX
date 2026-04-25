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
}
