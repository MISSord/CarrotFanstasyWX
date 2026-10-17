using System;
using System.Security.Cryptography;

/// <summary>
/// 热更清单签名。客户端只内置公钥；私钥在发布机的 CarrotFantasy/Keys 下，不进包。
/// </summary>
public static class ManifestSignature
{
    public const string SignatureFileName = "custom_manifest.json.sig";

    /// <summary>与 Keys/manifest_signing_private.xml 成对的公钥。</summary>
    public const string PublicKeyXml =
        "<RSAKeyValue><Modulus>tRMhVmU4HPPwYe9jkdHb0UIG8cypvGFNS5TB6eirAaZ/fsvHSZlskFwUjpchP0SwE4bbqpxSgCY9n7fhQlijcLHYaed9PYSFPA6e8TwAHohFqcEjj044M8gxZ/Qe2iUEPHb1xgZrF6fHuuBX2BbT7i9F0OM+nY8JhwaKrcZEsVScUDcJSaKdydgpMRKLUO81gTCN4APbcOYe9d7eywYwz6GJZimKtCgpwRVlNLtSM7OsYGkGbxx3SrinquWs0CJMHNdmYXghGo4XyGDl9ZJ8go0zSGg88kCIvX79uvaYMsFnzn8wrj5ZTDgazZP8RvY4a2vh32OJFgQOZmErHk5AAQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

    /// <summary>
    /// 仅调试阶段允许 http://。由 AssetBundlePathHelper 读取运行配置后设置；
    /// 正式构建（无 UNITY_EDITOR / DEVELOPMENT_BUILD / CF_DEV_TOOLS）恒为 false。
    /// </summary>
    public static bool AllowInsecureHttp { get; set; }

    public static bool IsAllowedServerUrl(string url, out string error)
    {
        return IsAllowedServerUrl(url, AllowInsecureHttp, out error);
    }

    public static bool IsAllowedServerUrl(string url, bool allowInsecureHttp, out string error)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            error = "下载地址为空";
            return false;
        }

        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            error = null;
            return true;
        }

        if (allowInsecureHttp && url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            error = null;
            return true;
        }

        error = allowInsecureHttp
            ? "下载地址只允许 https/file；要用 http 必须在运行配置里显式开启 allowInsecureHttp: " + url
            : "下载地址只允许 https 或 file，已拒绝: " + url;
        return false;
    }

    /// <summary>调试构建才允许放开 http；正式包始终走 https。</summary>
    public static bool IsInsecureHttpBuildAllowed()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || CF_DEV_TOOLS
        return true;
#else
        return false;
#endif
    }

    /// <summary>
    /// 开发期跳过清单签名的开关（编译期宏 <c>CF_DEV_SKIP_MANIFEST_SIGN</c>）。
    /// <para>
    /// 双重闸门：必须【同时】满足
    ///   ① 编译时定义了 CF_DEV_SKIP_MANIFEST_SIGN
    ///   ② 当前是 Editor / Development / CF_DEV_TOOLS 构建
    /// 才会真正跳过。正式包即使误留了该宏，②也不成立，仍然强制验签。
    /// </para>
    /// <para>
    /// 开关入口：菜单 Tools/AssetBundle/开发期跳过清单签名，或手工在
    /// Project Settings → Player → Scripting Define Symbols 里增删该宏。
    /// </para>
    /// </summary>
    public static bool IsVerificationBypassed
    {
        get
        {
#if CF_DEV_SKIP_MANIFEST_SIGN
            return IsInsecureHttpBuildAllowed();
#else
            return false;
#endif
        }
    }

    public static bool Verify(byte[] manifestBytes, byte[] signature)
    {
        if (IsVerificationBypassed)
        {
            UnityEngine.Debug.LogWarning(
                "[ManifestSignature] ⚠ 开发宏 CF_DEV_SKIP_MANIFEST_SIGN 生效，已跳过清单验签。"
                + "正式包必须移除该宏并重新打包与上传。");
            return true;
        }

        if (manifestBytes == null || manifestBytes.Length == 0 || signature == null || signature.Length == 0)
        {
            return false;
        }

        try
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(PublicKeyXml);
                return rsa.VerifyData(manifestBytes, CryptoConfig.MapNameToOID("SHA256"), signature);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("[ManifestSignature] 验签异常: " + e.Message);
            return false;
        }
    }
}
