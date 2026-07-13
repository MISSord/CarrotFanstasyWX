using System.Collections;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CarrotFantasy
{
    /// <summary>
    /// AOT 侧资源更新：在加载热更 DLL 之前完成 CheckUpdate / Download。
    /// 复用现有 AssetBundleUpdateChecker / Downloader 与 IMGUI 对话框。
    /// </summary>
    public static class AotResourceUpdateRunner
    {
        public enum Result
        {
            Success,
            Failed,
            Cancelled,
        }

        /// <summary>
        /// 执行更新检查与下载。Editor Development/Debug 模式直接成功跳过。
        /// </summary>
        public static IEnumerator Run(MonoBehaviour host, System.Action<Result> onFinished)
        {
            if (host == null)
            {
                onFinished?.Invoke(Result.Failed);
                yield break;
            }

#if UNITY_EDITOR
            LoadMode loadMode = (LoadMode)EditorPrefs.GetInt("GameLoadMode", 0);
            if (loadMode == LoadMode.Development || loadMode == LoadMode.DebugMode)
            {
                Debug.Log("[AotResourceUpdateRunner] Editor Dev/Debug：跳过资源更新");
                onFinished?.Invoke(Result.Success);
                yield break;
            }

            if (loadMode == LoadMode.Testing
                && AssetBundleUpdateChecker.TryBootstrapTestingCheck(out UpdateCheckResult testingResult))
            {
                AssetBundleManager.Instance.SetAssetBundleItem(testingResult.customManifest);
                if (testingResult.hasChanges)
                {
                    yield return RunDownloadFlow(host, testingResult, onFinished);
                }
                else
                {
                    onFinished?.Invoke(Result.Success);
                }

                yield break;
            }
#endif

            bool checkDone = false;
            UpdateCheckResult checkResult = null;
            AssetBundleUpdateChecker checker = new AssetBundleUpdateChecker();

            checker.StartUpdateCheck(
                AssetBundlePathHelper.GetServerLoadUrl(),
                result =>
                {
                    checkResult = result;
                    checkDone = true;
                });

            while (!checkDone || checker.IsRunning)
            {
                checker.Update();
                yield return null;
            }

            // 必须以 isSuccess 为准：Error 可能在 yield 期间由下载协程触发，
            // 此时循环内缓存的 lastState 可能仍是 DownloadingManifest，不能当作成功。
            bool checkFailed = checkResult == null || !checkResult.isSuccess;
            checker.EndCheck();

            if (checkFailed)
            {
                if (TryInjectCachedLocalManifest())
                {
                    Debug.LogWarning(
                        "[AotResourceUpdateRunner] 远程清单检查失败，已回退到本地缓存 custom_manifest.json");
                    yield return ShowUpdateListFallbackDialog(host, onFinished);
                    yield break;
                }

                Debug.LogError("[AotResourceUpdateRunner] 资源更新检查失败（无可用本地清单）");
                yield return ShowUpdateListErrorDialog(host);
                onFinished?.Invoke(Result.Failed);
                yield break;
            }

            if (checkResult.customManifest == null
                || checkResult.customManifest.AssetBundles == null
                || checkResult.customManifest.AssetBundles.Count == 0)
            {
                if (TryInjectCachedLocalManifest())
                {
                    Debug.LogWarning(
                        "[AotResourceUpdateRunner] 远程清单为空，已回退到本地缓存 custom_manifest.json");
                    yield return ShowUpdateListFallbackDialog(host, onFinished);
                    yield break;
                }

                Debug.LogError("[AotResourceUpdateRunner] 清单为空，无法继续");
                yield return ShowUpdateListErrorDialog(host);
                onFinished?.Invoke(Result.Failed);
                yield break;
            }

            if (!checkResult.hasChanges)
            {
                AssetBundleManager.Instance.SetAssetBundleItem(checkResult.customManifest);
                onFinished?.Invoke(Result.Success);
                yield break;
            }

            yield return RunDownloadFlow(host, checkResult, onFinished);
        }

        /// <summary>远程失败时尝试使用 persistentDataPath 中上次成功保存的清单。</summary>
        private static bool TryInjectCachedLocalManifest()
        {
            string path = AssetBundlePathHelper.GetPersistentManifestPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                CustomManifest manifest = JsonUtility.FromJson<CustomManifest>(json);
                if (manifest?.AssetBundles == null || manifest.AssetBundles.Count == 0)
                {
                    return false;
                }

                AssetBundleManager.Instance.SetAssetBundleItem(manifest);
                Debug.Log(
                    $"[AotResourceUpdateRunner] 已注入本地清单，共 {manifest.AssetBundles.Count} 个 AB");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AotResourceUpdateRunner] 读取本地清单失败: {e.Message}");
                return false;
            }
        }

        private static IEnumerator RunDownloadFlow(
            MonoBehaviour host,
            UpdateCheckResult checkResult,
            System.Action<Result> onFinished)
        {
            bool confirmed = false;
            bool cancelled = false;

            GameObject confirmGo = new GameObject("DownloadConfirmDialog");
            confirmGo.transform.SetParent(host.transform, false);
            DownloadConfirmDialog confirm = confirmGo.AddComponent<DownloadConfirmDialog>();
            confirm.Setup(
                checkResult.totalDownloadSize,
                () => confirmed = true,
                () => cancelled = true);

            while (!confirmed && !cancelled)
            {
                yield return null;
            }

            Object.Destroy(confirmGo);

            if (cancelled)
            {
                onFinished?.Invoke(Result.Cancelled);
                QuitApp();
                yield break;
            }

            GameContext context = new GameContext { result = checkResult };
            AssetBundleDownloader downloader = AssetBundleDownloader.Instance;
            downloader.Init();

            GameObject progressGo = new GameObject("DownloadProgressDialog");
            progressGo.transform.SetParent(host.transform, false);
            progressGo.AddComponent<DownloadProgressDialog>().Setup(downloader);

            bool downloadFinished = false;
            bool downloadSuccess = false;
            downloader.StartDownload(
                context,
                _ => { },
                success =>
                {
                    downloadFinished = true;
                    downloadSuccess = success;
                    if (success)
                    {
                        AssetBundleUpdateChecker.SaveLocalManifest(checkResult.customManifest);
                        AssetBundleManager.Instance.SetAssetBundleItem(checkResult.customManifest);
                    }
                });

            while (!downloadFinished || downloader.GetLoaderState() != LoaderState.Idle)
            {
                downloader.Update();
                yield return null;
            }

            downloader.EndDownload();
            Object.Destroy(progressGo);

            if (!downloadSuccess)
            {
                Debug.LogError("[AotResourceUpdateRunner] 资源下载失败");
                ShowErrorAndQuit();
                onFinished?.Invoke(Result.Failed);
                yield break;
            }

            onFinished?.Invoke(Result.Success);
        }

        /// <summary>
        /// 远程失败但本地有清单：询问是否继续；继续则走后续成功逻辑，退出则结束进程。
        /// </summary>
        private static IEnumerator ShowUpdateListFallbackDialog(
            MonoBehaviour host,
            System.Action<Result> onFinished)
        {
            bool continueChosen = false;
            bool exitChosen = false;

            GameObject dialogGo = new GameObject("UpdateListFallbackDialog");
            if (host != null)
            {
                dialogGo.transform.SetParent(host.transform, false);
            }

            UpdateListFallbackDialog dialog = dialogGo.AddComponent<UpdateListFallbackDialog>();
            dialog.Setup(
                onContinue: () => continueChosen = true,
                onExit: () => exitChosen = true);

            while (!continueChosen && !exitChosen)
            {
                yield return null;
            }

            Object.Destroy(dialogGo);

            if (exitChosen)
            {
                onFinished?.Invoke(Result.Cancelled);
                QuitApp();
                yield break;
            }

            onFinished?.Invoke(Result.Success);
        }

        /// <summary>获取热更新列表失败：提示后仅允许退出；重启按钮暂无逻辑。</summary>
        private static IEnumerator ShowUpdateListErrorDialog(MonoBehaviour host)
        {
            bool exitChosen = false;

            GameObject dialogGo = new GameObject("UpdateListErrorDialog");
            if (host != null)
            {
                dialogGo.transform.SetParent(host.transform, false);
            }

            UpdateListErrorDialog dialog = dialogGo.AddComponent<UpdateListErrorDialog>();
            dialog.Setup(
                onExit: () => exitChosen = true,
                onRestart: () =>
                {
                    // TODO: 实现进程级重启；当前按需求点击无反应。
                });

            while (!exitChosen)
            {
                yield return null;
            }

            Object.Destroy(dialogGo);
            QuitApp();
        }

        private static void ShowErrorAndQuit()
        {
#if UNITY_EDITOR
            EditorUtility.DisplayDialog("资源更新失败", "资源下载或校验失败，请检查网络后重试。", "退出");
#endif
            QuitApp();
        }

        private static void QuitApp()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
