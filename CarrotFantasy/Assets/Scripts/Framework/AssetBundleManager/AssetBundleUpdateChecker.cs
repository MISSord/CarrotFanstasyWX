using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

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

    /// <summary>LZMA 转 LZ4 之后落盘文件的大小；只有本地清单会填。</summary>
    public long ConvertedSize;
    /// <summary>LZMA 转 LZ4 之后落盘文件的 MD5；只有本地清单会填。</summary>
    public string ConvertedHash;
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
/// <summary>检查器内部状态。仅供本类实现使用；外部一律通过 UpdateFlow 观察流程。</summary>
internal enum CheckerState
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
/// <summary>
/// 职责划分（后续可拆成 4 个独立服务，当前先以注释标注归属）：
///   ManifestFetcher          拉取 + 验签 + 版本接受
///   LocalManifestStore       本地清单读写 + 客户端版本标记
///   ManifestDiffer           清单比对（帧内游标推进）
///   BundleIntegrityVerifier  Size/Hash/ConvertedHash 校验（无状态纯函数）
/// 
/// 关键约束：本类**不再对外暴露流程状态**。
/// CheckerState 已降级为 internal；"走到哪一步"由 UpdateFlow 统一描述。
/// </summary>
public class AssetBundleUpdateChecker
{
    public int bundlesPerFrame = 5;           // 每帧处理的AB包数量
    public float timeSlicePerFrame = 0.005f;  // 每帧最大处理时间(秒)

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
    internal CheckerState CurrentState => m_CurrentState;
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

