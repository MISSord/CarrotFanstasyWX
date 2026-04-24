using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 记录单个AB包信息
/// </summary>
[System.Serializable]
public class CustomAssetBundleInfo
{
    public string AssetName;
    public string BundleName;
    public string Hash;
    public string Version = "1.0.0";
    public long Size;
    public string[] Dependencies;
}

/// <summary>
/// 清单类
/// </summary>
[System.Serializable]
public class CustomManifest
{
    public string AppVersion;
    public int ManifestVersion = 0;
    public string BuildTime;
    public long buildTime;
    /// <summary>
    /// 压缩格式 0为LZMA，1为LZ4，2为无压缩
    /// </summary>
    public int CompressedFormat = 0;
    public List<CustomAssetBundleInfo> AssetBundles = new List<CustomAssetBundleInfo>();
}

[System.Serializable]
public class UpdateCheckResult
{
    public CustomManifest customManifest;
    public List<CustomAssetBundleInfo> bundlesToDownload = new List<CustomAssetBundleInfo>(); //新增列表
    public List<CustomAssetBundleInfo> bundlesToUpdate = new List<CustomAssetBundleInfo>(); //需要更新的列表
    public List<CustomAssetBundleInfo> upToDateBundles = new List<CustomAssetBundleInfo>(); //无需更新的列表
    public long totalDownloadSize = 0;
    public bool hasChanges = false;
    public float progress = 0f;
    public string currentOperation = "";
    public bool isSuccess = false;
    public string VersionNumber = "";
    //public ErrorCode errorCode;
}

public class AssetBundleUpdateChecker
{
    // 状态机枚举
    public enum CheckerState
    {
        Idle,                   // 空闲状态
        DownloadingManifest,    // 下载远程清单
        LoadingLocalManifest,   // 加载本地清单
        ComparingManifests,     // 对比清单
        VerifyingFiles,         // 验证文件
        Complete,               // 完成
        Error                   // 错误
    }

    [Header("性能配置")]
    public int bundlesPerFrame = 5;           // 每帧处理的AB包数量
    public float timeSlicePerFrame = 0.005f;  // 每帧最大处理时间(秒)

    [Header("校验配置")]
    public VerifyMethod verifyMethod = VerifyMethod.HashOnly;

    public enum VerifyMethod
    {
        CRCOnly,
        HashOnly,
        CRCAndHash
    }

    // 状态变量
    private CheckerState m_CurrentState = CheckerState.Idle;
    private CustomManifest m_RemoteManifest; // 服务端的AB包清单
    private CustomManifest m_LocalManifest;  // 本地的AB包清单
    private string m_LocalManifestPath;
    private UpdateCheckResult m_CurrentResult; // 校验结果
    private Action<UpdateCheckResult> m_OnCompleteCallback;
    private string m_RemoteManifestUrl;

    // 分帧处理变量
    private List<CustomAssetBundleInfo> m_RemainingBundlesToCheck;
    private int m_CurrentBundleIndex = 0;
    private float m_Progress = 0f;
    private string m_CurrentOperation = "";

    // 公共属性
    public CheckerState CurrentState => m_CurrentState;
    public float Progress => m_Progress;
    public string CurrentOperation => m_CurrentOperation;
    public bool IsRunning => m_CurrentState != CheckerState.Idle &&
                           m_CurrentState != CheckerState.Complete &&
                           m_CurrentState != CheckerState.Error;

    /// <summary>
    /// 开始检查更新（异步，通过Update驱动）
    /// </summary>
    public void StartUpdateCheck(string remoteManifestUrl, Action<UpdateCheckResult> onComplete)
    {
        if (IsRunning)
        {
            Debug.LogWarning("检查器正在运行，请等待完成");
            return;
        }

        ResetState();
        m_RemoteManifestUrl = remoteManifestUrl + "/custom_manifest.json";
        m_OnCompleteCallback = onComplete;

        ChangeState(CheckerState.DownloadingManifest);
        Debug.Log(string.Format("开始下载清单 {0}", m_RemoteManifestUrl));

        // 开始下载清单（协程）
        SRPScheduler.StartRunCoroutine(DownloadRemoteManifestCoroutine());
    }

