CarrotFantasyWX / Tools 脚本说明
================================

【Unity 构建 / 热更（需先设 UNITY_EDITOR 或 -UnityPath）】

unity-batch.ps1
  底层入口。用 Unity -batchmode 调用 BuildCli，支持 codeHotUpdate / abBuild / pcBuild。
  例：.\Tools\unity-batch.ps1 -Command codeHotUpdate -Target StandaloneWindows64
  例：.\Tools\unity-batch.ps1 -Command pcBuild -Channel prod

code-hotupdate.ps1
  薄封装：仅热更代码（HybridCLR Generate + 同步 DLL + 刷新清单），不重打 AB。
  例：.\Tools\code-hotupdate.ps1
  可选：-Upload  -RuntimeEnv dev|staging|prod

ab-build.ps1
  薄封装：完整 AB 打包（图集 → AB → 清单/Pack）。
  例：.\Tools\ab-build.ps1
  可选：-Upload  -ForceRebuild  -CopyStreaming  -RuntimeEnv prod

pc-build.ps1
  薄封装：PC Player 一键出包（通道 + Generate/同步 + BuildPlayer）。
  例：.\Tools\pc-build.ps1 -Channel dev
  例：.\Tools\pc-build.ps1 -Channel prod
  可选：-SkipGenerate
  产物：CarrotFantasy/Build/PC/{dev|prod}/{时间戳}/
  若刚切换 CF_DEV_TOOLS，脚本会自动重跑一次（exit 2 → retry）

说明：
  - 跑前 Unity 工程 Build Settings 激活平台需与 -Target 一致（默认 StandaloneWindows64）
  - 日志在仓库根 Logs/ 目录
  - 若提示禁止运行脚本：powershell -ExecutionPolicy Bypass -File .\Tools\xxx.ps1
  - 详细参数见 docs/BuildAndHotUpdateSOP.md 第 7 节

【协议 / 网络代码生成】

regen-game-network.ps1
  改 Proto/GameNetwork.proto 后执行：生成 GameNetwork.cs 到 Unity，并编译服务端。
  例：.\Tools\regen-game-network.ps1

regen-game-network.cmd
  同上，cmd 双击或命令行入口（内部调 ps1）。

【Git 工具】

restore-scripts-from-git.ps1
  把指定路径下已跟踪文件恢复为 git HEAD（丢弃本地改动）。
  例：powershell -ExecutionPolicy Bypass -File .\Tools\restore-scripts-from-git.ps1 -Path CarrotFantasy/Assets/Scripts -Force

【其它目录】

CfNet.ProtoGen/
  Proto 代码生成小工程，一般由 regen-game-network.ps1 调用，不必手跑。
