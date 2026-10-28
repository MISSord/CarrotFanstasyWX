using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// AB 包布局：负责把 AssetDatabase / 规则翻译成「包名 + 资源列表 + 加载名」。
/// <para>
/// 抽这一层的目的：构建后端（SBP / built-in）只消费布局，不关心包是怎么划分的；
/// 阶段 C 的「视图包 / 共享包」规则只需要新增一个实现，不用改构建代码。
/// </para>
/// </summary>
public interface IAssetBundleLayout
{
    /// <summary>布局名（日志与 Profile 展示用）。</summary>
    string Name { get; }

    /// <summary>生成构建输入；失败时给出可直接展示的原因。</summary>
    bool TryCreate(
        BuildTarget target,
        out List<AssetBundleBuild> builds,
        out string error);
}

/// <summary>包布局模式。</summary>
public enum AbBundleLayoutMode
{
    /// <summary>包名来自资源 .meta（现状）。</summary>
    Meta = 0,

    /// <summary>包名由代码规则决定（阶段 C：视图包 / 共享包）。</summary>
    Code = 1,
}
