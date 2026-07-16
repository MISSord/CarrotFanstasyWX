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
            AssetBundleCloudUploader.IsCodeHotUpdateUploadPath);

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
