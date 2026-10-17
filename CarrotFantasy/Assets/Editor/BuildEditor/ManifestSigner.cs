using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

/// <summary>
/// 用本机私钥给 custom_manifest.json 签名。私钥路径：CarrotFantasy/Keys/manifest_signing_private.xml。
/// </summary>
public static class ManifestSigner
{
    public static string PrivateKeyPath =>
        Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Keys", "manifest_signing_private.xml");

    public static bool TrySign(string manifestPath, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
        {
            error = "清单文件不存在: " + manifestPath;
            return false;
        }

        // 开发期跳过签名：不要求私钥，但要把可能残留的旧签名删掉，
        // 避免之后关掉宏上传出"新清单 + 旧签名"，导致客户端全员验签失败。
        if (ManifestSignature.IsVerificationBypassed)
        {
            string stalePath = manifestPath + ".sig";
            if (File.Exists(stalePath))
            {
                File.Delete(stalePath);
                Debug.Log("[ManifestSigner] 开发跳过签名，已删除残留签名文件: " + stalePath);
            }

            Debug.LogWarning(
                "[ManifestSigner] ⚠ 开发宏 CF_DEV_SKIP_MANIFEST_SIGN 生效：本次清单未签名。"
                + "发布前必须关闭该宏，并重新打包生成 .sig。");
            return true;
        }

        if (!File.Exists(PrivateKeyPath))
        {
            error = "缺少清单私钥，无法签名: " + PrivateKeyPath;
            return false;
        }

        try
        {
            string xml = File.ReadAllText(PrivateKeyPath).Trim();
            if (xml.Length > 0 && xml[0] == '\uFEFF')
            {
                xml = xml.Substring(1);
            }

            byte[] manifestBytes = File.ReadAllBytes(manifestPath);
            byte[] signature;
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(xml);
                signature = rsa.SignData(manifestBytes, CryptoConfig.MapNameToOID("SHA256"));
            }

            File.WriteAllBytes(manifestPath + ".sig", signature);
            Debug.Log("[ManifestSigner] 已签名 " + manifestPath);
            return true;
        }
        catch (Exception e)
        {
            error = "清单签名失败: " + e.Message;

            // 失败时让旧签名失效：否则目录里会残留「新清单 + 旧签名」，
            // 一旦被手工上传，全体客户端验签失败并静默停更。
            try
            {
                string stalePath = manifestPath + ".sig";
                if (File.Exists(stalePath))
                {
                    File.Delete(stalePath);
                    Debug.LogWarning("[ManifestSigner] 签名失败，已删除旧签名文件: " + stalePath);
                }
            }
            catch (Exception deleteError)
            {
                Debug.LogWarning("[ManifestSigner] 删除旧签名文件失败: " + deleteError.Message);
            }

            return false;
        }
    }
}
