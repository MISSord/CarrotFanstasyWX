using System.Collections.Generic;
using System.IO;
using CarrotFantasy.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 仅热更代码：HybridCLR Generate/All → 同步 DLL → 刷新清单/Pack → 可选上传云端。
/// 不重新打 Unity AssetBundle。
/// </summary>
public static class HybridCLRCodeHotUpdatePipeline
{
    /// <summary>本次需要上传的 Pack 文件名；null 表示全传（旧行为）。</summary>
    static HashSet<string> s_ChangedPacks;

    public struct Result
    {
        public bool Success;
        public string Message;
        public string PlatformBundlePath;
        public int CopiedDllCount;
        public int ManifestVersion;
    }

    [MenuItem("Tools/HybridCLR/一键热更代码（Generate+同步+清单）", priority = 110)]
    public static void RunFromMenu()
    {
        Run(
            EditorUserBuildSettings.activeBuildTarget,
            promptUpload: true,
            showManifestDialog: false,
            interactive: true);
    }

    public static Result Run(bool promptUpload, bool showManifestDialog = false)
    {
        return Run(
            AssetBundleBuildSettings.GetBuildTarget(),
            promptUpload,
            showManifestDialog,
            interactive: true);
    }

    public static Result Run(
        BuildTarget target,
        bool promptUpload,
        bool showManifestDialog = false)
    {
        return Run(target, promptUpload, showManifestDialog, interactive: true);
    }

    /// <param name="interactive">false 时不弹 Dialog / RebuildAdvisor，供 Batch CLI 使用。</param>
    public static Result Run(
        BuildTarget target,
        bool promptUpload,
        bool showManifestDialog,
        bool interactive)
    {
        var result = new Result();
        s_ChangedPacks = null;
        if (EditorUserBuildSettings.activeBuildTarget != target)
        {
            result.Message = string.Format(
                "当前 Editor 激活平台为 {0}，与目标 {1} 不一致。\n" +
                "请先在 File → Build Settings 切换到目标平台（HybridCLR Generate 依赖激活平台），再执行代码热更。",
                EditorUserBuildSettings.activeBuildTarget,
                target);
            Fail(result, interactive);
            return result;
        }

        AssetBundleBuildSettings.SetBuildTarget(target);
        string outputRoot = AssetBundleBuildSettings.GetOutputRoot();
        string platformPath = AssetBundleBuildSettings.GetPlatformBundlePath(outputRoot, target);

        try
        {
            if (interactive)
            {
                EditorUtility.DisplayProgressBar("代码热更", "HybridCLR Generate/All…", 0.1f);
            }

            if (!EditorApplication.ExecuteMenuItem("HybridCLR/Generate/All"))
            {
                // 部分 Unity 版本 ExecuteMenuItem 对子菜单返回 false，仍继续尝试同步。
                Debug.LogWarning("[HybridCLRCodeHotUpdate] ExecuteMenuItem 返回 false，继续尝试同步 DLL。");
            }

            if (interactive)
            {
                EditorUtility.DisplayProgressBar("代码热更", "同步 DLL 到 StreamingAssets / AB 输出…", 0.45f);
            }

            int copied = HybridCLRProjectSetup.SyncDllsForHotUpdate(target);
            result.CopiedDllCount = copied;
            if (copied <= 0)
            {
                result.Message =
                    "未复制到任何 DLL。请确认已安装 IL2CPP 工具链，且 Generate/All 已生成 HotUpdate/AOT DLL。";
                Fail(result, interactive);
                return result;
            }

            if (!Directory.Exists(platformPath))
            {
                result.Message =
                    "AB 平台输出目录不存在:\n" + platformPath +
                    "\n请先完整打一次 AssetBundle，再使用代码热更。";
                Fail(result, interactive);
                return result;
            }

            string hybridClrHotUpdate = Path.Combine(platformPath, "hybridclr", "hotupdate");
            if (!Directory.Exists(hybridClrHotUpdate)
                || Directory.GetFiles(hybridClrHotUpdate, "*.dll.bytes").Length == 0)
            {
                result.Message = "同步后未找到 hybridclr/hotupdate/*.dll.bytes，请查看 Console。";
                Fail(result, interactive);
                return result;
            }

            if (interactive)
            {
                EditorUtility.DisplayProgressBar("代码热更", "刷新 custom_manifest / Packs…", 0.75f);
            }

            // 先统一文件名大小写，再和现有清单比对：如果线上还是旧的大小写命名，
            // 这里会直接报"与清单不一致"，强制走一次完整 AB 打包 + 全量上传。
            AssetBundlePackager.NormalizeBundleFileNames(platformPath, target);

            // 生成新清单前先确认：除了 hybridclr 之外，本地 AB 输出必须和现有清单完全一致。
            // 否则新清单会指向从未上传过的字节（线上表现为全量重下或大面积 404）。
            if (!TryLoadManifest(platformPath, out CustomManifest previousManifest, out string manifestError))
            {
                result.Message = manifestError;
                Fail(result, interactive);
                return result;
            }

            if (!VerifyAbOutputMatchesManifest(platformPath, target, previousManifest, out string driftError))
            {
                result.Message = driftError;
                Fail(result, interactive);
                return result;
            }

            int version = AssetBundleBuildSettings.SuggestNextManifestVersion(outputRoot, target);
            CompressionType compression = AssetBundleBuildSettings.GetCompressionType();
            string fullOutputRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputRoot));

