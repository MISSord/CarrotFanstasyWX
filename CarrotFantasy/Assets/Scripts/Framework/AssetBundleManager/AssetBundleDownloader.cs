using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
//using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;

public enum LoaderState
{
    None,
    Loading,
    Convert,
    Idle, //下载和解压全部AB包后方能进入该阶段
}

/// <summary>
/// AB 下载与解压转换。
///
/// StartDownload 分支：
/// - packsToDownload 非空 → 下 ZIP Pack，校验 Pack Size/MD5，解压后逐 AB 校验再落盘
/// - 否则 → 按 BundleName 逐个下载，校验 Size/MD5；CompressedFormat==0 时再压成 LZ4
///
/// 断点续传：每个 AB（或 Pack）真正落盘成功后 UpsertLocalBundles 增量写本地清单；
/// 全部成功后再 SaveLocalManifest 全量覆盖。未完成的包不会提前写入远程 Hash。
/// 二次校验比对的是「下载下来的源文件」（与清单一致），不是转换后的 LZ4。
/// 超时策略：以 stall（连续无字节增长）为主，绝对上限为辅；
/// UnityWebRequest.timeout 仅作兜底，避免卡死。
/// Pack 解压期间用 activePackOperations 阻止过早进入 Idle。
/// </summary>
public class AssetBundleDownloader
{
    private const string LogTag = "AssetBundleDownloader";
    private static AssetBundleDownloader _instance;

