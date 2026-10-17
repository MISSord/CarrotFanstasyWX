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
        if (_instance != null)
        {
            return;
        }

        _instance = GameObject.FindAnyObjectByType<SRPScheduler>();
        if (_instance != null)
        {
            return;
        }

        // 场景里没挂：自建一个常驻宿主，避免清单/下载协程被静默丢弃（启动卡死）。
        var host = new GameObject("[SRPScheduler]");
        UnityEngine.Object.DontDestroyOnLoad(host);
        _instance = host.AddComponent<SRPScheduler>();
        Debug.LogWarning("[SRPScheduler] 场景中没有 SRPScheduler，已自动创建常驻实例");
    }

    public static void StartRunCoroutine(IEnumerator coroutine)
    {
        if (_instance != null)
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
        if (_instance != null)
        {
            _instance.StopAllCoroutines();
        }
    }
}