            CustomManifest manifest = AssetBundlePackager.GenerateManifest(
                fullOutputRoot,
                target,
                version,
                (int)compression,
                showManifestDialog);

            if (manifest == null)
            {
                result.Message = "生成清单失败，请查看 Console。";
                Fail(result, interactive);
                return result;
            }

            // 固定时间戳后，内容没变的 Pack 字节也一致；只上传哈希变化的 Pack。
            s_ChangedPacks = ComputeChangedPacks(previousManifest, manifest);

            AssetBundleBuildSettings.WriteRuntimeConfig(target);

            result.Success = true;
            result.PlatformBundlePath = platformPath;
            result.ManifestVersion = manifest.ManifestVersion;
            result.Message = string.Format(
                "平台: {0}\n复制 DLL: {1} 个\n清单版本: {2}\n路径: {3}",
                target,
                copied,
                manifest.ManifestVersion,
                platformPath);

            if (interactive)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("代码热更完成", result.Message, "确定");
                PcPlayerRebuildAdvisor.ShowAfterPack("代码热更（Generate + 同步 DLL + 清单）已完成。");
            }
            else
            {
                Debug.Log("[HybridCLRCodeHotUpdate] " + result.Message.Replace('\n', ' '));
            }

            if (promptUpload)
            {
                if (interactive)
                {
                    PromptAndUpload(platformPath);
                }
                else
                {
                    AssetBundleCloudUploadResult uploadResult = UploadCodeHotUpdate(platformPath);
                    if (!uploadResult.Success)
                    {
                        result.Success = false;
                        result.Message = result.Message + "\n上传失败: " + uploadResult.Message;
                        Debug.LogError("[HybridCLRCodeHotUpdate] 上传失败: " + uploadResult.Message);
                    }
                }
            }

            return result;
        }
        catch (System.Exception e)
        {
            result.Message = e.Message;
            Debug.LogException(e);
            Fail(result, interactive);
            return result;
        }
        finally
        {
            if (interactive)
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }

    static bool TryLoadManifest(string platformPath, out CustomManifest manifest, out string error)
    {
        manifest = null;
        error = null;
        string manifestPath = Path.Combine(platformPath, "custom_manifest.json");
        if (!File.Exists(manifestPath))
        {
            return true;
        }

        try
        {
            manifest = JsonUtility.FromJson<CustomManifest>(File.ReadAllText(manifestPath));
            return true;
        }
        catch (System.Exception e)
        {
            error = "读取现有 custom_manifest.json 失败: " + e.Message;
            return false;
        }
    }

    /// <summary>
    /// 代码热更只允许 hybridclr 目录变化：其余 AB 必须与现有清单逐字节一致。
    /// </summary>
    static bool VerifyAbOutputMatchesManifest(string platformPath, BuildTarget target, CustomManifest manifest, out string error)
    {
        error = null;
        if (manifest?.AssetBundles == null || manifest.AssetBundles.Count == 0)
        {
            return true;
        }

        // BuildPipeline 会在输出目录生成"平台名"总清单 AB 和 .manifest 文本，它们不在 custom_manifest 里。
        string platformManifestName = AssetBundlePackager.GetPlatformFolder(target).ToLowerInvariant();

        var known = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < manifest.AssetBundles.Count; i++)
        {
            CustomAssetBundleInfo bundle = manifest.AssetBundles[i];
            if (bundle == null || string.IsNullOrEmpty(bundle.BundleName))
            {
                continue;
            }

            string relative = bundle.BundleName.Replace('\\', '/');
            known.Add(relative);

            if (CarrotFantasy.HybridCLRPaths.IsHybridClrRawFile(relative))
            {
                continue;
            }

            string filePath = Path.Combine(platformPath, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(filePath))
            {
                error = "本地 AB 输出缺少清单中的文件: " + relative
                        + "（文件名大小写改过也会走到这里），请先完整打一次 AB 再热更代码。";
                return false;
            }

            FileInfo info = new FileInfo(filePath);
            if (info.Length != bundle.Size
                || (!string.IsNullOrEmpty(bundle.Hash)
                    && !string.Equals(MD5Checker.ComputeFileMD5(filePath), bundle.Hash, System.StringComparison.OrdinalIgnoreCase)))
            {
                error = "本地 AB 与清单不一致: " + relative + "，请先完整打一次 AB 再热更代码。";
                return false;
            }
        }

        foreach (string file in Directory.GetFiles(platformPath, "*", SearchOption.AllDirectories))
        {
            string relative = file.Substring(platformPath.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');

            if (relative.StartsWith(PackGroupingDefaults.PacksFolderName + "/", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(relative, platformManifestName, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (relative.EndsWith(".manifest", System.StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".sig", System.StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!known.Contains(relative))
            {
                error = "AB 输出目录里有清单未登记的新文件: " + relative + "，请先完整打一次 AB。";
                return false;
            }
        }

        return true;
    }

    static HashSet<string> ComputeChangedPacks(CustomManifest previous, CustomManifest current)
    {
        var changed = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        if (current?.DownloadPacks == null)
        {
            return changed;
        }

        var previousHashes = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        if (previous?.DownloadPacks != null)
        {
            for (int i = 0; i < previous.DownloadPacks.Count; i++)
            {
                DownloadPackInfo pack = previous.DownloadPacks[i];
                if (pack != null && !string.IsNullOrEmpty(pack.PackFileName))
                {
                    previousHashes[pack.PackFileName] = pack.PackHash;
                }
            }
        }

        for (int i = 0; i < current.DownloadPacks.Count; i++)
        {
            DownloadPackInfo pack = current.DownloadPacks[i];
            if (pack == null || string.IsNullOrEmpty(pack.PackFileName))
            {
                continue;
            }

            if (!previousHashes.TryGetValue(pack.PackFileName, out string oldHash)
                || !string.Equals(oldHash, pack.PackHash, System.StringComparison.OrdinalIgnoreCase))
            {
                changed.Add(Path.GetFileName(pack.PackFileName));
            }
        }

        return changed;
    }

    static bool IsCodeHotUpdateFileToUpload(string relativePath, HashSet<string> changedPacks)
    {
        if (!AssetBundleCloudUploader.IsCodeHotUpdateUploadPath(relativePath))
        {
            return false;
        }

        string path = relativePath.Replace('\\', '/');
        if (path.StartsWith(PackGroupingDefaults.PacksFolderName + "/", System.StringComparison.OrdinalIgnoreCase))
        {
            return changedPacks == null || changedPacks.Contains(Path.GetFileName(path));
        }

        return true;
    }

    static void Fail(Result result, bool interactive)
    {
        Debug.LogError("[HybridCLRCodeHotUpdate] " + result.Message);
        if (interactive)
        {
            EditorUtility.DisplayDialog("代码热更失败", result.Message, "确定");
        }
    }

    /// <summary>无确认框上传代码热更产物（hybridclr / packs / 清单）。</summary>
    public static AssetBundleCloudUploadResult UploadCodeHotUpdate(string platformBundlePath)
    {
        var fail = new AssetBundleCloudUploadResult();
        if (string.IsNullOrEmpty(platformBundlePath) || !Directory.Exists(platformBundlePath))
        {
            fail.Message = "平台输出目录不存在:\n" + platformBundlePath;
            Debug.LogError("[HybridCLRCodeHotUpdate] " + fail.Message);
            return fail;
        }

        if (!AssetBundleDeploySettings.TryValidate(out string validationError))
        {
            fail.Message = validationError;
            Debug.LogError("[HybridCLRCodeHotUpdate] 上传失败: " + validationError);
            return fail;
        }

        AssetBundleCloudUploadResult uploadResult = AssetBundleCloudUploader.UploadDirectory(
            platformBundlePath,
            relativePath => IsCodeHotUpdateFileToUpload(relativePath, s_ChangedPacks));

        if (uploadResult.Cancelled)
        {
            Debug.LogWarning(
                "[HybridCLRCodeHotUpdate] 上传已取消，已上传 "
                + uploadResult.UploadedFileCount
                + " 个文件。");
            return uploadResult;
        }

        if (!uploadResult.Success)
        {
            Debug.LogError("[HybridCLRCodeHotUpdate] 上传失败: " + uploadResult.Message);
            return uploadResult;
        }

        Debug.Log("[HybridCLRCodeHotUpdate] 上传完成: " + uploadResult.Message);
        return uploadResult;
    }

    public static void PromptAndUpload(string platformBundlePath)
    {
        if (string.IsNullOrEmpty(platformBundlePath) || !Directory.Exists(platformBundlePath))
        {
            EditorUtility.DisplayDialog("上传失败", "平台输出目录不存在:\n" + platformBundlePath, "确定");
            return;
        }

        string host = AssetBundleDeploySettings.Host;
        string remote = AssetBundleDeploySettings.RemotePath;
        if (!EditorUtility.DisplayDialog(
                "上传云端",
                "代码热更产物已准备完成。\n是否立即上传到云服务器？\n\n"
                + "将上传：hybridclr/、packs/、custom_manifest.json、version.txt\n\n"
                + host + ":" + remote,
                "上传",
                "稍后"))
        {
            return;
        }

        AssetBundleCloudUploadResult uploadResult = UploadCodeHotUpdate(platformBundlePath);
        if (uploadResult.Cancelled)
        {
            EditorUtility.DisplayDialog(
                "上传已取消",
                string.Format("已上传 {0} 个文件。", uploadResult.UploadedFileCount),
                "确定");
            return;
        }

        if (!uploadResult.Success)
        {
            EditorUtility.DisplayDialog("上传失败", uploadResult.Message, "确定");
            return;
        }

        EditorUtility.DisplayDialog("上传完成", uploadResult.Message, "确定");
    }
}