    public static AssetBundleDownloader Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new AssetBundleDownloader();
            }
            return _instance;
        }
    }

    private string finallDownloadUrl = string.Empty;
    private int maxConcurrentDownloads = 3; // 同时下载数量限制
    private int maxRetryCount = 3;
    /// <summary>连续无字节增长超过该秒数视为卡住（stall），触发重试。</summary>
    private const float StallTimeoutSeconds = 25f;
    /// <summary>单次请求绝对上限（秒），防止极端情况下永久挂起；真正判超时以 stall 为主。</summary>
    private const int AbsoluteMaxTimeoutSeconds = 1800;
    /// <summary>整批下载的安全上限；单个任务仍以 stall 超时为主判据。</summary>
    private const float BatchTimeoutSeconds = 600f;
    private LoaderState loaderState = LoaderState.None;
    /// <summary>关键 Pack（boot）失败：只有它为 true 才阻止进入游戏。</summary>
    private bool criticalPackFailed;

    private bool enableLogging = true;
    private List<DownloadTask> activeDownloads = new List<DownloadTask>();
    private Queue<DownloadTask> pendingDownloads = new Queue<DownloadTask>();
    /// <summary>失败后等待退避的任务，到点后再回 pendingDownloads。</summary>
    private readonly List<DownloadTask> pendingRetries = new List<DownloadTask>();

    private Queue<ConvertTask> pendingConverts = new Queue<ConvertTask>();
    private List<ConvertTask> activeConverts = new List<ConvertTask>();

    // 总下载进度与速度统计
    private long totalDownloadSize = 0;
    private long downloadedBytes = 0;
    private long completedDownloadBytes = 0;
    private float downloadSpeed = 0f;
    private float lastDownloadedBytes = 0f;
    private float lastSpeedUpdateTime = 0f;

    // 下载后二次校验用清单
    private Dictionary<string, CustomAssetBundleInfo> bundleInfoDict;
    private CustomManifest currentManifest;

    /// <summary>进行中的 Pack 解压/转换协程数；不为 0 时不能进入 Idle。</summary>
    private int activePackOperations;

    /// <summary>
    /// 下载任务类
    /// </summary>
    [System.Serializable]
    public class DownloadTask
    {
        public string bundleName;
        public string remoteURL;
        public string tempPath;
        public string finalPath;
        public UnityWebRequest webRequest;
        public float progress;
        public long downloadedBytes;
        public long totalBytes;
        public int retryCount;
        public DownloadStatus status;
        public string errorMessage;
        public System.Action<bool, string> callback;
        public bool isNeedConvert;
        public bool isPackDownload;
        public DownloadPackInfo packInfo;
        /// <summary>本次请求开始时间（unscaledTime），用于绝对上限。</summary>
        public float requestStartUnscaledTime;
        /// <summary>上次观察到字节增长的时间（unscaledTime），用于 stall 检测。</summary>
        public float lastProgressUnscaledTime;
        /// <summary>上次观察到的已下载字节，用于判断是否有进度。</summary>
        public long lastObservedDownloadedBytes;
        /// <summary>已计入 completedDownloadBytes 的字节数；失败重试前需回退，避免进度虚高。</summary>
        public long progressBytesCommitted;
        /// <summary>退避到点之前不再重新入队（unscaledTime）。</summary>
        public float retryNotBeforeUnscaledTime;
    }

    /// <summary>
    /// 转换任务类
    /// </summary>
    [System.Serializable]
    public class ConvertTask
    {
        public string bundleName;
        public string sourcePath;
        public string targetPath;
        public float progress;
        public ConvertStatus status;
        public string errorMessage;
        public System.Action<bool, string> callback;
        /// <summary>对应下载任务已计入进度的字节，转换失败时回退。</summary>
        public long progressBytesCommitted;
    }

    public enum DownloadStatus
    {
        Pending,
        Downloading,
        Completed,
        Failed,
        Retrying
    }

    public enum ConvertStatus
    {
        Pending,
        Converting,
        Completed,
        Failed
    }

    public System.Action<DownloadTask> OnDownloadProgress;
    public System.Action<string, AssetBundle> OnDownloadComplete;
    public System.Action<string, string> OnDownloadFailed;

    public void Init()
    {
        // 创建本地保存目录
        string fullPath = Path.Combine(Application.persistentDataPath, AssetBundlePathHelper.localSavePath);
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }
    }

    /// <summary>
    /// 开始批量下载（Pack 优先；未被 Pack 覆盖的 AB 走单包下载）。
    /// <para>
    /// <paramref name="onCompleted"/> 的触发时机**不是"下载完成"**：
    /// LZMA 模式（CompressedFormat == 0）下，每个包的 callback 会被转移给 ConvertTask，
    /// 所以本回调要等「下载 + LZMA→LZ4 转换」全部结束才触发；
    /// LZ4 / 无压缩模式才会在下载完成后立即触发。
    /// 参数 true/false 表示整批是否全部成功。
    /// </para>
    /// </summary>
    public void StartDownload(GameContext context, System.Action<bool> onCompleted)
    {
        loaderState = LoaderState.Loading;
        activePackOperations = 0;
        criticalPackFailed = false;

        UpdateCheckResult result = context.result;
        CustomManifest custom = result.customManifest;
        currentManifest = custom;
        finallDownloadUrl = AssetBundlePathHelper.GetServerLoadUrl();
        if (!ManifestSignature.IsAllowedServerUrl(finallDownloadUrl, out string urlError))
        {
            Debug.LogError(urlError);
            finallDownloadUrl = string.Empty;
            loaderState = LoaderState.Idle;
            onCompleted?.Invoke(false);
            return;
        }

        if (!HasEnoughDiskSpace(result?.totalDownloadSize ?? 0))
        {
            loaderState = LoaderState.Idle;
            onCompleted?.Invoke(false);
            return;
        }

        // 断点续传：每个包落盘成功时 UpsertLocalBundles；全部成功后再由 DownloadState 全量 SaveLocalManifest。
        // 不要在开始时写完整远程清单，否则未下载的包也会带上远程 Hash，LZMA 下会误判已最新。

        totalDownloadSize = result?.totalDownloadSize ?? 0;
        downloadedBytes = 0;
        completedDownloadBytes = 0;
        downloadSpeed = 0f;
        lastDownloadedBytes = 0f;
        lastSpeedUpdateTime = 0f;
        bundleInfoDict = new Dictionary<string, CustomAssetBundleInfo>(System.StringComparer.OrdinalIgnoreCase);
        if (result != null)
        {
            // 建立下载后二次校验用的清单映射（仅待下载/待更新的包）
            foreach (CustomAssetBundleInfo bundle in result.bundlesToDownload)
            {
                bundleInfoDict[bundle.BundleName] = bundle;
            }
            foreach (CustomAssetBundleInfo bundle in result.bundlesToUpdate)
            {
                bundleInfoDict[bundle.BundleName] = bundle;
            }

            // Pack 优先：Planner 已把差异 AB 映射为整 Pack；
            // 未被任何 Pack 覆盖的 AB 仍走单包下载，避免清单被手改 / Pack 构建被跳过时漏包。
            if (result.packsToDownload != null && result.packsToDownload.Count > 0)
            {
                string[] remaining = CollectBundlesNotCoveredByPacks(result, out long uncoveredSize);
                totalDownloadSize += uncoveredSize;
                this.DownloadPacks(result.packsToDownload.ToArray(), custom, null, onCompleted, remaining);
                return;
            }

            int totalBundles = result.bundlesToDownload.Count + result.bundlesToUpdate.Count;
            if (totalBundles <= 0)
            {
                loaderState = LoaderState.Idle;
                onCompleted?.Invoke(true);
                return;
            }

            //两个列表加起来等于要下载的
            string[] bundleStr = new string[totalBundles];
            int index = 0;
            List<CustomAssetBundleInfo> list = result.bundlesToDownload;
            for (int i = 0; i < result.bundlesToDownload.Count; i++)
            {
                bundleStr[index] = list[i].BundleName;
                index++;
            }
            list = result.bundlesToUpdate;
            for (int i = 0; i < result.bundlesToUpdate.Count; i++)
            {
                bundleStr[index] = list[i].BundleName;
                index++;
            }
            this.DownloadBundles(bundleStr, custom, null, onCompleted);
        }
        else
        {
            loaderState = LoaderState.Idle;
            onCompleted?.Invoke(false);
        }
    }

    /// <summary>Pack 未覆盖到的待更 AB（清单被手改、Pack 构建被跳过时兜底）。</summary>
    private static string[] CollectBundlesNotCoveredByPacks(UpdateCheckResult result, out long uncoveredSize)
    {
        uncoveredSize = 0;
        var covered = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        if (result.packsToDownload != null)
        {
            for (int i = 0; i < result.packsToDownload.Count; i++)
            {
                DownloadPackInfo pack = result.packsToDownload[i];
                if (pack?.BundleNames == null)
                {
                    continue;
                }

                for (int j = 0; j < pack.BundleNames.Length; j++)
                {
                    if (!string.IsNullOrEmpty(pack.BundleNames[j]))
                    {
                        covered.Add(pack.BundleNames[j]);
                    }
                }
            }
        }

        var remaining = new List<string>();
        AppendUncovered(result.bundlesToDownload, covered, remaining, ref uncoveredSize);
        AppendUncovered(result.bundlesToUpdate, covered, remaining, ref uncoveredSize);
        return remaining.ToArray();
    }

    private static void AppendUncovered(List<CustomAssetBundleInfo> bundles, HashSet<string> covered,
        List<string> output, ref long uncoveredSize)
    {
        if (bundles == null)
        {
            return;
        }

        for (int i = 0; i < bundles.Count; i++)
        {
            CustomAssetBundleInfo info = bundles[i];
            if (info == null || string.IsNullOrEmpty(info.BundleName))
            {
                continue;
            }

            if (covered.Contains(info.BundleName) || output.Contains(info.BundleName))
            {
                continue;
            }

            output.Add(info.BundleName);
            uncoveredSize += info.Size;
        }
    }

    /// <summary>
    /// 注入已签名清单：运行期按需下载时用它做 Size/MD5 校验与本地清单记录。
    /// </summary>
    public void SetManifest(CustomManifest manifest)
    {
        if (manifest?.AssetBundles == null)
        {
            return;
        }

        currentManifest = manifest;
        bundleInfoDict = new Dictionary<string, CustomAssetBundleInfo>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < manifest.AssetBundles.Count; i++)
        {
            CustomAssetBundleInfo info = manifest.AssetBundles[i];
            if (info != null && !string.IsNullOrEmpty(info.BundleName))
            {
                bundleInfoDict[info.BundleName] = info;
            }
        }
    }

    public void Update()
    {
        UpdateDownloads();
        UpdateConverts();
    }

    /// <summary>
    /// 更新下载任务状态
    /// </summary>
    private void UpdateDownloads()
    {
        // Idle/None 时若仍有任务（按需下载），自动进入 Loading。
        if (loaderState == LoaderState.None || loaderState == LoaderState.Idle)
        {
            if (pendingDownloads.Count == 0 && activeDownloads.Count == 0 && pendingRetries.Count == 0)
            {
                return;
            }

            loaderState = LoaderState.Loading;
        }

        long currentDownloaded = completedDownloadBytes;
        float nowUnscaled = Time.unscaledTime;

        // 检查正在下载的任务
        for (int i = activeDownloads.Count - 1; i >= 0; i--)
        {
            var task = activeDownloads[i];

            if (task.webRequest == null)
            {
                continue;
            }

            if (!task.webRequest.isDone)
            {
                // 先刷新进度，再做 stall / 绝对超时判断
                task.progress = task.webRequest.downloadProgress;
                task.downloadedBytes = (long)task.webRequest.downloadedBytes;
                UpdateDownloadProgressWatch(task, nowUnscaled);
                OnDownloadProgress?.Invoke(task);

                if (TryAbortTimedOutDownload(task, nowUnscaled))
                {
                    // 已在内部 Abort + 失败处理，本帧直接移除，避免下一帧再走 isDone 重复回调
                    DisposeWebRequest(task);
                    activeDownloads.RemoveAt(i);
                    continue;
                }
            }
            else
            {
                HandleDownloadCompletion(task);
                DisposeWebRequest(task);
                activeDownloads.RemoveAt(i);
            }

            currentDownloaded += task.downloadedBytes;
        }

        downloadedBytes = currentDownloaded;

        // 计算下载速度（每 0.5 秒更新一次）
        float now = Time.unscaledTime;
        float deltaTime = now - lastSpeedUpdateTime;
        if (deltaTime >= 0.5f)
        {
            downloadSpeed = Mathf.Max(0f, (currentDownloaded - lastDownloadedBytes) / deltaTime);
            lastDownloadedBytes = currentDownloaded;
            lastSpeedUpdateTime = now;
        }

        // 到点的重试任务回到待下载队列（失败退避，避免弱网下连打 CDN）。
        for (int i = pendingRetries.Count - 1; i >= 0; i--)
        {
            if (now >= pendingRetries[i].retryNotBeforeUnscaledTime)
            {
                pendingDownloads.Enqueue(pendingRetries[i]);
                pendingRetries.RemoveAt(i);
            }
        }

        // 启动新的下载任务
        while (activeDownloads.Count < maxConcurrentDownloads && pendingDownloads.Count > 0)
        {
            var task = pendingDownloads.Dequeue();
            StartDownloadTask(task);
        }

        // 下载队列清空后进入 Convert；Pack 解压也算在 Convert 阶段。
        if (loaderState == LoaderState.Loading && pendingDownloads.Count == 0 && activeDownloads.Count == 0)
        {
            loaderState = LoaderState.Convert;
        }
    }

    /// <summary>
    /// 更新转换任务状态
    /// </summary>
    private void UpdateConverts()
    {
        // 处于下载阶段时不解压，避免 IO 过大
        if (loaderState == LoaderState.None || loaderState == LoaderState.Loading)
        {
            return;
        }

        // 启动新的转换任务
        while (activeConverts.Count < 6 && pendingConverts.Count > 0) // 最多同时转换6个
        {
            var task = pendingConverts.Dequeue();
            StartConvertTask(task);
        }

        // 单 AB 转换与 Pack 解压都结束后才 Idle
        if (loaderState == LoaderState.Convert
            && activeConverts.Count == 0
            && pendingConverts.Count == 0
            && activePackOperations <= 0)
        {
            loaderState = LoaderState.Idle;
        }
    }

    /// <summary>
    /// 下载单个 AB 包。
    /// 按需下载时若 CDN URL 尚未初始化，会自动从 PathHelper 补齐。
    /// </summary>
    public void DownloadBundle(string bundleName, bool isNeedConvert, System.Action<bool, string> callback = null)
    {
        if (IsBundleAlreadyInQueue(bundleName))
        {
            callback?.Invoke(false, "Bundle is already in download queue");
            return;
        }

        EnsureDownloadUrlReady();
        if (!ManifestSignature.IsAllowedServerUrl(finallDownloadUrl, out string urlError))
        {
            callback?.Invoke(false, urlError);
            return;
        }

        long knownSize = 0;
        if (bundleInfoDict != null
            && bundleInfoDict.TryGetValue(bundleName, out CustomAssetBundleInfo info)
            && info != null)
        {
            knownSize = info.Size;
        }

        // 一律先下到「正式文件同目录」的临时文件：
        // 1) 校验通过后才原子替换，失败不碰旧文件；
        // 2) 必须同卷改名——AtomicFile.Replace 依赖 File.Replace，跨卷会直接失败。
        string finalPath = AssetBundlePathHelper.GetLocalLZ4Path(bundleName);
        var downloadTask = new DownloadTask
        {
            bundleName = bundleName,
            remoteURL = $"{finallDownloadUrl}/{bundleName}",
            tempPath = finalPath + ".download",
            finalPath = finalPath,
            status = DownloadStatus.Pending,
            callback = callback,
            isNeedConvert = isNeedConvert,
            totalBytes = knownSize,
        };

        pendingDownloads.Enqueue(downloadTask);
        if (loaderState == LoaderState.None || loaderState == LoaderState.Idle)
        {
            loaderState = LoaderState.Loading;
        }

        Log($"AB包 {bundleName} 已加入下载队列，当前位置: {pendingDownloads.Count}");
    }

    public void DownloadPack(DownloadPackInfo packInfo, System.Action<bool, string> callback = null)
    {
        if (packInfo == null)
        {
            callback?.Invoke(false, "Pack 信息为空");
            return;
        }

        if (IsPackAlreadyInQueue(packInfo.PackName))
        {
            callback?.Invoke(false, "Pack is already in download queue");
            return;
        }

        EnsureDownloadUrlReady();
        if (!ManifestSignature.IsAllowedServerUrl(finallDownloadUrl, out string urlError))
        {
            callback?.Invoke(false, urlError);
            return;
        }

        var downloadTask = new DownloadTask
        {
            bundleName = packInfo.PackName,
            packInfo = packInfo,
            isPackDownload = true,
            remoteURL = $"{finallDownloadUrl}/{packInfo.PackFileName}",
            tempPath = Path.Combine(Application.temporaryCachePath, packInfo.PackName + ".zip"),
            finalPath = string.Empty,
            status = DownloadStatus.Pending,
            callback = callback,
            isNeedConvert = false,
            totalBytes = packInfo.PackSize,
        };

        pendingDownloads.Enqueue(downloadTask);
        if (loaderState == LoaderState.None || loaderState == LoaderState.Idle)
        {
            loaderState = LoaderState.Loading;
        }

        Log($"下载 Pack {packInfo.PackName} 已加入队列，包含 {packInfo.BundleNames?.Length ?? 0} 个 AB");
    }

    /// <summary>下载前粗查磁盘：按「下载 + 解压/转换」2 倍估算，再留 64MB 余量。</summary>
    private static bool HasEnoughDiskSpace(long downloadBytes)
    {
        if (downloadBytes <= 0)
        {
            return true;
        }

        try
        {
            long required = downloadBytes * 2 + 64L * 1024 * 1024;
            string root = Path.GetPathRoot(Path.GetFullPath(Application.persistentDataPath));
            if (string.IsNullOrEmpty(root))
            {
                return true;
            }

            var drive = new DriveInfo(root);
            if (!drive.IsReady)
            {
                return true;
            }

            if (drive.AvailableFreeSpace < required)
            {
                Debug.LogError(
                    "磁盘空间不足：需要约 " + (required / (1024 * 1024)) + "MB，可用 "
                    + (drive.AvailableFreeSpace / (1024 * 1024)) + "MB，请清理后重试。");
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("磁盘空间检查失败，跳过: " + e.Message);
        }

        return true;
    }

    /// <summary>按需下载时补齐 CDN 根 URL（热更 StartDownload 之外的入口也会用到）。</summary>
    private void EnsureDownloadUrlReady()
    {
        if (!string.IsNullOrEmpty(finallDownloadUrl))
        {
            return;
        }

        finallDownloadUrl = AssetBundlePathHelper.GetServerLoadUrl();
        if (!ManifestSignature.IsAllowedServerUrl(finallDownloadUrl, out string urlError))
        {
            Debug.LogError(urlError);
            finallDownloadUrl = string.Empty;
        }
    }

    public void DownloadPacks(DownloadPackInfo[] packs, CustomManifest custom, System.Action<int, int> progressCallback = null,
        System.Action<bool> completeCallback = null, string[] remainingBundles = null)
    {
        SRPScheduler.StartRunCoroutine(DownloadPacksCoroutine(packs, progressCallback, completeCallback, remainingBundles));
    }

    private IEnumerator DownloadPacksCoroutine(DownloadPackInfo[] packs, System.Action<int, int> progressCallback,
        System.Action<bool> completeCallback, string[] remainingBundles)
    {
        int completedCount = 0;
        int totalCount = packs.Length;
        bool allSuccess = true;

        Log($"开始批量下载 {totalCount} 个 Pack");

        foreach (DownloadPackInfo pack in packs)
        {
            DownloadPack(pack, (success, message) =>
            {
                if (!success)
                {
                    if (pack.Critical)
                    {
                        criticalPackFailed = true;
                        LogError($"关键 Pack 下载失败: {pack.PackName}, {message}");
                    }
                    else
                    {
                        // 非关键包失败不阻止进游戏，缺的资源由按需下载补。
                        GameLogController.Warning($"非关键 Pack 下载失败，进入游戏后按需补: {pack.PackName}, {message}", LogTag);
                    }
                }

                completedCount++;
                progressCallback?.Invoke(completedCount, totalCount);
                Log($"Pack 下载处理完成: {pack.PackName} ({completedCount}/{totalCount})");
            });
        }

        float deadline = Time.realtimeSinceStartup + BatchTimeoutSeconds;
        while (completedCount < totalCount && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        if (completedCount < totalCount)
        {
            LogError($"Pack 批量下载超时: {completedCount}/{totalCount}");
            allSuccess = false;
        }

        // Pack 覆盖不到的 AB 继续按单包下载；这部分失败只降级，不算致命。
        if (remainingBundles != null && remainingBundles.Length > 0)
        {
            bool remainderSuccess = false;
            bool needConvert = currentManifest != null && currentManifest.CompressedFormat == 0;
            yield return DownloadBundlesCoroutine(remainingBundles, needConvert, progressCallback, r => remainderSuccess = r);
            if (!remainderSuccess)
            {
                GameLogController.Warning("部分补充包下载失败，进入游戏后按需重试", LogTag);
            }
        }

        AssetBundleUpdateChecker.FlushPendingLocalBundles();

        Log($"批量 Pack 下载完成: 成功 {completedCount}/{totalCount}");
        completeCallback?.Invoke(allSuccess && !criticalPackFailed);
    }

    /// <summary>
    /// 批量下载并转换 AB。
    /// CompressedFormat==0（LZMA）时 isNeedConvert=true：先下到 temp，校验通过后再 Recompress 为 LZ4。
    /// </summary>
    public void DownloadBundles(string[] bundleNames, CustomManifest custom, System.Action<int, int> progressCallback = null,
        System.Action<bool> completeCallback = null)
    {
        bool isNeedConvert = custom.CompressedFormat == 0;
        SRPScheduler.StartRunCoroutine(DownloadBundlesCoroutine(bundleNames, isNeedConvert, progressCallback, completeCallback));
    }

    /// <summary>
    /// 批量下载协程
    /// </summary>
    private IEnumerator DownloadBundlesCoroutine(string[] bundleNames, bool isNeedConvert, System.Action<int, int> progressCallback,
        System.Action<bool> completeCallback)
    {
        int completedCount = 0;
        int totalCount = bundleNames.Length;
        bool allSuccess = true;

        Log($"开始批量下载 {totalCount} 个AB包");

        // 创建完成标记字典
        var completionFlags = new Dictionary<string, bool>();
        foreach (var bundleName in bundleNames)
        {
            completionFlags[bundleName] = false;
        }

        // 添加所有下载任务
        foreach (string bundleName in bundleNames)
        {
            bool convertThis = isNeedConvert;
            DownloadBundle(bundleName, convertThis, (success, message) =>
            {
                completionFlags[bundleName] = true;
                if (!success) allSuccess = false;
                completedCount++;
                progressCallback?.Invoke(completedCount, totalCount);
                Log($"下载与解压完成: {bundleName} ({completedCount}/{totalCount})");
            });
        }

        float deadline = Time.realtimeSinceStartup + BatchTimeoutSeconds;
        while (completedCount < totalCount && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        if (completedCount < totalCount)
        {
            LogError($"AB 批量下载超时: {completedCount}/{totalCount}");
            allSuccess = false;
        }

        AssetBundleUpdateChecker.FlushPendingLocalBundles();

        Log($"批量下载完成: 成功 {completedCount}/{totalCount}");
        completeCallback?.Invoke(allSuccess);
    }

    /// <summary>
    /// 开始下载任务。
    /// 主超时：stall（连续无字节增长）；辅超时：绝对上限。UWR.timeout 仅作兜底。
    /// </summary>
    private void StartDownloadTask(DownloadTask task)
    {
        task.status = DownloadStatus.Downloading;
        activeDownloads.Add(task);

        float now = Time.unscaledTime;
        task.requestStartUnscaledTime = now;
        task.lastProgressUnscaledTime = now;
        task.lastObservedDownloadedBytes = 0;
        task.downloadedBytes = 0;

        Log(string.Format(
            "开始下载: {0}, 大小={1}B, stall={2}s, 绝对上限={3}s",
            task.bundleName,
            task.totalBytes,
            StallTimeoutSeconds,
            AbsoluteMaxTimeoutSeconds));

        string targetDir = Path.GetDirectoryName(task.tempPath);
        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        task.webRequest = UnityWebRequest.Get(task.remoteURL);
        // UWR 绝对超时仅作兜底；真正判超时在 UpdateDownloads 的 stall 检测。
        task.webRequest.timeout = AbsoluteMaxTimeoutSeconds;
        task.webRequest.downloadHandler = new DownloadHandlerFile(task.tempPath);
        task.webRequest.SendWebRequest();
    }

    /// <summary>有字节增长则刷新 stall 计时。</summary>
    private static void UpdateDownloadProgressWatch(DownloadTask task, float nowUnscaled)
    {
        if (task.downloadedBytes > task.lastObservedDownloadedBytes)
        {
            task.lastObservedDownloadedBytes = task.downloadedBytes;
            task.lastProgressUnscaledTime = nowUnscaled;
        }
    }

    /// <summary>
    /// stall 或绝对上限触发时 Abort 并走失败重试。返回 true 表示本任务已结束处理。
    /// </summary>
    private bool TryAbortTimedOutDownload(DownloadTask task, float nowUnscaled)
    {
        float stallElapsed = nowUnscaled - task.lastProgressUnscaledTime;
        if (stallElapsed >= StallTimeoutSeconds)
        {
            string error = string.Format(
                "stall timeout: {0:F1}s 无进度增长 (已下 {1}B / {2}B)",
                stallElapsed,
                task.downloadedBytes,
                task.totalBytes);
            LogError($"下载超时({task.bundleName}): {error}");
            try
            {
                task.webRequest.Abort();
            }
            catch (Exception e)
            {
                LogError($"Abort 失败: {e.Message}");
            }

            // 先释放请求（关闭文件句柄）再删暂存文件，避免 Windows 上删不掉。
            DisposeWebRequest(task);
            HandleDownloadFailure(task, error);
            return true;
        }

        float absoluteElapsed = nowUnscaled - task.requestStartUnscaledTime;
        if (absoluteElapsed >= AbsoluteMaxTimeoutSeconds)
        {
            string error = string.Format(
                "absolute timeout: 已持续 {0:F0}s (已下 {1}B / {2}B)",
                absoluteElapsed,
                task.downloadedBytes,
                task.totalBytes);
            LogError($"下载超时({task.bundleName}): {error}");
            try
            {
                task.webRequest.Abort();
            }
            catch (Exception e)
            {
                LogError($"Abort 失败: {e.Message}");
            }

            // 先释放请求（关闭文件句柄）再删暂存文件，避免 Windows 上删不掉。
            DisposeWebRequest(task);
            HandleDownloadFailure(task, error);
            return true;
        }

        return false;
    }

    //这个代码可以实现支持断点传输，先屏蔽
    //public IEnumerator DownloadAssetBundleWithResume(System.Action<bool, string> onCompleted = null)
    //{
    //    string localSavePath = Path.Combine(Application.persistentDataPath, localSaveDirectory);

    //    // 确保保存目录存在
    //    if (!Directory.Exists(localSavePath))
    //    {
    //        Directory.CreateDirectory(localSavePath);
    //    }

    //    string tempFilePath = Path.Combine(localSavePath, localFileName + ".temp");
    //    string finalFilePath = Path.Combine(localSavePath, localFileName);

    //    // 1. 检查已下载的临时文件大小，用于断点续传
    //    long existingBytes = 0;
    //    if (File.Exists(tempFilePath))
    //    {
    //        FileInfo fileInfo = new FileInfo(tempFilePath);
    //        existingBytes = fileInfo.Length;
    //        Debug.Log($"发现未完成的下载，已下载: {existingBytes} 字节");
    //    }

    //    // 2. 创建UnityWebRequest
    //    using (UnityWebRequest www = new UnityWebRequest(bundleUrl, UnityWebRequest.kHttpVerbGET))
    //    {
    //        // 3. 创建预分配缓冲区（例如32KB）和自定义DownloadHandler
    //        byte[] preallocatedBuffer = new byte[32 * 1024]; // 32KB缓冲区
    //        FileDownloadHandler downloadHandler = new FileDownloadHandler(tempFilePath, finalFilePath, preallocatedBuffer);
    //        www.downloadHandler = downloadHandler;

    //        // 4. 设置Range请求头以实现断点续传
    //        if (existingBytes > 0)
    //        {
    //            www.SetRequestHeader("Range", $"bytes={existingBytes}-");
    //            Debug.Log($"设置Range头: bytes={existingBytes}-");
    //        }

    //        // 5. 发送请求
    //        www.SendWebRequest();
    //        Debug.Log("开始下载AssetBundle...");

    //        // 6. 等待下载完成，并更新进度
    //        while (!www.isDone)
    //        {
    //            float progress = downloadHandler.GetProgress();
    //            Debug.Log($"下载进度: {progress * 100:F2}%");
    //            yield return null;
    //        }

    //        // 7. 处理下载结果
    //        if (www.result != UnityWebRequest.Result.Success)
    //        {
    //            Debug.LogError($"下载失败: {www.error}");
    //            onCompleted?.Invoke(false, www.error);
    //        }
    //        else
    //        {
    //            Debug.Log("AssetBundle下载并保存成功！");
    //            onCompleted?.Invoke(true, finalFilePath);
    //        }

    //        // 确保自定义DownloadHandler被正确释放
    //        downloadHandler.Dispose();
    //    }
    //}

    /// <summary>
    /// 处理下载完成
    /// </summary>
    private void HandleDownloadCompletion(DownloadTask task)
    {
#if UNITY_2020_3_OR_NEWER
        bool success = task.webRequest.result == UnityWebRequest.Result.Success;
#else
        bool success = !task.webRequest.isNetworkError && !task.webRequest.isHttpError;
#endif
        if (!success)
        {
            string error = task.webRequest.error;
            // 先释放请求句柄，再删暂存文件，避免 Windows 上删不掉。
            DisposeWebRequest(task);
            HandleDownloadFailure(task, error);
        }
        else
        {
            HandleDownloadSuccess(task);
        }
    }

    /// <summary>
    /// 处理下载成功。
    /// 单 AB：VerifyDownloadedFile(Size+MD5) → 可选入队 LZ4 转换。
    /// Pack：走 HandlePackDownloadSuccessCoroutine。
    /// </summary>
    private void HandleDownloadSuccess(DownloadTask task)
    {
        if (task.isPackDownload)
        {
            SRPScheduler.StartRunCoroutine(HandlePackDownloadSuccessCoroutine(task));
            return;
        }

        long fileSize = 0;
        if (File.Exists(task.tempPath))
        {
            fileSize = new FileInfo(task.tempPath).Length;
        }

        // 下载后二次校验（大小 + MD5）——校验对象是源文件，与清单 Hash 一致
        if (!VerifyDownloadedFile(task.bundleName, task.tempPath, out string verifyError))
        {
            LogError($"下载文件校验失败: {task.bundleName}, {verifyError}");
            HandleDownloadFailure(task, verifyError);
            return;
        }

        Log($"下载成功: {task.bundleName}, 文件大小: {fileSize} bytes");
        CommitDownloadProgress(task, fileSize);

        if (task.isNeedConvert == true)
        {
            // 创建转换任务
            var convertTask = new ConvertTask
            {
                bundleName = task.bundleName,
                sourcePath = task.tempPath,
                targetPath = task.finalPath,
                status = ConvertStatus.Pending,
                callback = task.callback,
                progressBytesCommitted = task.progressBytesCommitted,
            };

            pendingConverts.Enqueue(convertTask);
        }
        else
        {
            try
            {
                AtomicFile.Replace(task.tempPath, task.finalPath);
            }
            catch (System.Exception e)
            {
                LogError($"替换正式文件失败: {task.bundleName}, {e.Message}");
                HandleDownloadFailure(task, e.Message);
                return;
            }

            PersistCompletedBundle(task.bundleName);
            Log(string.Format("{0}下载完成，无需解压", task.bundleName));
            task.callback?.Invoke(true, "");
        }

        task.status = DownloadStatus.Completed;

        OnDownloadProgress?.Invoke(task);
    }

    /// <summary>
    /// Pack：先 VerifyPackFile，再 ExtractPackCoroutine（内含逐 AB Size/MD5，可选转 LZ4）。
    /// 解压期间计入 activePackOperations，避免 LoaderState 过早 Idle。
    /// </summary>
    private IEnumerator HandlePackDownloadSuccessCoroutine(DownloadTask task)
    {
        activePackOperations++;

        long fileSize = 0;
        if (File.Exists(task.tempPath))
        {
            fileSize = new FileInfo(task.tempPath).Length;
        }

        if (!AssetBundlePackExtractor.VerifyPackFile(task.tempPath, task.packInfo, out string verifyError))
        {
            LogError($"Pack 校验失败: {task.packInfo.PackName}, {verifyError}");
            HandleDownloadFailure(task, verifyError);
            activePackOperations = Mathf.Max(0, activePackOperations - 1);
            yield break;
        }

        Log($"Pack 下载成功: {task.packInfo.PackName}, 文件大小: {fileSize} bytes");
        CommitDownloadProgress(task, fileSize);

        bool isNeedConvert = currentManifest != null && currentManifest.CompressedFormat == 0;
        bool extractSuccess = false;
        string extractError = string.Empty;
        yield return AssetBundlePackExtractor.ExtractPackCoroutine(
            task.tempPath,
            task.packInfo,
            bundleInfoDict,
            isNeedConvert,
            currentManifest,
            (success, error) =>
            {
                extractSuccess = success;
                extractError = error;
            });

        TryDeleteFile(task.tempPath);

        if (extractSuccess)
        {
            // Pack 内已落盘的 AB 增量写入本地清单。
            PersistCompletedBundles(task.packInfo.BundleNames);
            task.status = DownloadStatus.Completed;
            task.callback?.Invoke(true, string.Empty);
            OnDownloadProgress?.Invoke(task);
        }
        else
        {
            // HandleDownloadFailure 内会回退已计入进度，避免重试后总量虚高。
            HandleDownloadFailure(task, string.IsNullOrEmpty(extractError) ? "Pack 解压失败" : extractError);
        }

        activePackOperations = Mathf.Max(0, activePackOperations - 1);
    }

    /// <summary>
    /// 删除下载产生的临时/残缺文件，避免下次启动误用。
    /// </summary>
    private void DeleteDownloadArtifacts(DownloadTask task)
    {
        if (task == null)
        {
            return;
        }

        TryDeleteFile(task.tempPath);
        TryDeleteFile(task.finalPath + ".new");
    }

    private static void TryDeleteFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return;
        }

        try
        {
            File.Delete(filePath);
        }
        catch (Exception e)
        {
            GameLogController.Warning($"删除残缺文件失败: {filePath}, {e.Message}", LogTag);
        }
    }

    /// <summary>
    /// 处理下载失败
    /// </summary>
    private void HandleDownloadFailure(DownloadTask task, string error)
    {
        RollbackDownloadProgress(task);
        DeleteDownloadArtifacts(task);
        task.retryCount++;

        if (task.retryCount < maxRetryCount)
        {
            // 指数退避 + 抖动：弱网下不要连着打三次 CDN。
            float delay = Mathf.Min(30f, Mathf.Pow(2f, Mathf.Max(0, task.retryCount - 1)))
                          + UnityEngine.Random.Range(0f, 1f);
            task.retryNotBeforeUnscaledTime = Time.unscaledTime + delay;

            Log($"下载失败，{delay:F1}s 后重试: {task.bundleName} ({task.retryCount}/{maxRetryCount})");
            task.status = DownloadStatus.Retrying;
            task.errorMessage = error;
            task.downloadedBytes = 0;
            task.lastObservedDownloadedBytes = 0;
            task.progressBytesCommitted = 0;

            pendingRetries.Add(task);
        }
        else
        {
            string errorMsg = $"下载 {task.bundleName} 失败，已达到最大重试次数: {error}";
            LogError(errorMsg);

            task.status = DownloadStatus.Failed;
            task.errorMessage = errorMsg;

            if (task.isPackDownload && task.packInfo != null && task.packInfo.Critical)
            {
                criticalPackFailed = true;
            }

            OnDownloadFailed?.Invoke(task.bundleName, errorMsg);
            task.callback?.Invoke(false, errorMsg);
        }

        OnDownloadProgress?.Invoke(task);
    }

    /// <summary>将本次下载字节计入总进度（同一任务只计一次）。</summary>
    private void CommitDownloadProgress(DownloadTask task, long bytes)
    {
        if (task == null || bytes <= 0 || task.progressBytesCommitted > 0)
        {
            return;
        }

        task.progressBytesCommitted = bytes;
        completedDownloadBytes += bytes;
    }

    /// <summary>失败/重试前回退已计入的进度字节。</summary>
    private void RollbackDownloadProgress(DownloadTask task)
    {
        if (task == null || task.progressBytesCommitted <= 0)
        {
            return;
        }

        completedDownloadBytes = Math.Max(0, completedDownloadBytes - task.progressBytesCommitted);
        task.progressBytesCommitted = 0;
    }

    /// <summary>
    /// 开始转换任务
    /// </summary>
    private void StartConvertTask(ConvertTask task)
    {
        task.status = ConvertStatus.Converting;
        activeConverts.Add(task);

        Log($"开始格式转换: {task.bundleName}");

        // 使用协程进行转换，但不嵌套在其他协程中
        SRPScheduler.StartRunCoroutine(ConvertBundleCoroutine(task));
    }

    /// <summary>
    /// LZMA → LZ4Runtime。成功后 LoadFromFile 验一次；转换后文件 Hash 与清单不同，启动校验见 UpdateChecker。
    /// </summary>
    private IEnumerator ConvertBundleCoroutine(ConvertTask task)
    {
        // 配置LZ4压缩方法
        BuildCompression lz4CompressionMethod = BuildCompression.LZ4Runtime;

        string path = Path.GetDirectoryName(task.targetPath);
        if (Directory.Exists(path) == false)
        {
            Directory.CreateDirectory(path);
        }

        string stagedPath = task.targetPath + ".new";
        TryDeleteFile(stagedPath);
        var recompressOperation = AssetBundle.RecompressAssetBundleAsync(task.sourcePath, stagedPath, lz4CompressionMethod, 0, ThreadPriority.Normal);

        yield return recompressOperation;

        bool success = recompressOperation.success;
        string errorMessage = "";

        if (File.Exists(task.sourcePath))
        {
            File.Delete(task.sourcePath);
        }

        if (success)
        {
            AssetBundle bundle = AssetBundle.LoadFromFile(stagedPath);
            if (bundle == null)
            {
                errorMessage = "转换后的AB包加载失败";
                success = false;
            }
            else
            {
                OnDownloadComplete?.Invoke(task.bundleName, bundle);
                bundle.Unload(false);
                try
                {
                    AtomicFile.Replace(stagedPath, task.targetPath);
                    task.status = ConvertStatus.Completed;
                    task.progress = 1f;
                    PersistConvertedBundle(task.bundleName);
                    task.callback?.Invoke(true, "Download and convert successful");
                    Log($"AB包 {task.bundleName} 转换完成");
                }
                catch (System.Exception e)
                {
                    errorMessage = "替换正式文件失败: " + e.Message;
                    success = false;
                }
            }
        }
        else
        {
            errorMessage = "格式转换失败";
        }

        if (!success)
        {
            if (task.progressBytesCommitted > 0)
            {
                completedDownloadBytes = Math.Max(0, completedDownloadBytes - task.progressBytesCommitted);
                task.progressBytesCommitted = 0;
            }

            TryDeleteFile(stagedPath);
            task.status = ConvertStatus.Failed;
            task.errorMessage = errorMessage;
            task.callback?.Invoke(false, errorMessage);
            OnDownloadFailed?.Invoke(task.bundleName, errorMessage);
        }

        // 从活动列表移除
        activeConverts.Remove(task);
    }

    /// <summary>
    /// 检查AB包是否已在队列中
    /// </summary>
    private bool IsBundleAlreadyInQueue(string bundleName)
    {
        foreach (var task in activeDownloads)
        {
            if (task.bundleName == bundleName) return true;
        }

        foreach (var task in pendingDownloads)
        {
            if (task.bundleName == bundleName) return true;
        }

        foreach (var task in activeConverts)
        {
            if (task.bundleName == bundleName) return true;
        }

        foreach (var task in pendingConverts)
        {
            if (task.bundleName == bundleName) return true;
        }

        return false;
    }

    private bool IsPackAlreadyInQueue(string packName)
    {
        foreach (DownloadTask task in activeDownloads)
        {
            if (task.isPackDownload && task.packInfo != null && task.packInfo.PackName == packName)
            {
                return true;
            }
        }

        foreach (DownloadTask task in pendingDownloads)
        {
            if (task.isPackDownload && task.packInfo != null && task.packInfo.PackName == packName)
            {
                return true;
            }
        }

        return false;
    }

    ///// <summary>
    ///// 检查本地AB包是否为最新版本
    ///// </summary>
    //private bool IsLocalBundleUpToDate(string bundleName)
    //{
    //    string localPath = AssetBundlePathHelper.GetLocalLZ4Path(bundleName);
    //    return File.Exists(localPath); // 简化实现
    //}

    ///// <summary>
    ///// 获取下载队列状态
    ///// </summary>
    //public DownloadQueueStatus GetQueueStatus()
    //{
    //    return new DownloadQueueStatus
    //    {
    //        activeDownloads = activeDownloads.Count,
    //        pendingDownloads = pendingDownloads.Count,
    //        activeConverts = activeConverts.Count,
    //        pendingConverts = pendingConverts.Count
    //    };
    //}

    ///// <summary>
    ///// 队列状态结构
    ///// </summary>
    //[System.Serializable]
    //public struct DownloadQueueStatus
    //{
    //    public int activeDownloads;
    //    public int pendingDownloads;
    //    public int activeConverts;
    //    public int pendingConverts;
    //}

    /// <summary>
    /// 暂停所有下载
    /// </summary>
    public void PauseAllDownloads()
    {
        foreach (var task in activeDownloads)
        {
            if (task.webRequest != null)
            {
                task.webRequest.Abort();
            }

            DisposeWebRequest(task);
            pendingDownloads.Enqueue(task); // 重新加入队列
        }
        activeDownloads.Clear();
    }

    /// <summary>
    /// 清理下载缓存
    /// </summary>
    public void ClearDownloadCache()
    {
        string fullPath = Path.Combine(Application.persistentDataPath, AssetBundlePathHelper.localSavePath);
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, true);
            Directory.CreateDirectory(fullPath);
            Log("下载缓存已清理");
        }
    }

    public void EndDownload()
    {
        foreach (DownloadTask task in activeDownloads)
        {
            if (task.webRequest != null)
            {
                task.webRequest.Abort();
            }

            DisposeWebRequest(task);
            DeleteDownloadArtifacts(task);
        }
        activeDownloads.Clear();

        while (pendingDownloads.Count > 0)
        {
            pendingDownloads.Dequeue();
        }

        pendingRetries.Clear();

        foreach (ConvertTask task in activeConverts)
        {
            TryDeleteFile(task.sourcePath);
            // 只删转换暂存文件：targetPath 上是已经安装好的旧版本，不能动。
            TryDeleteFile(task.targetPath + ".new");
        }
        activeConverts.Clear();

        while (pendingConverts.Count > 0)
        {
            ConvertTask task = pendingConverts.Dequeue();
            TryDeleteFile(task.sourcePath);
            // 同上：正式路径不能删。
            TryDeleteFile(task.targetPath + ".new");
        }

        activePackOperations = 0;
        loaderState = LoaderState.None;
        bundleInfoDict = null;
        Log("下载流程已终止，已清理未完成文件");
    }

    private static void DisposeWebRequest(DownloadTask task)
    {
        if (task?.webRequest == null)
        {
            return;
        }

        try
        {
            task.webRequest.Dispose();
        }
        catch (Exception e)
        {
            GameLogController.Warning($"释放 UnityWebRequest 失败: {e.Message}", LogTag);
        }

        task.webRequest = null;
    }

    /// <summary>
    /// 单个 AB 落盘成功后增量写入本地清单，支持中断后续传。
    /// </summary>
    private void PersistCompletedBundle(string bundleName)
    {
        CustomAssetBundleInfo info = ResolveBundleInfo(bundleName);
        if (info == null)
        {
            return;
        }

        AssetBundleUpdateChecker.UpsertLocalBundles(currentManifest, new[] { info });
    }

    /// <summary>
    /// LZMA 转 LZ4 完成后落盘记录：同时记下转换后的大小与 MD5，
    /// 下次启动就能真正校验落盘文件，而不是只看"存在且非空"。
    /// </summary>
    private void PersistConvertedBundle(string bundleName)
    {
        CustomAssetBundleInfo info = ResolveBundleInfo(bundleName);
        if (info == null)
        {
            return;
        }

        try
        {
            string finalPath = AssetBundlePathHelper.GetLocalLZ4Path(bundleName);
            if (!File.Exists(finalPath))
            {
                return;
            }

            FileInfo fileInfo = new FileInfo(finalPath);
            info.ConvertedSize = fileInfo.Length;
            info.ConvertedHash = AssetBundleUpdateChecker.ComputeFileMD5(finalPath);
        }
        catch (Exception e)
        {
            GameLogController.Warning($"记录转换后 Hash 失败: {bundleName}, {e.Message}", LogTag);
            return;
        }

        AssetBundleUpdateChecker.UpsertLocalBundles(currentManifest, new[] { info });
    }

    private CustomAssetBundleInfo ResolveBundleInfo(string bundleName)
    {
        if (string.IsNullOrEmpty(bundleName) || currentManifest == null)
        {
            return null;
        }

        CustomAssetBundleInfo info = null;
        if (bundleInfoDict != null)
        {
            bundleInfoDict.TryGetValue(bundleName, out info);
        }

        if (info == null && currentManifest.AssetBundles != null)
        {
            for (int i = 0; i < currentManifest.AssetBundles.Count; i++)
            {
                CustomAssetBundleInfo candidate = currentManifest.AssetBundles[i];
                if (candidate != null
                    && string.Equals(candidate.BundleName, bundleName, StringComparison.OrdinalIgnoreCase))
                {
                    info = candidate;
                    break;
                }
            }
        }

        return info;
    }

    private void PersistCompletedBundles(string[] bundleNames)
    {
        if (bundleNames == null || bundleNames.Length == 0 || currentManifest == null)
        {
            return;
        }

        var completed = new List<CustomAssetBundleInfo>();
        for (int i = 0; i < bundleNames.Length; i++)
        {
            string bundleName = bundleNames[i];
            if (string.IsNullOrEmpty(bundleName))
            {
                continue;
            }

            CustomAssetBundleInfo info = null;
            if (bundleInfoDict != null)
            {
                bundleInfoDict.TryGetValue(bundleName, out info);
            }

            if (info == null && currentManifest.AssetBundles != null)
            {
                for (int j = 0; j < currentManifest.AssetBundles.Count; j++)
                {
                    CustomAssetBundleInfo candidate = currentManifest.AssetBundles[j];
                    if (candidate != null
                        && string.Equals(candidate.BundleName, bundleName, StringComparison.OrdinalIgnoreCase))
                    {
                        info = candidate;
                        break;
                    }
                }
            }

            if (info != null)
            {
                completed.Add(info);
            }
        }

        if (completed.Count > 0)
        {
            AssetBundleUpdateChecker.UpsertLocalBundles(currentManifest, completed);
        }
    }

    private void Log(string message)
    {
        if (enableLogging)
        {
            GameLogController.Log(message, LogTag);
        }
    }

    private void LogError(string message)
    {
        if (enableLogging)
        {
            GameLogController.Error(message, LogTag);
        }
    }

    /// <summary>
    /// 对下载后的源文件进行二次校验（大小 + MD5）。
    /// 注意：转换后的 LZ4 文件不会走此方法；启动时由 UpdateChecker 用本地清单源 Hash 判版本。
    /// </summary>
    private bool VerifyDownloadedFile(string bundleName, string filePath, out string errorMessage)
    {
        errorMessage = "";
        if (bundleInfoDict == null || !bundleInfoDict.TryGetValue(bundleName, out CustomAssetBundleInfo info))
        {
            return true;
        }

        if (!File.Exists(filePath))
        {
            errorMessage = "下载文件不存在";
            return false;
        }

        try
        {
            FileInfo fileInfo = new FileInfo(filePath);
            if (info.Size > 0 && fileInfo.Length != info.Size)
            {
                errorMessage = $"文件大小不匹配: 本地 {fileInfo.Length} B != 清单 {info.Size} B";
                return false;
            }

            if (!string.IsNullOrEmpty(info.Hash))
            {
                string localHash = AssetBundleUpdateChecker.ComputeFileMD5(filePath);
                if (!string.Equals(localHash, info.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = $"MD5 不匹配: 本地 {localHash} != 清单 {info.Hash}";
                    return false;
                }
            }

            return true;
        }
        catch (Exception e)
        {
            errorMessage = $"校验异常: {e.Message}";
            return false;
        }
    }

    public LoaderState GetLoaderState()
    {
        return this.loaderState;
    }

    /// <summary>获取总下载进度（0~1）。</summary>
    public float GetTotalProgress()
    {
        if (totalDownloadSize <= 0) return 0f;
        return Mathf.Clamp01((float)downloadedBytes / totalDownloadSize);
    }

    /// <summary>获取已下载字节数。</summary>
    public long GetDownloadedBytes()
    {
        return downloadedBytes;
    }

    /// <summary>获取总需要下载的字节数。</summary>
    public long GetTotalDownloadSize()
    {
        return totalDownloadSize;
    }

    /// <summary>获取格式化后的下载速度文本。</summary>
    public string GetDownloadSpeedText()
    {
        return FormatBytesPerSecond(downloadSpeed);
    }

    private static string FormatBytesPerSecond(float bytesPerSecond)
    {
        if (bytesPerSecond < 1024) return $"{bytesPerSecond:F0} B/s";
        if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024.0f:F2} KB/s";
        return $"{bytesPerSecond / (1024.0f * 1024.0f):F2} MB/s";
    }
}