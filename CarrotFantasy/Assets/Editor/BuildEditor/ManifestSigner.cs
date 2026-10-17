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
            return false;
        }
    }
}
