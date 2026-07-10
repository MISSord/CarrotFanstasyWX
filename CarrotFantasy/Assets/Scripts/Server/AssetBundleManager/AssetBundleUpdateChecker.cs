using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 单个 AB 在清单中的元数据。
/// Hash/Size 对应构建产物；若 CompressedFormat==0，下载后会再压成 LZ4，落盘文件内容与此 Hash 不同。
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
/// 热更清单（custom_manifest.json）。
/// AssetBundles：按 AB 粒度的差异对比与加载依赖。
/// DownloadPacks：合并下载描述；为空则运行时按单个 AB 下载。
/// </summary>
[System.Serializable]
public class CustomManifest
{
    public string AppVersion;
    public int ManifestVersion = 0;
    public string BuildTime;
    public long buildTime;
    /// <summary>
    /// 压缩格式：0=LZMA（下载后转 LZ4），1=LZ4/ChunkBased，2=无压缩。
    /// 与 Editor CompressionType 枚举序号一致。
    /// </summary>
    public int CompressedFormat = 0;
    public List<CustomAssetBundleInfo> AssetBundles = new List<CustomAssetBundleInfo>();
    /// <summary>合并下载包列表；为空时回退为按单个 AB 下载。</summary>
    public List<DownloadPackInfo> DownloadPacks = new List<DownloadPackInfo>();
}

[System.Serializable]
public class DownloadPackInfo
{
    public string PackName;
    /// <summary>相对平台目录，如 packs/pack_boot_001.zip。</summary>
    public string PackFileName;
    public long PackSize;
    public string PackHash;
    public string[] BundleNames;
}

/// <summary>
/// 更新检查结果。Downloader 优先看 packsToDownload；为空则下载 bundlesToDownload+bundlesToUpdate。
/// </summary>
[System.Serializable]
public class UpdateCheckResult
{
    public CustomManifest customManifest;
    public List<CustomAssetBundleInfo> bundlesToDownload = new List<CustomAssetBundleInfo>(); //新增列表
    public List<CustomAssetBundleInfo> bundlesToUpdate = new List<CustomAssetBundleInfo>(); //需要更新的列表
    public List<CustomAssetBundleInfo> upToDateBundles = new List<CustomAssetBundleInfo>(); //无需更新的列表
    public List<DownloadPackInfo> packsToDownload = new List<DownloadPackInfo>();
    public long totalDownloadSize = 0;
    public bool hasChanges = false;
    public float progress = 0f;
    public string currentOperation = "";
    public bool isSuccess = false;
    public string VersionNumber = "";
    //public ErrorCode errorCode;
}

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

