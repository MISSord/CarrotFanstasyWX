# EnhancedScroller 安装说明

[EnhancedScroller](https://assetstore.unity.com/packages/tools/gui/enhancedscroller-36378) 为 echo17 的商业插件（Asset Store 最新版 **2.40.2**，2025-07-10）。

## 获取插件

1. 在 [Unity Asset Store](https://assetstore.unity.com/packages/tools/gui/enhancedscroller-36378) 购买并下载；或
2. 在 [itch.io](https://echo17.itch.io/enhancedscroller-unity) 购买后下载 `.unitypackage`。

## 导入到本项目

1. 将下载的 `EnhancedScroller_*.unitypackage` 放到本目录（`CarrotFantasy/Packages/EnhancedScroller/`）。
2. 在 Unity 菜单选择：**CarrotFantasy → 第三方插件 → 导入 EnhancedScroller**。
3. 导入完成后，插件会位于 `Assets/ThirdParty/EnhancedScroller/`（与 DOTween 等第三方资源同级，并纳入 `Unity.ThirdParty` 程序集）。业务代码在 `Unity.Model` 中可直接 `using EnhancedUI.EnhancedScroller;`。

若已手动通过 **Assets → Import Package** 导入到 `Assets/EnhancedScroller/`，再次执行上述菜单会自动迁移到 `ThirdParty` 目录。

## 文档

- 官方手册：https://www.echo17.com/enhancedscroller.html
