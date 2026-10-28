using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// 代码化布局（阶段 C 实现）：按规则决定包名与资源归属，不再依赖 .meta。
/// <para>
/// 规划中的规则：
/// </para>
/// <list type="bullet">
///   <item>视图包：<c>ui/view/{view}_prefab</c> + 该视图 Images / 视图专属 Sprites（依赖闭包）；</item>
///   <item>共享包：fonts / ui animator / 共享 atlas / 共享 sprites 目录；</item>
///   <item>其余资源按现有 meta 规则兜底，逐步收编。</item>
/// </list>
/// <para>当前只占位：选择 Code 布局会明确失败，避免静默退回 Meta 而产生错误预期。</para>
/// </summary>
public sealed class CodeLayout : IAssetBundleLayout
{
    public string Name
    {
        get { return "Code"; }
    }

    public bool TryCreate(
        BuildTarget target,
        out List<AssetBundleBuild> builds,
        out string error)
    {
        builds = null;
        error = "Code 布局尚未实现（阶段 C：视图包 / 共享包规则）。当前请使用 Meta 布局。";
        return false;
    }
}
