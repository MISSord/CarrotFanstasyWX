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

//AssetBundle下载器，负责AB下载与解压转换
public class AssetBundleDownloader
{
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
    private float timeout = 30f;
    private LoaderState loaderState = LoaderState.None;

    private bool enableLogging = true;
    private List<DownloadTask> activeDownloads = new List<DownloadTask>();
    private Queue<DownloadTask> pendingDownloads = new Queue<DownloadTask>();

    private Queue<ConvertTask> pendingConverts = new Queue<ConvertTask>();
    private List<ConvertTask> activeConverts = new List<ConvertTask>();

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

    public void StartDownload(GameContext context, System.Action<bool> finishCalls, System.Action<bool> callBacks)
    {
        loaderState = LoaderState.Loading;

        UpdateCheckResult result = context.result;
        CustomManifest custom = result.customManifest;
        finallDownloadUrl = AssetBundlePathHelper.GetServerLoadUrl();
        if (result != null)
        {
            //两个列表加起来等于要下载的
            string[] bundleStr = new string[result.bundlesToDownload.Count + result.bundlesToUpdate.Count];
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
            this.DownloadBundles(bundleStr, custom, null, callBacks);
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
        if (loaderState == LoaderState.None) return;

        // 检查正在下载的任务
        for (int i = activeDownloads.Count - 1; i >= 0; i--)
        {
            var task = activeDownloads[i];

            if (task.webRequest != null && task.webRequest.isDone)
            {
                HandleDownloadCompletion(task);
                activeDownloads.RemoveAt(i);
            }
            else if (task.webRequest != null)
            {
                // 更新进度
                task.progress = task.webRequest.downloadProgress;
                task.downloadedBytes = (long)task.webRequest.downloadedBytes;
                OnDownloadProgress?.Invoke(task);
            }
        }

        // 启动新的下载任务
        while (activeDownloads.Count < maxConcurrentDownloads && pendingDownloads.Count > 0)
        {
            var task = pendingDownloads.Dequeue();
            StartDownloadTask(task);
        }

        //全部下载完，切换到解压状态
        if (loaderState == LoaderState.Loading && pendingDownloads.Count == 0 && activeDownloads.Count == 0)
        {
            loaderState = LoaderState.Convert;
            //这里加个回调，告诉外面进入解压状态，刷新界面
        }
    }

    /// <summary>
    /// 更新转换任务状态
    /// </summary>
    private void UpdateConverts()
    {
        //处于下载阶段时不解压，避免IO过大
        //进入游戏后下载器处于待机状态，这时候下载完成后直接解压，避免资源等待时间过长
        if (loaderState == LoaderState.None || loaderState == LoaderState.Loading) return;

        // 启动新的转换任务
        while (activeConverts.Count < 6 && pendingConverts.Count > 0) // 最多同时转换6个
        {
            var task = pendingConverts.Dequeue();
            StartConvertTask(task);
        }

        //全部解压完成，进入idle状态，告诉外面处理完毕
        if (loaderState == LoaderState.Convert && activeConverts.Count == 0 && pendingConverts.Count == 0)
        {
            loaderState = LoaderState.Idle;
        }
    }

    /// <summary>
    /// 下载单个AB包
    /// </summary>
    public void DownloadBundle(string bundleName, bool isNeedConvert, System.Action<bool, string> callback = null)
    {
        if (IsBundleAlreadyInQueue(bundleName))
        {
            callback?.Invoke(false, "Bundle is already in download queue");
            return;
        }

        //需要转换的话，放临时路径，不需要就直接放对应的位置
        var downloadTask = new DownloadTask
        {
            bundleName = bundleName,
            remoteURL = $"{finallDownloadUrl}/{bundleName}",
            tempPath = isNeedConvert == true ? Path.Combine(Application.temporaryCachePath, $"{bundleName}.temp") : AssetBundlePathHelper.GetLocalLZ4Path(bundleName),
            finalPath = AssetBundlePathHelper.GetLocalLZ4Path(bundleName),
            status = DownloadStatus.Pending,
            callback = callback,
            isNeedConvert = isNeedConvert
        };

        //// 检查本地是否已存在最新版本
        //if (IsLocalBundleUpToDate(bundleName))
        //{
        //    Log($"AB包 {bundleName} 已是最新版本，跳过下载");
        //    callback?.Invoke(true, "Already up to date");
        //    return;
        //}

        pendingDownloads.Enqueue(downloadTask);
        Log($"AB包 {bundleName} 已加入下载队列，当前位置: {pendingDownloads.Count}");
    }

    /// <summary>
    /// 批量下载并转换AB包
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
            DownloadBundle(bundleName, isNeedConvert, (success, message) =>
            {
                completionFlags[bundleName] = true;
                if (!success) allSuccess = false;
                completedCount++;
                progressCallback?.Invoke(completedCount, totalCount);
                Log($"下载与解压完成: {bundleName} ({completedCount}/{totalCount})");
            });
        }