    /// <summary>
    /// 状态机更新驱动
    /// </summary>
    public void Update()
    {
        if (!IsRunning) return;

        switch (m_CurrentState)
        {
            case CheckerState.ComparingManifests:
                ExecuteCompareManifests();
                break;

            case CheckerState.VerifyingFiles:
                ExecuteVerifyFiles();
                break;
        }
    }

    /// <summary>
    /// 状态转换
    /// </summary>
    private void ChangeState(CheckerState newState)
    {
        if (m_CurrentState == newState) return;

        Debug.Log($"状态转换: {m_CurrentState} -> {newState}");
        m_CurrentState = newState;

        if (newState == CheckerState.LoadingLocalManifest)
        {
            m_CurrentOperation = "加载本地清单...";
            ExecuteLoadLocalManifest();
        }
        else if (newState == CheckerState.ComparingManifests)
        {
            m_CurrentOperation = "对比清单文件...";
            PrepareComparison();
        }
        else if (newState == CheckerState.VerifyingFiles)
        {
            m_CurrentOperation = "验证文件完整性...";
            PrepareVerification();
        }
        else if (newState == CheckerState.Complete)
        {
            m_CurrentOperation = "检查完成";
            m_Progress = 1f;
            OnCheckComplete();
        }
        else if (newState == CheckerState.Error)
        {
            m_CurrentOperation = "发生错误";
            OnCheckError();
        }
    }