        if (!ManifestSignature.IsAllowedServerUrl(remoteManifestUrl, out string urlError))
        {
            Debug.LogError(urlError);
            onComplete?.Invoke(new UpdateCheckResult
            {
                isSuccess = false,
                currentOperation = urlError,
            });
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
        const int maxAttempts = 3;
        CheckerState state = CheckerState.Error;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            bool needRetry = false;

            using (UnityWebRequest www = UnityWebRequest.Get(BuildCacheBustedUrl(m_RemoteManifestUrl)))
            {
                www.timeout = 15;
                var operation = www.SendWebRequest();

                while (!operation.isDone)
                {
                    m_Progress = operation.progress * 0.3f; // 下载占30%进度
                    yield return null;
                }

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"下载远程清单失败({attempt}/{maxAttempts}): {www.error}");
                    needRetry = true;
                }
                else
                {
                    byte[] manifestBytes = www.downloadHandler.data;
                    bool signatureOk = false;
                    string signatureError = null;
                    yield return DownloadAndVerifySignature(manifestBytes, (ok, error) =>
                    {
                        signatureOk = ok;
                        signatureError = error;
                    });

                    if (!signatureOk)
                    {
                        Debug.LogError(signatureError);
                        // 签名文件没下下来算网络问题，可重试；验签不通过重试也没意义。
                        needRetry = signatureError != null
                                    && signatureError.StartsWith("下载清单签名失败", StringComparison.Ordinal);
                    }
                    else
                    {
                        try
                        {
                            string json = www.downloadHandler.text;
                            CustomManifest remote = JsonUtility.FromJson<CustomManifest>(json);
                            if (remote == null)
                            {
                                Debug.LogError("解析远程清单失败: 结果为空");
                            }
                            else if (!AcceptManifestVersion(remote, out string versionError))
                            {
                                Debug.LogError(versionError);
                            }
                            else
                            {
                                m_RemoteManifest = remote;
                                Debug.Log($"远程清单加载成功，包含 {m_RemoteManifest.AssetBundles?.Count} 个AB包");
                                state = CheckerState.LoadingLocalManifest;
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"解析远程清单失败: {e.Message}");
                        }
                    }
                }
            }

            if (state == CheckerState.LoadingLocalManifest)
            {
                break;
            }

            if (!needRetry)
            {
                // 下载成功但内容/版本不合法，重试没有意义。
                break;
            }

            if (attempt < maxAttempts)
            {
                yield return new WaitForSecondsRealtime(attempt);
            }
        }

        ChangeState(state);
    }

    /// <summary>给清单/签名请求加时间戳，绕开 CDN 缓存；file:// 不加（会变成文件名的一部分）。</summary>
    static string BuildCacheBustedUrl(string url)
    {
        if (string.IsNullOrEmpty(url)
            || url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        string separator = url.IndexOf('?') >= 0 ? "&" : "?";
        return url + separator + "t=" + DateTime.UtcNow.Ticks;
    }

    private delegate void SignatureResult(bool ok, string error);

    /// <summary>
    /// 下载与清单原文配套的 .sig。编辑器 file:// 且没有签名文件时只警告，方便本地未签名目录调试。
    /// </summary>
    private IEnumerator DownloadAndVerifySignature(byte[] manifestBytes, SignatureResult onResult)
    {
        // 开发期跳过签名：连 .sig 都不去下载，省掉一次必然 404 的请求与重试。
        if (ManifestSignature.IsVerificationBypassed)
        {
            Debug.LogWarning(
                "[AssetBundleUpdateChecker] 开发宏 CF_DEV_SKIP_MANIFEST_SIGN 生效，跳过清单签名下载与校验。");
            onResult(true, null);
            yield break;
        }

        string signatureUrl = m_RemoteManifestUrl + ".sig";
        using (UnityWebRequest sigRequest = UnityWebRequest.Get(BuildCacheBustedUrl(signatureUrl)))
        {
            sigRequest.timeout = 10;
            yield return sigRequest.SendWebRequest();

            bool downloaded = sigRequest.result == UnityWebRequest.Result.Success
                && sigRequest.downloadHandler.data != null
                && sigRequest.downloadHandler.data.Length > 0;
            if (downloaded && ManifestSignature.Verify(manifestBytes, sigRequest.downloadHandler.data))
            {
                onResult(true, null);
                yield break;
            }

#if UNITY_EDITOR
            if (m_RemoteManifestUrl.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && !downloaded)
            {
                Debug.LogWarning("[AssetBundleUpdateChecker] 编辑器 file 清单没有签名，跳过验签: " + signatureUrl);
                onResult(true, null);
                yield break;
            }
#endif
            onResult(false, downloaded ? "清单签名校验失败" : "下载清单签名失败: " + sigRequest.error);
        }
    }

    /// <summary>拒绝比本地已接受版本更旧的清单。相等仍允许，以便同一版本里单个包更新。</summary>
    private static bool AcceptManifestVersion(CustomManifest remote, out string error)
    {
        int accepted = ReadAcceptedManifestVersion();
        if (remote.ManifestVersion < accepted)
        {
            error = "拒绝回滚清单: remote=" + remote.ManifestVersion + " local=" + accepted;
            return false;
        }

        error = null;
        return true;
    }

    private static int ReadAcceptedManifestVersion()
    {
        string manifestPath = Path.Combine(Application.persistentDataPath, "custom_manifest.json");
        if (!File.Exists(manifestPath))
        {
            return 0;
        }

        try
        {
            CustomManifest local = JsonUtility.FromJson<CustomManifest>(File.ReadAllText(manifestPath));
            return local != null ? local.ManifestVersion : 0;
        }
        catch (Exception e)
        {
            Debug.LogWarning("读取本地清单版本失败，按 0 处理: " + e.Message);
            return 0;
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

                // 客户端整包升级后，旧缓存里的热更 DLL / AB 可能与新 AOT 不兼容。
                // 直接丢弃并按全量重新校验，StreamingAssets 里的基线内容会被重新识别为"已有"。
                if (IsLocalManifestFromOtherAppVersion())
                {
                    Debug.LogWarning(
                        "检测到客户端整包版本变化（标记=" + ReadClientVersionMarker()
                        + ", 当前=" + Application.version + "），清空持久化缓存后重新校验。");
                    bool cleaned = AssetBundleCacheCleaner.ResetDownloadedAssetsForAppVersionChange();
                    m_LocalManifest = new CustomManifest { AssetBundles = new List<CustomAssetBundleInfo>() };

                    if (cleaned)
                    {
                        // 清理成功才写标记：本次下载中途失败也不会重复清库。
                        WriteClientVersionMarker();
                    }
                    else
                    {
                        // 清理失败时保留旧标记：下次启动会再尝试清理，避免旧 AB 盖住包内基线。
                        Debug.LogWarning("[AssetBundleUpdateChecker] 旧缓存未清理干净，保留版本标记以便下次启动重试");
                    }
                }
                else
                {
                    Debug.Log($"本地清单加载成功，包含 {m_LocalManifest.AssetBundles?.Count} 个AB包");
                }
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
    /// 判断"客户端是否换了个整包版本"，用于决定是否清空热更缓存。
    /// <para>
    /// 不能用清单里的 <c>AppVersion</c>：那是打包机写入、随 CDN 清单下发的服务端值，
    /// 与客户端 <c>Application.version</c> 比较只要不一致就会清库 —— 而清库后又把服务端值
    /// 存回本地，于是下次启动继续不一致，形成"每次启动清库并全量重下"的死循环。
    /// 这里改为比较客户端自己写下的版本标记（ab_client_version.txt）。
    /// </para>
    /// </summary>
    private static bool IsLocalManifestFromOtherAppVersion()
    {
        string marked = ReadClientVersionMarker();

        // 标记缺失 = 首次运行（或从没有标记的旧版本升级上来）。不做清库，
        // 宁可多留一份旧缓存，也不要陷入反复清库重下。
        if (string.IsNullOrEmpty(marked))
        {
            return false;
        }

        return !string.Equals(marked, Application.version, StringComparison.Ordinal);
    }

    private static string GetClientVersionMarkerPath()
    {
        return Path.Combine(Application.persistentDataPath, "ab_client_version.txt");
    }

    private static void WriteClientVersionMarker()
    {
        try
        {
            File.WriteAllText(GetClientVersionMarkerPath(), Application.version ?? string.Empty);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AssetBundleUpdateChecker] 写入客户端版本标记失败: " + e.Message);
        }
    }

    private static string ReadClientVersionMarker()
    {
        try
        {
            string path = GetClientVersionMarkerPath();
            if (!File.Exists(path))
            {
                return null;
            }

            return File.ReadAllText(path).Trim();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AssetBundleUpdateChecker] 读取客户端版本标记失败: " + e.Message);
            return null;
        }
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
                // 本地清单无记录时，先看持久化目录是否已有完整文件（支持中断后续传），
                // 再看包内 StreamingAssets 基线：首装时资源已随包分发，不应再全量下载一遍。
                if (IsPersistentBundleValid(remoteBundle))
                {
                    m_CurrentResult.upToDateBundles.Add(remoteBundle);
                }
                else if (AssetBundlePathHelper.IsBundleAvailableAtRuntime(remoteBundle.BundleName))
                {
                    // 文件在（基线或持久化目录），但清单里没记录，需按远端 Hash 校验后才算数。
                    m_CurrentResult.bundlesToUpdate.Add(remoteBundle);
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

        // 有"转换后记录"时按转换后的 Size + MD5 校验落盘文件；
        // 旧清单没有该字段，退化为"存在且非空"（下次转换后会自动补上）。
        if (!string.IsNullOrEmpty(localBundle.ConvertedHash) && localBundle.ConvertedSize > 0)
        {
            if (fileInfo.Length != localBundle.ConvertedSize)
            {
                return false;
            }

            string actual = ComputeFileMD5(localFilePath);
            return string.Equals(actual, localBundle.ConvertedHash, StringComparison.OrdinalIgnoreCase);
        }

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
        string manifestPath = Path.Combine(Application.persistentDataPath, "custom_manifest.json");
        try
        {
            // ConvertedHash / ConvertedSize 是本地专有字段，远程清单里没有；
            // 整份覆盖会抹掉历史转换记录，下次启动就退化成「文件非空即通过」。
            MergeConvertedInfoFromLocal(manifestPath, generatedManifest);

            WriteManifestAtomically(manifestPath, generatedManifest);
            ClearPendingLocalBundles();
            WriteClientVersionMarker();
            Debug.Log("完成服务器清单本地保存");
        }
        catch (Exception e)
        {
            Debug.LogError("保存本地清单失败: " + e.Message);
        }
    }

    /// <summary>
    /// 把旧本地清单里「源 Hash 与将要保存的清单一致」的转换记录合并回来。
    /// 源 Hash 不同说明远程版本变了，旧的转换结果作废，不能沿用。
    /// </summary>
    static void MergeConvertedInfoFromLocal(string manifestPath, CustomManifest target)
    {
        if (target == null || target.AssetBundles == null || !File.Exists(manifestPath))
        {
            return;
        }

        CustomManifest local;
        try
        {
            local = JsonUtility.FromJson<CustomManifest>(File.ReadAllText(manifestPath));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Manifest] 读取旧本地清单失败，跳过转换记录合并: " + e.Message);
            return;
        }

        if (local == null || local.AssetBundles == null)
        {
            return;
        }

        Dictionary<string, CustomAssetBundleInfo> localByName =
            new Dictionary<string, CustomAssetBundleInfo>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < local.AssetBundles.Count; i++)
        {
            CustomAssetBundleInfo info = local.AssetBundles[i];
            if (info != null && !string.IsNullOrEmpty(info.BundleName))
            {
                localByName[info.BundleName] = info;
            }
        }

        int merged = 0;
        for (int i = 0; i < target.AssetBundles.Count; i++)
        {
            CustomAssetBundleInfo remote = target.AssetBundles[i];
            if (remote == null || string.IsNullOrEmpty(remote.BundleName))
            {
                continue;
            }

            CustomAssetBundleInfo old;
            if (!localByName.TryGetValue(remote.BundleName, out old) || old == null)
            {
                continue;
            }

            // 源 Hash 必须一致：否则远程换了新版本，旧的转换结果已经作废。
            if (!string.Equals(old.Hash, remote.Hash, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrEmpty(old.ConvertedHash) || old.ConvertedSize <= 0)
            {
                continue;
            }

            // 内存里的值优先：本次刚转换过的包（RecordConvertedBundle 已写进 manifest）
            // 比磁盘上的旧记录更新。用旧值覆盖会导致下次启动拿旧 MD5 校验新文件，
            // 校验失败后重下、再被覆盖，来回打转。
            if (!string.IsNullOrEmpty(remote.ConvertedHash) && remote.ConvertedSize > 0)
            {
                continue;
            }

            remote.ConvertedHash = old.ConvertedHash;
            remote.ConvertedSize = old.ConvertedSize;
            merged++;
        }

        if (merged > 0)
        {
            Debug.Log($"[Manifest] 保留了 {merged} 个包的转换记录（ConvertedHash / ConvertedSize）");
        }
    }

    /// <summary>先写 .tmp 再原子替换，避免写到一半崩溃把本地清单写坏。</summary>
    static void WriteManifestAtomically(string manifestPath, CustomManifest manifest)
    {
        string json = JsonUtility.ToJson(manifest, true);
        string tempPath = manifestPath + ".tmp";

        // 用 AtomicFile 写入而非 File.WriteAllText：前者会 Flush(true) 落盘，
        // 否则掉电后可能出现"清单文件名对、内容 0 字节"。
        AtomicFile.WriteAllText(tempPath, json);
        AtomicFile.Replace(tempPath, manifestPath);
    }

    private static readonly Dictionary<string, CustomAssetBundleInfo> s_PendingBundles =
        new Dictionary<string, CustomAssetBundleInfo>(StringComparer.OrdinalIgnoreCase);
    private static CustomManifest s_PendingTemplate;
    private static float s_LastFlushTime = -999f;
    private const int PendingFlushCount = 32;
    private const float PendingFlushIntervalSeconds = 2f;

    /// <summary>
    /// 增量记录已落盘的 AB（带缓冲）。满 32 个或距上次落盘 2 秒才真正写文件，
    /// 避免"每完成一个包就整份重写 JSON"的 O(n²) 开销；崩溃最多丢这一小批记录，下次按 Hash 重校即可。
    /// </summary>
    /// <summary>
    /// 落盘完成后记录"转换后"的 Size + MD5。
    /// <para>
    /// 没有这一步，启动校验会因为 <c>ConvertedHash</c> 为空而退化成
    /// <see cref="IsPersistentBundleValid"/> 里的"文件非空"，完整性校验形同虚设。
    /// 单包下载路径（<c>PersistConvertedBundle</c>）本来就会记，Pack 路径原先漏了。
    /// </para>
    /// </summary>
    public static void RecordConvertedBundle(CustomManifest manifest, CustomAssetBundleInfo info, string finalPath)
    {
        if (info == null || string.IsNullOrEmpty(info.BundleName))
        {
            return;
        }

        try
        {
            if (string.IsNullOrEmpty(finalPath) || !File.Exists(finalPath))
            {
                return;
            }

            FileInfo fileInfo = new FileInfo(finalPath);
            info.ConvertedSize = fileInfo.Length;
            info.ConvertedHash = ComputeFileMD5(finalPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AssetBundleUpdateChecker] 记录转换后 Hash 失败: " + info.BundleName + ", " + e.Message);
            return;
        }

        UpsertLocalBundles(manifest, new[] { info });
    }

    public static void UpsertLocalBundles(CustomManifest remoteTemplate, IEnumerable<CustomAssetBundleInfo> completedBundles)
    {
        if (completedBundles == null)
        {
            return;
        }

        s_PendingTemplate = remoteTemplate;
        foreach (CustomAssetBundleInfo bundle in completedBundles)
        {
            if (bundle == null || string.IsNullOrEmpty(bundle.BundleName))
            {
                continue;
            }

            s_PendingBundles[bundle.BundleName] = CloneBundleInfo(bundle);
        }

        if (s_PendingBundles.Count >= PendingFlushCount
            || Time.realtimeSinceStartup - s_LastFlushTime >= PendingFlushIntervalSeconds)
        {
            FlushPendingLocalBundles();
        }
    }

    /// <summary>把缓冲中的已完成 AB 一次性合并进本地清单（原子写）。</summary>
    public static void FlushPendingLocalBundles()
    {
        s_LastFlushTime = Time.realtimeSinceStartup;
        if (s_PendingBundles.Count == 0)
        {
            return;
        }

        var completed = new List<CustomAssetBundleInfo>(s_PendingBundles.Values);
        s_PendingBundles.Clear();
        WriteLocalBundlesNow(s_PendingTemplate, completed);
    }

    public static void ClearPendingLocalBundles()
    {
        s_PendingBundles.Clear();
        s_PendingTemplate = null;
    }

    /// <summary>
    /// 增量写入本地清单：仅把「已成功落盘」的 AB 记入本地。
    /// 用于下载中断后续传——下次启动可识别已完成的包，又不会把未下载的包误判为最新。
    /// </summary>
    static void WriteLocalBundlesNow(CustomManifest remoteTemplate, IEnumerable<CustomAssetBundleInfo> completedBundles)
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

        // 半次下载只补已落盘的包。ManifestVersion 留到整次成功后的 SaveLocalManifest，避免把本地版本提前抬高。
        if (remoteTemplate != null)
        {
            local.AppVersion = remoteTemplate.AppVersion;
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

        WriteManifestAtomically(manifestPath, local);
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
            ConvertedSize = source.ConvertedSize,
            ConvertedHash = source.ConvertedHash,
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
        LoadMode loadMode = GameRuntimeInfo.CurrentLoadMode;
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