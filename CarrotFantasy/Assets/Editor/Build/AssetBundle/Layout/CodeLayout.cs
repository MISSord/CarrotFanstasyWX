using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 代码化布局入口。包划分规则写在这里，构建后端只消费结果。
/// <para>当前没有收编规则，结果与 <see cref="MetaLayout"/> 相同。</para>
/// </summary>
public sealed class CodeLayout : IAssetBundleLayout
{
    /// <summary>
    /// 最近一次 Code 布局实际产出的包名（供清单阶段校验/过滤依赖）。
    /// 只在编辑器构建流程内使用，单线程。
    /// </summary>
    public static IReadOnlyCollection<string> LastBundleNames { get; private set; }

    public string Name
    {
        get { return "Code"; }
    }

    public bool TryCreate(
        BuildTarget target,
        out List<AssetBundleBuild> builds,
        out string error)
    {
        LastBundleNames = null;
        if (!MetaLayout.Instance.TryCreate(target, out builds, out error))
        {
            return false;
        }

        var names = new List<string>(builds.Count);
        for (int i = 0; i < builds.Count; i++)
        {
            names.Add(builds[i].assetBundleName);
        }

        LastBundleNames = names;
        Debug.Log("[CodeLayout] 当前没有收编规则，包划分与 Meta 布局相同，共 " + builds.Count + " 个包。");
        return true;
    }
}