    /// <summary>
    /// 下载远程清单（协程）
    /// </summary>
    private IEnumerator DownloadRemoteManifestCoroutine()
    {
        m_CurrentOperation = "下载远程清单...";

        using (UnityWebRequest www = UnityWebRequest.Get(m_RemoteManifestUrl))
        {
            www.timeout = 10;
            var operation = www.SendWebRequest();

            while (!operation.isDone)
            {
                m_Progress = operation.progress * 0.3f; // 下载占30%进度
                yield return null;
            }

            CheckerState state;
            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    m_RemoteManifest = JsonUtility.FromJson<CustomManifest>(www.downloadHandler.text);
                    Debug.Log($"远程清单加载成功，包含 {m_RemoteManifest.AssetBundles?.Count} 个AB包");
                    state = CheckerState.LoadingLocalManifest;
                }
                catch (Exception e)
                {
                    Debug.LogError($"解析远程清单失败: {e.Message}");
                    state = CheckerState.Error;
                }
            }
            else
            {
                Debug.LogError($"下载远程清单失败: {www.error}");
                state = CheckerState.Error;
            }
            ChangeState(state);
        }
    }

    /// <summary>
    /// 执行加载本地清单
    /// </summary>
    private void ExecuteLoadLocalManifest()
    {
        Debug.Log("ExecuteLoadLocalManifest");
        m_LocalManifestPath = Path.Combine(Application.persistentDataPath, "custom_manifest.json");

        try
        {
            if (File.Exists(m_LocalManifestPath))
            {
                string localJson = File.ReadAllText(m_LocalManifestPath);
                m_LocalManifest = JsonUtility.FromJson<CustomManifest>(localJson);
                Debug.Log($"本地清单加载成功，包含 {m_LocalManifest.AssetBundles?.Count} 个AB包");
            }
            else
            {
                m_LocalManifest = new CustomManifest { AssetBundles = new List<CustomAssetBundleInfo>() };
                Debug.Log("本地清单不存在，将下载所有AB包");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"加载本地清单失败: {e.Message}");
            m_LocalManifest = new CustomManifest { AssetBundles = new List<CustomAssetBundleInfo>() };
        }

        m_Progress = 0.4f; // 本地加载完成，进度到40%
        ChangeState(CheckerState.ComparingManifests);
    }

    /// <summary>
    /// 准备清单对比
    /// </summary>
    private void PrepareComparison()
    {
        if (m_RemoteManifest?.AssetBundles == null)
        {
            Debug.LogError("远程清单为空，无法对比");
            ChangeState(CheckerState.Error);
            return;
        }

        m_CurrentResult = new UpdateCheckResult();
        m_RemainingBundlesToCheck = new List<CustomAssetBundleInfo>(m_RemoteManifest.AssetBundles);
        m_CurrentBundleIndex = 0;
        m_Progress = 0.5f;

        if (m_RemoteManifest != null && m_RemoteManifest.ManifestVersion == m_LocalManifest.ManifestVersion)
        {
            Debug.Log("清单版本一致，无需对比，直接跳到完成");
            ChangeState(CheckerState.Complete);
            return;
        }
    }

    /// <summary>
    /// 执行清单对比（分帧）
    /// </summary>
    private void ExecuteCompareManifests()
    {
        if (m_RemainingBundlesToCheck == null || m_RemainingBundlesToCheck.Count == 0)
        {
            ChangeState(CheckerState.VerifyingFiles);
            return;
        }

        float startTime = Time.realtimeSinceStartup;
        int processedCount = 0;

        // 分帧处理：数量限制 + 时间片限制
        while (m_RemainingBundlesToCheck.Count > 0 &&
               processedCount < bundlesPerFrame &&
               (Time.realtimeSinceStartup - startTime) < timeSlicePerFrame)
        {
            var remoteBundle = m_RemainingBundlesToCheck[0];
            m_RemainingBundlesToCheck.RemoveAt(0);

            // 查找本地对应的AB包
            var localBundle = FindLocalBundle(remoteBundle.AssetName);

            if (localBundle == null)
            {
                // 新增的AB包
                m_CurrentResult.bundlesToDownload.Add(remoteBundle);
                m_CurrentResult.totalDownloadSize += remoteBundle.Size;
            }
            else
            {
                // 检查文件是否存在，如果不存在也加入下载列表
                string localFilePath = AssetBundlePathHelper.GetLocalLZ4Path(remoteBundle.BundleName);
                if (!File.Exists(localFilePath))
                {
                    m_CurrentResult.bundlesToDownload.Add(remoteBundle);
                    m_CurrentResult.totalDownloadSize += remoteBundle.Size;
                }
                else
                {
                    // 需要验证的包加入待验证列表
                    m_CurrentResult.bundlesToUpdate.Add(remoteBundle);
                }
            }

            processedCount++;
            m_CurrentBundleIndex++;
        }

        // 更新进度
        int totalBundles = m_RemoteManifest.AssetBundles.Count;
        m_Progress = 0.5f + 0.2f * (m_CurrentBundleIndex / (float)totalBundles); // 对比占20%进度

        m_CurrentOperation = $"对比清单文件... ({m_CurrentBundleIndex}/{totalBundles})";

        // 如果处理完成，进入下一状态
        if (m_RemainingBundlesToCheck.Count == 0)
        {
            Debug.Log($"清单对比完成: 新增{m_CurrentResult.bundlesToDownload.Count}个, 待验证{m_CurrentResult.bundlesToUpdate.Count}个");
            ChangeState(CheckerState.VerifyingFiles);
        }
    }

    ///// <summary>
    ///// 准备文件验证
    ///// </summary>
    private void PrepareVerification()
    {
        // 将待验证的包转移到剩余检查列表
        m_RemainingBundlesToCheck = new List<CustomAssetBundleInfo>(m_CurrentResult.bundlesToUpdate);
        m_CurrentResult.bundlesToUpdate.Clear(); // 清空，后面重新添加
        m_CurrentBundleIndex = 0;
        m_Progress = 0.7f; // 验证阶段从70%开始
    }

    /// <summary>
    /// 执行文件验证（分帧）
    /// </summary>
    private void ExecuteVerifyFiles()
    {
        if (m_RemainingBundlesToCheck == null || m_RemainingBundlesToCheck.Count == 0)
        {
            FinalizeResult();
            ChangeState(CheckerState.Complete);
            return;
        }

        float startTime = Time.realtimeSinceStartup;
        int processedCount = 0;

        while (m_RemainingBundlesToCheck.Count > 0 &&
               processedCount < bundlesPerFrame &&
               (Time.realtimeSinceStartup - startTime) < timeSlicePerFrame)
        {
            var remoteBundle = m_RemainingBundlesToCheck[0];
            m_RemainingBundlesToCheck.RemoveAt(0);

            string localFilePath = AssetBundlePathHelper.GetLocalLZ4Path(remoteBundle.BundleName);
            bool needsUpdate = CheckBundleIntegrity(localFilePath, remoteBundle);

            if (needsUpdate)
            {
                m_CurrentResult.bundlesToUpdate.Add(remoteBundle);
                m_CurrentResult.totalDownloadSize += remoteBundle.Size;
            }
            else
            {
                m_CurrentResult.upToDateBundles.Add(remoteBundle);
            }

            processedCount++;
            m_CurrentBundleIndex++;
        }

        // 更新进度
        int totalBundlesToVerify = m_CurrentResult.bundlesToUpdate.Count + m_CurrentResult.upToDateBundles.Count + m_RemainingBundlesToCheck.Count;
        if (totalBundlesToVerify > 0)
        {
            m_Progress = 0.7f + 0.3f * (m_CurrentBundleIndex / (float)totalBundlesToVerify); // 验证占30%进度
        }

        m_CurrentOperation = $"验证文件完整性... ({m_CurrentBundleIndex}/{totalBundlesToVerify})";
    }

    /// <summary>
    /// 检查AB包完整性
    /// </summary>
    private bool CheckBundleIntegrity(string localFilePath, CustomAssetBundleInfo remoteBundle)
    {
        try
        {
            if (verifyMethod == VerifyMethod.CRCOnly)
            {
                return !CheckCRC(localFilePath, remoteBundle);
            }
            else if (verifyMethod == VerifyMethod.HashOnly)
            {
                return !CheckHash(localFilePath, remoteBundle);
            }
            else if (verifyMethod == VerifyMethod.CRCAndHash)
            {
                bool crcValid = CheckCRC(localFilePath, remoteBundle);
                bool hashValid = CheckHash(localFilePath, remoteBundle);
                return !(crcValid && hashValid);
            }
            else
            {
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"完整性检查失败 {localFilePath}: {e.Message}");
            return true; // 检查失败时要求更新
        }
    }

    /// <summary>
    /// 最终结果处理
    /// </summary>
    private void FinalizeResult()
    {
        m_CurrentResult.hasChanges = m_CurrentResult.bundlesToDownload.Count > 0 ||
                                   m_CurrentResult.bundlesToUpdate.Count > 0;

        Debug.Log($"检查完成: 新增{m_CurrentResult.bundlesToDownload.Count}个, " +
                 $"更新{m_CurrentResult.bundlesToUpdate.Count}个, " +
                 $"最新{m_CurrentResult.upToDateBundles.Count}个, " +
                 $"总大小: {m_CurrentResult.totalDownloadSize} bytes");
    }

    /// <summary>
    /// 检查完成回调
    /// </summary>
    private void OnCheckComplete()
    {
        m_CurrentResult.progress = 1f;
        m_CurrentResult.currentOperation = "完成";
        m_CurrentResult.isSuccess = true;
        m_CurrentResult.customManifest = m_RemoteManifest; //传递服务器的数据
        m_OnCompleteCallback?.Invoke(m_CurrentResult);
    }

    /// <summary>
    /// 检查错误回调
    /// </summary>
    private void OnCheckError()
    {
        var errorResult = new UpdateCheckResult();
        errorResult.currentOperation = "检查过程中发生错误";
        errorResult.isSuccess = false;
        m_OnCompleteCallback?.Invoke(errorResult);
    }

    /// <summary>
    /// 重置状态
    /// </summary>
    private void ResetState()
    {
        m_CurrentState = CheckerState.Idle;
        m_Progress = 0f;
        m_CurrentOperation = "";
        m_RemoteManifest = null;
        m_LocalManifest = null;
        m_CurrentResult = null;
        m_RemainingBundlesToCheck = null;
        m_CurrentBundleIndex = 0;
    }

    // 原有的工具方法保持不变
    private bool CheckCRC(string filePath, CustomAssetBundleInfo remoteBundle)
    {
        try
        {
            FileInfo fileInfo = new FileInfo(filePath);
            return fileInfo.Length == remoteBundle.Size;
        }
        catch (Exception e)
        {
            Debug.LogError($"CRC检查失败 {filePath}: {e.Message}");
            return false;
        }
    }

    private bool CheckHash(string filePath, CustomAssetBundleInfo remoteBundle)
    {
        try
        {
            string localHash = ComputeFileMD5(filePath);
            return string.Equals(localHash, remoteBundle.Hash, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e)
        {
            Debug.LogError($"哈希检查失败 {filePath}: {e.Message}");
            return false;
        }
    }

    private string ComputeFileMD5(string filePath)
    {
        using (var md5 = MD5.Create())
        {
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    private CustomAssetBundleInfo FindLocalBundle(string AssetName)
    {
        if (m_LocalManifest?.AssetBundles == null) return null;
        return m_LocalManifest.AssetBundles.Find(b =>
            string.Equals(b.AssetName, AssetName, StringComparison.OrdinalIgnoreCase));
    }

    //public List<CustomAssetBundleInfo> GetAllBundlesToDownload()
    //{
    //    if (m_CurrentResult == null) return new List<CustomAssetBundleInfo>();

    //    var allBundles = new List<CustomAssetBundleInfo>();
    //    allBundles.AddRange(m_CurrentResult.bundlesToDownload);
    //    allBundles.AddRange(m_CurrentResult.bundlesToUpdate);
    //    return allBundles;
    //}

    public static void SaveLocalManifest(CustomManifest generatedManifest)
    {
        // 保存清单文件
        string manifestJson = JsonUtility.ToJson(generatedManifest, true);
        string manifestPath = Path.Combine(Application.persistentDataPath, "custom_manifest.json");
        File.WriteAllText(manifestPath, manifestJson);
        Debug.Log("完成服务器清单本地保存");
    }

    //public void StopCheck()
    //{
    //    if (IsRunning)
    //    {
    //        Debug.Log("停止AB包检查");
    //        ResetState();
    //    }
    //}

    //public void ClearLocalCache()
    //{
    //    try
    //    {
    //        StopCheck();

    //        m_LocalManifestPath = Path.Combine(Application.persistentDataPath, "custom_manifest.json");
    //        if (File.Exists(m_LocalManifestPath))
    //        {
    //            File.Delete(m_LocalManifestPath);
    //        }

    //        string[] bundleFiles = Directory.GetFiles(Application.persistentDataPath, "*.*", SearchOption.AllDirectories);
    //        foreach (string file in bundleFiles)
    //        {
    //            if (!file.EndsWith(".json") && !file.EndsWith(".meta"))
    //            {
    //                File.Delete(file);
    //            }
    //        }

    //        m_LocalManifest = new CustomManifest { AssetBundles = new List<CustomAssetBundleInfo>() };
    //        Debug.Log("本地缓存已清理");
    //    }
    //    catch (Exception e)
    //    {
    //        Debug.LogError($"清理缓存失败: {e.Message}");
    //    }
    //}

    public void EndCheck()
    {
        this.ResetState();
    }
}