        // 等待所有任务完成
        yield return new WaitUntil(() => completedCount >= totalCount);

        Log($"批量下载完成: 成功 {completedCount}/{totalCount}");
        completeCallback?.Invoke(allSuccess);
    }

    /// <summary>
    /// 开始下载任务
    /// </summary>
    private void StartDownloadTask(DownloadTask task)
    {
        task.status = DownloadStatus.Downloading;
        activeDownloads.Add(task);

        Log($"开始下载: {task.bundleName}");

        task.webRequest = UnityWebRequest.Get(task.remoteURL);
        task.webRequest.timeout = (int)timeout;
        task.webRequest.downloadHandler = new DownloadHandlerFile(task.tempPath);
        task.webRequest.SendWebRequest();
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
        if (task.webRequest.result != UnityWebRequest.Result.Success)
#else
        if (task.webRequest.isNetworkError || task.webRequest.isHttpError)
#endif
        {
            HandleDownloadFailure(task, task.webRequest.error);
        }
        else
        {
            HandleDownloadSuccess(task);
        }
    }

    /// <summary>
    /// 处理下载成功
    /// </summary>
    private void HandleDownloadSuccess(DownloadTask task)
    {
        Log($"下载成功: {task.bundleName}, 文件大小: {new FileInfo(task.tempPath).Length} bytes");

        if (task.isNeedConvert == true)
        {
            // 创建转换任务
            var convertTask = new ConvertTask
            {
                bundleName = task.bundleName,
                sourcePath = task.tempPath,
                targetPath = task.finalPath,
                status = ConvertStatus.Pending,
                callback = task.callback
            };

            pendingConverts.Enqueue(convertTask);
        }
        else
        {
            Log(string.Format("{0}下载完成，无需解压", task.bundleName));
            task.callback?.Invoke(true, "");
        }

        task.status = DownloadStatus.Completed;

        OnDownloadProgress?.Invoke(task);
    }

    /// <summary>
    /// 处理下载失败
    /// </summary>
    private void HandleDownloadFailure(DownloadTask task, string error)
    {
        task.retryCount++;

        if (task.retryCount < maxRetryCount)
        {
            Log($"下载失败，准备重试: {task.bundleName} ({task.retryCount}/{maxRetryCount})");
            task.status = DownloadStatus.Retrying;
            task.errorMessage = error;

            // 重新加入下载队列
            pendingDownloads.Enqueue(task);
        }
        else
        {
            string errorMsg = $"下载 {task.bundleName} 失败，已达到最大重试次数: {error}";
            LogError(errorMsg);

            task.status = DownloadStatus.Failed;
            task.errorMessage = errorMsg;

            OnDownloadFailed?.Invoke(task.bundleName, errorMsg);
            task.callback?.Invoke(false, errorMsg);
        }

        OnDownloadProgress?.Invoke(task);
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
    /// 转换协程
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
        // 启动异步再压缩任务，CRC校验设为0表示跳过
        var recompressOperation = AssetBundle.RecompressAssetBundleAsync(task.sourcePath, task.targetPath, lz4CompressionMethod, 0, ThreadPriority.Normal);

        // 等待再压缩操作完成
        yield return recompressOperation;

        // 检查再压缩结果
        bool success = recompressOperation.success;
        string errorMessage = ""; // recompressOperation.errorMessage;

        // 清理临时文件
        if (File.Exists(task.sourcePath))
        {
            File.Delete(task.sourcePath);
        }

        // 处理转换结果
        if (success)
        {
            task.status = ConvertStatus.Completed;
            task.progress = 1f;

            // 验证转换后的AB包
            AssetBundle bundle = AssetBundle.LoadFromFile(task.targetPath);
            if (bundle != null)
            {
                OnDownloadComplete?.Invoke(task.bundleName, bundle);
                bundle.Unload(false);
                task.callback?.Invoke(true, "Download and convert successful");
                Log($"AB包 {task.bundleName} 转换完成");
            }
            else
            {
                errorMessage = "转换后的AB包加载失败";
                success = false;
            }
        }
        else
        {
            errorMessage = "格式转换失败";
        }

        if (!success)
        {
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

    }

    private void Log(string message)
    {
        if (enableLogging)
        {
            Debug.Log($"[AssetBundleDownloader] {message}");
        }
    }

    private void LogError(string message)
    {
        if (enableLogging)
        {
            Debug.LogError($"[AssetBundleDownloader] {message}");
        }
    }

    public LoaderState GetLoaderState()
    {
        return this.loaderState;
    }
}