/// <summary>
/// 启动热更检查状态机。
///
/// 流程：
/// 1. DownloadingManifest — 拉远程 custom_manifest.json
/// 2. LoadingLocalManifest — 读 persistentDataPath/custom_manifest.json
/// 3. ComparingManifests — 分帧对比：无本地记录则查持久化文件；有记录则看文件是否存在
/// 4. VerifyingFiles — 对「存在」的包做完整性校验，分到 update / upToDate
/// 5. FinalizeResult — 先赋 customManifest，再 ApplyPackDownloadPlan
///
/// 校验策略见 CheckBundleIntegrity：默认 HashOnly(MD5)；
/// LZMA 模式下持久化目录中的已转换文件改用「本地清单源 Hash」比对版本。
/// </summary>
public class AssetBundleUpdateChecker
{
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
    private int m_RemainingCursor = 0;
    private int m_TotalBundlesInStage = 0;
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
        m_RemainingCursor = 0;
        m_TotalBundlesInStage = m_RemainingBundlesToCheck.Count;
        m_CurrentBundleIndex = 0;
        m_Progress = 0.5f;
    }

    /// <summary>
    /// 执行清单对比（分帧）。
    /// 本地无记录：IsPersistentBundleValid → 支持下载中断后续传。
    /// 本地有记录：文件缺失 → download；文件存在 → 暂入 bundlesToUpdate，交 VerifyingFiles 精判。
    /// </summary>
    private void ExecuteCompareManifests()
    {
        if (m_RemainingBundlesToCheck == null || m_RemainingCursor >= m_RemainingBundlesToCheck.Count)
        {
            ChangeState(CheckerState.VerifyingFiles);
            return;
        }

        float startTime = Time.realtimeSinceStartup;
        int processedCount = 0;

        // 分帧处理：数量限制 + 时间片限制
        while (m_RemainingCursor < m_RemainingBundlesToCheck.Count &&
               processedCount < bundlesPerFrame &&
               (Time.realtimeSinceStartup - startTime) < timeSlicePerFrame)
        {
            var remoteBundle = m_RemainingBundlesToCheck[m_RemainingCursor];
            m_RemainingCursor++;

            // 查找本地对应的AB包
            var localBundle = FindLocalBundle(remoteBundle.BundleName);

            if (localBundle == null)
            {
                // 本地清单无记录时，仍检查持久化目录是否已有完整文件（支持中断后续传）
                if (IsPersistentBundleValid(remoteBundle))
                {
                    m_CurrentResult.upToDateBundles.Add(remoteBundle);
                }
                else
                {
                    m_CurrentResult.bundlesToDownload.Add(remoteBundle);
                    m_CurrentResult.totalDownloadSize += remoteBundle.Size;
                }
            }
            else
            {
                if (!AssetBundlePathHelper.ExistsInPersistentData(remoteBundle.BundleName)
                    && !AssetBundlePathHelper.IsBundleAvailableAtRuntime(remoteBundle.BundleName))
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
        int totalBundles = m_TotalBundlesInStage;
        if (totalBundles > 0)
        {
            m_Progress = 0.5f + 0.2f * (m_CurrentBundleIndex / (float)totalBundles); // 对比占20%进度
        }

        m_CurrentOperation = $"对比清单文件... ({m_CurrentBundleIndex}/{totalBundles})";

        // 如果处理完成，进入下一状态
        if (m_RemainingCursor >= m_RemainingBundlesToCheck.Count)
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
        m_RemainingCursor = 0;
        m_TotalBundlesInStage = m_RemainingBundlesToCheck.Count;
        m_CurrentResult.bundlesToUpdate.Clear(); // 清空，后面重新添加
        m_CurrentBundleIndex = 0;
        m_Progress = 0.7f; // 验证阶段从70%开始
    }

    /// <summary>
    /// 执行文件验证（分帧）。
    /// 使用 GetRuntimeLoadPath：优先持久化，其次 Editor Build 输出，再次 StreamingAssets。
    /// </summary>
    private void ExecuteVerifyFiles()
    {
        if (m_RemainingBundlesToCheck == null || m_RemainingCursor >= m_RemainingBundlesToCheck.Count)
        {
            FinalizeResult();
            ChangeState(CheckerState.Complete);
            return;
        }

        float startTime = Time.realtimeSinceStartup;
        int processedCount = 0;

        while (m_RemainingCursor < m_RemainingBundlesToCheck.Count &&
               processedCount < bundlesPerFrame &&
               (Time.realtimeSinceStartup - startTime) < timeSlicePerFrame)
        {
            var remoteBundle = m_RemainingBundlesToCheck[m_RemainingCursor];
            m_RemainingCursor++;

            string localFilePath = AssetBundlePathHelper.GetRuntimeLoadPath(remoteBundle.BundleName);
            if (!File.Exists(localFilePath))
            {
                m_CurrentResult.bundlesToUpdate.Add(remoteBundle);
                m_CurrentResult.totalDownloadSize += remoteBundle.Size;
                processedCount++;
                m_CurrentBundleIndex++;
                continue;
            }

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
        int totalBundlesToVerify = m_TotalBundlesInStage;
        if (totalBundlesToVerify > 0)
        {
            m_Progress = 0.7f + 0.3f * (m_CurrentBundleIndex / (float)totalBundlesToVerify); // 验证占30%进度
        }

        m_CurrentOperation = $"验证文件完整性... ({m_CurrentBundleIndex}/{totalBundlesToVerify})";
    }

    /// <summary>
    /// 持久化目录中是否已有通过校验的完整 AB 包。
    /// </summary>
    private bool IsPersistentBundleValid(CustomAssetBundleInfo remoteBundle)
    {
        string persistentPath = AssetBundlePathHelper.GetLocalLZ4Path(remoteBundle.BundleName);
        if (!File.Exists(persistentPath))
        {
            return false;
        }

        return !CheckBundleIntegrity(persistentPath, remoteBundle);
    }

    /// <summary>
    /// 检查AB包完整性。返回 true 表示需要更新。
    /// CompressedFormat==0（LZMA）时，持久化目录中的文件已被再压缩为 LZ4，
    /// 不能再用远程 LZMA 的 Size/Hash 校验文件内容，改为比对本地清单记录的源 Hash。
    /// </summary>
    private bool CheckBundleIntegrity(string localFilePath, CustomAssetBundleInfo remoteBundle)
    {
        try
        {
            if (ShouldUseConvertedBundleVersionCheck(localFilePath))
            {
                return !IsConvertedPersistentBundleCurrent(localFilePath, remoteBundle);
            }

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
    /// LZMA 热更包会在下载后转成 LZ4 写入持久化目录；此时文件内容 Hash 与清单不一致。
    /// </summary>
    private bool ShouldUseConvertedBundleVersionCheck(string localFilePath)
    {
        if (m_RemoteManifest == null || m_RemoteManifest.CompressedFormat != 0)
        {
            return false;
        }

        return IsUnderPersistentDownloadDirectory(localFilePath);
    }

    private static bool IsUnderPersistentDownloadDirectory(string localFilePath)
    {
        if (string.IsNullOrEmpty(localFilePath))
        {
            return false;
        }

        string root = Path.GetFullPath(AssetBundlePathHelper.GetPersistentDownloadDirectory())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullPath = Path.GetFullPath(localFilePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string prefix = root + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 已转换的持久化 AB：用本地清单中的源 Hash 与远程比对版本，并确认文件可读。
    /// </summary>
    private bool IsConvertedPersistentBundleCurrent(string localFilePath, CustomAssetBundleInfo remoteBundle)
    {
        CustomAssetBundleInfo localBundle = FindLocalBundle(remoteBundle.BundleName);
        if (localBundle == null
            || !string.Equals(localBundle.Hash, remoteBundle.Hash, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!File.Exists(localFilePath))
        {
            return false;
        }

        FileInfo fileInfo = new FileInfo(localFilePath);
        return fileInfo.Length > 0;
    }

    /// <summary>
    /// 最终结果处理
    /// </summary>
    private void FinalizeResult()
    {
        // Pack 规划依赖远程清单，必须先赋值再计算合并下载。
        m_CurrentResult.customManifest = m_RemoteManifest;
        DownloadPackPlanner.ApplyPackDownloadPlan(m_CurrentResult);

        m_CurrentResult.hasChanges = m_CurrentResult.bundlesToDownload.Count > 0 ||
                                   m_CurrentResult.bundlesToUpdate.Count > 0;

        Debug.Log($"检查完成: 新增{m_CurrentResult.bundlesToDownload.Count}个, " +
                 $"更新{m_CurrentResult.bundlesToUpdate.Count}个, " +
                 $"最新{m_CurrentResult.upToDateBundles.Count}个, " +
                 $"合并包{m_CurrentResult.packsToDownload.Count}个, " +
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
        if (m_CurrentResult.customManifest == null)
        {
            m_CurrentResult.customManifest = m_RemoteManifest;
        }
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

    /// <summary>
    /// CRC 实际比的是文件 Size（历史命名保留）。
    /// </summary>
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

    /// <summary>MD5 比对；用于未再压缩的构建产物（LZ4 / StreamingAssets / Build 输出）。</summary>
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

    public static string ComputeFileMD5(string filePath)
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

    private CustomAssetBundleInfo FindLocalBundle(string bundleName)
    {
        if (m_LocalManifest?.AssetBundles == null) return null;
        return m_LocalManifest.AssetBundles.Find(b =>
            string.Equals(b.BundleName, bundleName, StringComparison.OrdinalIgnoreCase));
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

    /// <summary>
    /// 增量写入本地清单：仅把「已成功落盘」的 AB 记入本地。
    /// 用于下载中断后续传——下次启动可识别已完成的包，又不会把未下载的包误判为最新。
    /// </summary>
    public static void UpsertLocalBundles(CustomManifest remoteTemplate, IEnumerable<CustomAssetBundleInfo> completedBundles)
    {
        if (completedBundles == null)
        {
            return;
        }

        string manifestPath = Path.Combine(Application.persistentDataPath, "custom_manifest.json");
        CustomManifest local = null;
        if (File.Exists(manifestPath))
        {
            try
            {
                local = JsonUtility.FromJson<CustomManifest>(File.ReadAllText(manifestPath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("读取本地清单失败，将重建: " + e.Message);
            }
        }

        if (local == null)
        {
            local = new CustomManifest
            {
                AssetBundles = new List<CustomAssetBundleInfo>(),
            };
        }

        if (local.AssetBundles == null)
        {
            local.AssetBundles = new List<CustomAssetBundleInfo>();
        }

        // 同步远程版本头，便于后续对比；DownloadPacks 不在此写入（仅完整成功时全量保存）。
        if (remoteTemplate != null)
        {
            local.AppVersion = remoteTemplate.AppVersion;
            local.ManifestVersion = remoteTemplate.ManifestVersion;
            local.BuildTime = remoteTemplate.BuildTime;
            local.buildTime = remoteTemplate.buildTime;
            local.CompressedFormat = remoteTemplate.CompressedFormat;
        }

        Dictionary<string, int> indexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < local.AssetBundles.Count; i++)
        {
            CustomAssetBundleInfo existing = local.AssetBundles[i];
            if (existing != null && !string.IsNullOrEmpty(existing.BundleName))
            {
                indexByName[existing.BundleName] = i;
            }
        }

        int upsertCount = 0;
        foreach (CustomAssetBundleInfo bundle in completedBundles)
        {
            if (bundle == null || string.IsNullOrEmpty(bundle.BundleName))
            {
                continue;
            }

            CustomAssetBundleInfo copy = CloneBundleInfo(bundle);
            if (indexByName.TryGetValue(copy.BundleName, out int index))
            {
                local.AssetBundles[index] = copy;
            }
            else
            {
                indexByName[copy.BundleName] = local.AssetBundles.Count;
                local.AssetBundles.Add(copy);
            }

            upsertCount++;
        }

        if (upsertCount <= 0)
        {
            return;
        }

        File.WriteAllText(manifestPath, JsonUtility.ToJson(local, true));
        Debug.Log(string.Format("本地清单增量更新：写入/覆盖 {0} 个已完成 AB", upsertCount));
    }

    static CustomAssetBundleInfo CloneBundleInfo(CustomAssetBundleInfo source)
    {
        return new CustomAssetBundleInfo
        {
            AssetName = source.AssetName,
            BundleName = source.BundleName,
            Hash = source.Hash,
            Version = source.Version,
            Size = source.Size,
            Dependencies = source.Dependencies != null
                ? (string[])source.Dependencies.Clone()
                : null,
        };
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

#if UNITY_EDITOR
    /// <summary>Editor 测试模式：启动时预加载本地清单。</summary>
    public static bool TryApplyLocalManifestForStartup()
    {
        LoadMode loadMode = (LoadMode)EditorPrefs.GetInt("GameLoadMode", 0);
        if (loadMode != LoadMode.Testing)
        {
            return false;
        }

        if (!TryBootstrapTestingCheck(out UpdateCheckResult result))
        {
            return false;
        }

        AssetBundleManager.Instance.SetAssetBundleItem(result.customManifest);
        Debug.Log("[Testing] 启动时已注入本地 AB 清单，共 " + result.customManifest.AssetBundles.Count + " 个包。");
        return true;
    }

    /// <summary>
    /// Editor 测试模式：从 Build 输出 / StreamingAssets / 持久化目录加载清单，并检查 AB 是否可本地读取。
    /// </summary>
    public static bool TryBootstrapTestingCheck(out UpdateCheckResult result)
    {
        result = null;
        CustomManifest manifest = TryLoadTestingManifest();
        if (manifest?.AssetBundles == null || manifest.AssetBundles.Count == 0)
        {
            Debug.LogWarning("[Testing] 未找到本地 custom_manifest.json，将回退到远程清单检查。");
            return false;
        }

        result = new UpdateCheckResult
        {
            customManifest = manifest,
            isSuccess = true,
            VersionNumber = manifest.ManifestVersion.ToString(),
        };

        foreach (CustomAssetBundleInfo bundle in manifest.AssetBundles)
        {
            if (!AssetBundlePathHelper.IsBundleAvailableAtRuntime(bundle.BundleName))
            {
                result.bundlesToDownload.Add(bundle);
                result.totalDownloadSize += bundle.Size;
            }
            else
            {
                result.upToDateBundles.Add(bundle);
            }
        }

        result.hasChanges = result.bundlesToDownload.Count > 0;
        Debug.Log(string.Format(
            "[Testing] 本地清单加载成功：共 {0} 个包，可用 {1} 个，缺失 {2} 个。",
            manifest.AssetBundles.Count,
            result.upToDateBundles.Count,
            result.bundlesToDownload.Count));
        return true;
    }

    static CustomManifest TryLoadTestingManifest()
    {
        string buildManifestPath = AssetBundlePathHelper.ResolveLocalFilePath(
            AssetBundlePathHelper.GetServerLoadUrl().Replace("\\", "/") + "/custom_manifest.json");

        // Build 输出与 ab_runtime_config 一致，Editor 测试模式优先使用
        CustomManifest buildManifest = TryReadManifestFile(buildManifestPath);
        if (buildManifest != null)
        {
            Debug.Log(string.Format(
                "[Testing] 选用 Build 清单 ManifestVersion={0}，共 {1} 个包。",
                buildManifest.ManifestVersion,
                buildManifest.AssetBundles.Count));
            return buildManifest;
        }

        string[] fallbackCandidates =
        {
            AssetBundlePathHelper.GetStreamingAssetsManifestPath(),
            Path.Combine(Application.persistentDataPath, "custom_manifest.json"),
        };

        CustomManifest best = null;
        foreach (string candidate in fallbackCandidates)
        {
            CustomManifest manifest = TryReadManifestFile(candidate);
            if (manifest == null)
            {
                continue;
            }

            if (best == null || manifest.ManifestVersion > best.ManifestVersion)
            {
                best = manifest;
            }
        }

        if (best != null)
        {
            Debug.Log(string.Format(
                "[Testing] 选用备用清单 ManifestVersion={0}，共 {1} 个包。",
                best.ManifestVersion,
                best.AssetBundles.Count));
        }

        return best;
    }

    static CustomManifest TryReadManifestFile(string candidate)
    {
        if (string.IsNullOrEmpty(candidate) || !File.Exists(candidate))
        {
            return null;
        }

        try
        {
            CustomManifest manifest = JsonUtility.FromJson<CustomManifest>(File.ReadAllText(candidate));
            if (manifest?.AssetBundles != null && manifest.AssetBundles.Count > 0)
            {
                Debug.Log("[Testing] 读取清单: " + candidate);
                return manifest;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Testing] 读取清单失败: " + candidate + " -> " + ex.Message);
        }

        return null;
    }
#endif
}