using System.Collections;
using UnityEngine;

public class SRPScheduler : MonoBehaviour
{
    private static SRPScheduler _instance;
    
    public static SRPScheduler Instance
    {
        get { return _instance; }
    }

    public static void Init()
    {
        if (_instance == null)
        {
            _instance = GameObject.FindAnyObjectByType<SRPScheduler>();
        }
    }

    public static void StartRunCoroutine(IEnumerator coroutine)
    {
        if(_instance != null)
        {
            _instance.StartCoroutine(coroutine);
        }
    }

    public static void StopRunCoroutine(IEnumerator coroutine)
    {
        if (_instance != null)
        {
            _instance.StopCoroutine(coroutine);
        }
    }

    public void DeleteMe()
    {
        if(_instance != null)
        {
            _instance.StopAllCoroutines();
        }
    }
}
