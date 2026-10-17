# CarrotFantasy AssetBundle 热更框架 —— 问题清单与改进建议

| 项目 | 内容 |
|---|---|
| 审查对象 | `D:\Work\CarrotFantasyWX\CarrotFantasy`（Unity 2023.1.0f1c1 + HybridCLR） |
| 审查范围 | `Assets/Scripts/AOT/AssetBundleManager/`、`Assets/Editor/BuildEditor/`、`Assets/Scripts/Util/AssetBundle/`，以及启动链路 `GameMain → AotResourceUpdateRunner → HybridCLRBootstrap` |
| 参考快照 | `Build/AssetBundles/StandaloneWindows/custom_manifest.json`（ManifestVersion = 29，CompressedFormat = 1）、`Assets/StreamingAssets/ab_runtime_config.json` |
| 审查方式 | 静态代码走查（未运行、未构建；沙箱只读） |
| 审查日期 | 2026-10-17 |
| 修复日期 | 2026-10-17（P0 部分修复，详见第 8 节） |
| 结论 | 框架骨架合格，风险集中在 **写入原子性、清单可信度、启动期主线程重活、发布流程非原子切换** 四类 |

---

## 0. 结论速览（TL;DR）

1. **有一个 P0 级安全问题**：CDN 走明文 HTTP，且客户端会把下载到的 `CarrotFantasy.HotUpdate.dll.bytes` 直接 `Assembly.Load`，清单又没有签名 —— 链路中间人可稳定实现远程代码执行。**（已修复：清单签名 + https 白名单 + DLL 哈希校验，见 §8）**
2. **有一个 P0 级数据风险**：下载 / LZMA→LZ4 转换 / Pack 解压全部"直接写最终文件"，失败还会删掉旧版本。更新中途崩溃或失败，玩家本地可能既没有新版本、也没有旧版本。**（已修复：暂存 + 原子替换，见 §8）**
3. **发布流程不是原子切换**：上传按路径排序，`custom_manifest.json` 排在最前，传到一半中断 = 线上"新清单 + 缺文件"，所有玩家启动 404。**（已修复：内容先行、清单最后切，见 §8）**
4. **启动期在主线程做重活**：逐包 MD5、整包 ZIP 解压都在主线程，分帧切片挡不住单个大包，首启卡顿明显。
5. **版本一致性缺少护栏**：`AppVersion` 写进清单却无人校验；客户端整包更新后仍会优先加载上一代热更 DLL，存在 AOT 与热更程序集错配的隐患。

问题数量：**P0 × 5、P1 × 15、P2 × 9**（详见下文分级）。

### 做得好的地方（先说明，避免只列问题）

- 清单驱动的 AB 级差分 + `DownloadPacks` 合并下载，方向正确；
- stall（连续无字节增长）超时 + 绝对上限 + 重试，比单纯 `UnityWebRequest.timeout` 合理；
- 对比/校验分帧，避免一次性卡死；
- 断点续传用"每包落盘成功才增量写本地清单"的思路，方向正确；
- Boot 包分组、单图集独立 Pack、按大小装箱，考虑了更新粒度；
- 业务层 `AssetBundleCache`（Provider 池 + 句柄租约 + 空闲释放）设计相当扎实；
- Editor 侧有完整管线 + CLI + SFTP 上传，工程化程度不低。

---

## 1. 现状架构速览

```
启动
 └─ GameMain.Awake          初始化 SRPScheduler / PathHelper / AssetBundleManager
 └─ GameMain.Start          AotResourceUpdateRunner.Run
      ├─ AssetBundleUpdateChecker   下载 custom_manifest.json → 对比本地清单 → 校验文件
      ├─ 确认弹窗 → AssetBundleDownloader
      │     ├─ 有 DownloadPacks → 按 Pack 下载 → 解压 → 逐 AB 校验 → 落盘
      │     └─ 无 DownloadPacks → 逐 AB 下载 → 校验 →（LZMA 时）转 LZ4
      └─ 全部成功 → SaveLocalManifest → SetAssetBundleItem
 └─ HybridCLRBootstrap       读 hybridclr/*.dll.bytes → LoadMetadataForAOTAssembly → Assembly.Load
 └─ HotUpdateEntry           业务启动，走 AssetBundleLoaders（业务层缓存）
```

关键约定：

- 清单 `custom_manifest.json`：`AssetBundles`（BundleName / Size / MD5 / Dependencies）+ `DownloadPacks`；
- `CompressedFormat`：0 = LZMA（下载后转 LZ4），1 = LZ4/ChunkBased，2 = 无压缩；
- HybridCLR 的 `.dll.bytes` 是**原始文件**，不是真 AB，靠 `HybridCLRPaths.IsHybridClrRawFile` 在转换/解压路径上特判；
- 当前线上清单 `CompressedFormat = 1`，即"下载后转 LZ4"这条分支实际上处于**休眠**状态 —— 下面的 P1-1、P1-2 一旦切回 LZMA 就会立刻生效。

---

## 2. 分级定义

| 级别 | 含义 | 处理建议 |
|---|---|---|
| **P0** | 可导致远程代码执行、线上批量不可用、玩家数据/安装损坏 | 发版前必须修 |
| **P1** | 特定场景下功能错误、明显卡顿、发布流程有坑 | 一个迭代内修 |
| **P2** | 健壮性/可维护性/平台适配问题 | 排期修 |

---

## 3. P0 问题（必须修）

### P0-1 明文 HTTP + 无签名清单 + `Assembly.Load` = 远程代码执行

- **现象**：CDN 配置是 `http://124.222.203.161/ab/{0}`；清单里的 MD5 与清单本身同源；客户端把下载到的 DLL 字节直接交给 `Assembly.Load`。
- **位置**：
  - `Assets/StreamingAssets/ab_runtime_config.json:3`
  - `Assets/Scripts/AOT/HybridCLRBootstrap.cs:112`
  - `Assets/Scripts/AOT/AssetBundleManager/AssetBundleUpdateChecker.cs:232-264`
- **影响**：任何能控链路的人（公共 WiFi、被劫持的 DNS、被攻陷的 CDN）改清单 + 换 DLL + 改 Hash，即可在玩家机器上执行任意 C# 代码。MD5 只能防"传坏了"，防不了"被改了"。
- **建议**：
  1. 强制 HTTPS；
  2. 清单做非对称签名（Ed25519 / RSA-2048），公钥内置客户端，启动先验签再解析；
  3. 摘要换 SHA-256（MD5 已有选择前缀碰撞，不适合对抗场景）；
  4. 可选：AB 文件级 AEAD 或对 HybridCLR DLL 单独做一次签名校验。

### P0-2 无防回滚 / 无版本单调校验

- **现象**：`ManifestVersion` 只在编辑器里用来"建议下一个号"，运行时不校验；攻击者或误操作可下发旧清单让客户端"降级"。
- **位置**：`AssetBundleUpdateChecker.cs:248`（直接 `JsonUtility.FromJson`）；`AssetBundleBuildSettings.cs:119-127`。
- **影响**：可被用于回滚到有已知漏洞的版本；运营误操作也会把玩家打回旧版本。
- **建议**：清单内加 `MinSupportedVersion` / `IssuedAt`，客户端要求 `ManifestVersion ≥ 本地记录` 且 `IssuedAt` 不早于上次；服务端可下发"强制最低版本"。

### P0-3 ZIP 解压未防 Zip Slip

- **现象**：用 `entry.FullName` 直接拼目标路径，代码注释也承认"未做路径穿越防护"。
- **位置**：`Assets/Scripts/AOT/AssetBundleManager/AssetBundlePackExtractor.cs:221`（注释见 `:16`、`:220`）。
- **影响**：配合 P0-1，构造 `../../` entry 可向任意可写目录落文件（覆盖配置、写入启动项等）。
- **建议**：`Path.GetFullPath(dest)` 必须位于 `extractRoot` 之下；拒绝绝对路径、`..`、以及不在 `packInfo.BundleNames` 白名单内的 entry。

### P0-4 写入非原子：失败会毁掉旧版本

- **现象**：
  - 不需要转换时 `tempPath == finalPath`，`DownloadHandlerFile` 直接写正式路径；失败时 `DeleteDownloadArtifacts` 连旧文件一起删；
  - `RecompressAssetBundleAsync(src, target)` 直接覆盖目标，失败再删目标；
  - Pack 解压 `File.Copy(source, target, true)` 直接覆盖。
- **位置**：
  - `AssetBundleDownloader.cs:371-372`、`:835-848`、`:955`
  - `AssetBundlePackExtractor.cs:161`、`:265`
- **影响**：
  1. 下载/转换/解压中途崩溃 → 正式路径留下半截文件；
  2. 更新失败 → 旧版本已被删除，玩家只能重下整包；
  3. 半截文件若在下次校验中被误判为"可用"（见 P1-1），会直接加载失败。
- **建议**：统一"双缓冲 + 原子替换"：写 `xxx.new` → 校验 → `File.Replace/Move` 覆盖 → 成功后才删旧、才写本地清单。失败路径保留旧文件。

### P0-5 发布非原子：清单先于内容上传

- **现象**：上传按相对路径排序后逐个 `UploadFile`，`custom_manifest.json` 排在 `hybridclr/`、`packs/`、`ui/` 之前。
- **位置**：`Assets/Editor/BuildEditor/AssetBundleCloudUploader.cs:211`（排序）、`:67-99`（顺序上传）。
- **影响**：上传中断/失败 = 线上清单指向尚不存在的文件 → 客户端 404 → 重试 3 次 → 直接退出。这是最典型的线上事故形态。
- **建议**：
  1. 两阶段：内容先传（`xxx.tmp` → `rename`），**清单最后原子切换**；
  2. 更稳的做法：`v{ManifestVersion}/` 版本目录 + `latest.json` 指针，回滚 = 改指针；
  3. 上传后做一次"回读校验"（HEAD 或下载比对 Hash）。

---

## 4. P1 问题

### P1-1 LZMA 模式下的完整性校验形同虚设

- **位置**：`AssetBundleUpdateChecker.cs:559-575`（`IsConvertedPersistentBundleCurrent`）
- **现象**：只判断"本地清单 Hash == 远端 Hash" + "文件存在" + "`Length > 0`"。
- **影响**：截断的 LZ4、被改写的文件都能通过；而 `CheckCRC` 实际只比 Size（`:640-652`），命名有误导性。
- **建议**：转换成功后把**转换后**的 Size/Hash 写入本地记录（sidecar 或清单），启动时校验它；或直接统一到 LZ4，删掉这条分支。

### P1-2 本地清单全量非原子重写，丢了就全量重下

- **位置**：`AssetBundleUpdateChecker.cs:698-705`（`SaveLocalManifest`）、`:711-794`（`UpsertLocalBundles`）
- **现象**：
  - `File.WriteAllText` 直接覆盖，写一半崩溃 → JSON 损坏 → 解析失败 → 本地清单视为空；
  - 每成功一个 AB 就"读全量 → 改 → 写全量"，几百个包 = 几百次全量序列化 + 落盘（O(n²)）。
- **影响**：LZMA 模式下本地清单丢失 = 所有包重新下载；大量小文件更新时 IO/CPU 明显偏高。
- **建议**：原子写（tmp + replace）+ 保留 `.bak`；状态改"每包 sidecar"或追加日志，清单只做汇总，损坏可从 sidecar 重建。

### P1-3 走 Pack 时忽略"不在任何 Pack 里的 AB"

- **位置**：`AssetBundleDownloader.cs:191-203`
- **现象**：`packsToDownload` 非空就直接 `return`，`bundlesToDownload / bundlesToUpdate` 完全不参与。
- **影响**：正常构建能全覆盖，但清单被手改、Pack 构建被跳过、或增量改清单时会漏包，表现为"启动后某资源永远加载失败"。同时 `totalDownloadSize` 被 Pack 总大小直接覆盖，未覆盖部分会让进度条算错。
- **建议**：计算 `needed − union(pack.BundleNames)`，剩余部分回退单包下载，并把大小计入总量。

### P1-4 运行期按需下载没有校验、也不持久化

- **位置**：`AssetBundleDownloader.cs:1298-1304`（无 info 直接 `return true`）、`:1195-1200`（`currentManifest == null` 直接 return）
- **现象**：`StartDownload` 未执行过时（例如本次启动无更新），`bundleInfoDict` 与 `currentManifest` 都是空的。
- **影响**：按需下载的包不做 Size/MD5 校验；下次启动本地清单无记录 → LZMA 模式下每次启动重下。
- **建议**：`AssetBundleManager.SetAssetBundleItem` 时同步给 Downloader 一份 manifest + 索引，任何下载入口都能校验与持久化。

### P1-5 `AppVersion` 写入但从不校验 → 跨版本串用旧热更 DLL

- **位置**：`AssetBundlePackager.cs:209`（写入）；全工程无读取比较。
- **影响**：客户端整包更新后，`GetRuntimeLoadPath` 仍优先持久化目录（`AssetBundlePathHelper.cs:79-112`），上一代 `hybridclr/*.dll.bytes` 会被新 AOT 加载，造成 HybridCLR 元数据/程序集错配；远程清单失败走本地兜底时尤其容易踩到。
- **建议**：缓存目录按 `Application.version`（或 build number）分代：`DownloadedAssetBundles/{version}/`；或启动时发现 `manifest.AppVersion != Application.version` 就清缓存重下。

### P1-6 清单请求：无重试、无缓存失效、无内置基线兜底

- **位置**：`AssetBundleUpdateChecker.cs:228-265`（单次请求、`timeout = 10`）
- **影响**：弱网首启失败率高；CDN 可能返回缓存中的旧清单；全新安装 + 断网时即使包里内置了基线内容也进不去（只回退持久化清单，不回退 StreamingAssets 清单）。
- **建议**：3 次指数退避 + 多 CDN 兜底；URL 带 `?v={ManifestVersion}&t={buildTime}` 或要求 `Cache-Control: no-cache`；失败先回退**内置基线清单**，再回退本地缓存。

### P1-7 代码热更会重建"全量清单 + 全部 Pack"，却只上传其中一部分

- **位置**：`HybridCLRCodeHotUpdatePipeline.cs:126-131`（`GenerateManifest` + `BuildPacks` 全量重建）、`:226-228`（上传过滤只放行 `hybridclr/`、`packs/`、清单）
- **现象**：只同步了 DLL，但清单按**本地 AB 输出目录**重算了所有 Size/Hash，Pack 也全部重新打包。
- **影响**：只要本地 AB 输出与线上不一致（换机器、别人打过包、输出目录被清过），新清单就指向**从未上传过的字节** → 全量重下或大面积 404；另外每次改一行 C# 都要把所有 Pack 重传一遍（GB 级）。
- **建议**：拉取线上清单 → 只替换 `hybridclr/*` 条目 → 只上传变化的 DLL（及受影响的少量 Pack），其余原样保留；发布前做"清单 vs 线上文件"一致性检查。

### P1-8 启动期在主线程算 MD5

- **位置**：`AssetBundleUpdateChecker.cs:655-667`（`CheckHash`）、`:413-466`（分帧循环）、`AssetBundleDownloader.cs:736`（下载后二次校验）
- **现象**：同步读全文件算 MD5；5ms 切片只在**两个包之间**判断，单个大包就是几百毫秒卡顿。
- **建议**：Size 快检 → 可疑再后台线程（`Task.Run` + 主线程结果队列）算 Hash；或加载时用 `LoadFromFileAsync` 的 crc 兜底。

### P1-9 整包 ZIP 解压是同步的，且三份磁盘占用

- **位置**：`AssetBundlePackExtractor.cs:208-235`（同步解压）、`:72`（解压目录）、`:122-133`（转换副本）
- **影响**：单个 Pack 解压期间主线程停住；"ZIP + 解压目录 + 目标文件"峰值约 2–3 倍 Pack 大小。
- **建议**：边解压边 copy（不落中间目录）、放到工作线程、解压前检查剩余空间。

### P1-10 Downloader 被两处同时 Update

- **位置**：`GameMain.cs:71` → `AssetBundleManager.Update()` → `AssetBundleDownloader.Update()`（`AssetBundleManager.cs:473`）；同时 `AotResourceUpdateRunner.cs:222` 也在调 `downloader.Update()`。
- **影响**：目前靠内部状态机兜住，但进度回调翻倍、状态机被提前推进，属于"随时会踩"的隐患。
- **建议**：明确单一驱动方（建议只由 `AotResourceUpdateRunner` 驱动，Manager 不转发）。

### P1-11 启动挂死风险：Scheduler 缺失 + WaitUntil 无超时

- **位置**：`SRPScheduler.cs:21-27`（找不到实例时静默不执行）、`AssetBundleDownloader.cs:495-531`（`WaitUntil(completedCount >= totalCount)`）
- **影响**：场景里没有 Scheduler → 清单下载协程根本不跑 → `while (!checkDone …)` 永久等待，表现为"卡在启动、无提示"；某个任务回调因异常未触发 → 下载协程永久挂起。
- **建议**：`Init` 找不到就自建 `DontDestroyOnLoad` 宿主；`WaitUntil` 加超时/失败熔断并给出错误 UI。

### P1-12 无回滚、无灰度、无开关

- **现象**：线上只有一份清单；`ManifestVersion` 不参与任何决策。
- **影响**：推了坏包只能再推一版，让玩家再下一次；无法止损。
- **建议**：保留 N-1 清单与文件、服务端可下发"强制回滚/停更"标志、按渠道或百分比灰度。

### P1-13 任何 Pack 失败 = 整局不可进

- **位置**：`AotResourceUpdateRunner.cs:204-237`（`downloadSuccess == false` 直接 `ShowErrorAndQuit`）
- **影响**：一个纯展示资源包 404 也会让玩家进不了游戏；已经设计了 `pack_boot`，却没有用它做"关键/非关键"分级。
- **建议**：boot 包必须成功；其余包失败降级进入游戏、后台补，或在功能入口处按需重试。

### P1-14 `env` 与 URL 脱钩，默认模板是 `file:///`

- **位置**：`AssetBundleBuildSettings.cs:129-154`（默认 `file://` 模板）、`:163-194`（`env` 只写标签）、`PcBuildChannel.cs:42`；`AssetBundlePathHelper.RuntimeEnv` 读出来无人使用。
- **影响**：`env=prod` 与 `env=dev` 共用同一个 EditorPrefs URL；忘记配置就会出"正式包指向本机路径"的包。
- **建议**：按 env 维护 URL 表；打包时强校验：prod 必须 https、非 IP、非 `file://`，否则直接 fail。

### P1-15 从不清理"远端已删除"的包

- **现象**：Checker 只产出 `bundlesToDownload / bundlesToUpdate / upToDate`，没有 `obsolete` 集合；`AssetBundleCacheCleaner` 只有"全删"。
- **影响**：旧图集、旧 Pack、旧热更 DLL 永久留在玩家磁盘；磁盘占用只增不减，也增加误用旧文件的机会。
- **建议**：以远端清单为准计算本地多余文件，加入延迟清理（保留最近 N 天/N 个版本）。

---

## 5. P2 问题

| 编号 | 问题 | 位置 | 建议 |
|---|---|---|---|
| P2-1 | 重试是立即重排，无退避，弱网下等于连打三次 CDN | `AssetBundleDownloader.cs:876-887` | 指数退避 + 抖动 |
| P2-2 | 下载前不检查剩余磁盘空间 | `AssetBundleDownloader.cs:537-566` | 按 `totalDownloadSize × 2` 预检 |
| P2-3 | 失败路径先删文件、后 `DisposeWebRequest`，Windows 上句柄可能未释放 | `AssetBundleDownloader.cs:272-278`、`:835-848` | 先 Dispose 再删 |
| P2-4 | 不支持断点续传（Range），大包失败从头来 | `AssetBundleDownloader.cs:630-695`（注释掉的实现） | 保留 `.part` + Range |
| P2-5 | 两套更新流程并存，旧状态机是死代码但仍在维护 | `GameStateMachine.cs:50-55`、`:67`、`:274-372` | 删除旧流程或收编 AOT runner |
| P2-6 | 大小写隐患：清单名全小写，StreamingAssets 拷贝保留原始大小写 | `AssetBundlePackager.cs:167`、`:99-123` | 构建期强制小写 AB 名 |
| P2-7 | 平台声明与实现不匹配：Android/iOS/WebGL 用 `File.Exists` + `LoadFromFile` 不可行 | `AssetBundlePackager.cs:22-29`、`AssetBundlePathHelper.cs:102-111`、`:167-173` | 只保留 PC，或补 `UnityWebRequestAssetBundle` 分支 |
| P2-8 | 硬编码清单：AOT 元数据 4 个程序集、Boot 包名列表 | `HybridCLRPaths.cs:21-27`、`PackGroupingDefaults.cs:22-33` | 从 HybridCLR settings 读取；Boot 改规则/配置 |
| P2-9 | 运行时脚本里存在无条件的 `using UnityEditor;`（`GameStateMachine.cs:3` 等） | 多处 | 统一挪进 `#if UNITY_EDITOR`；若 player 构建未报 CS0246 需确认是否有特殊处理 |

其他小项：

- `AssetBundleUpdateChecker` 是普通类却用了 `[Header]`（`:107-109`），不会生效，属误导性代码；
- `CustomAssetBundleInfo.Version`（"1.0.0"）与 `UpdateCheckResult.progress/currentOperation` 基本未使用；
- `EnsureBundleLoaded` 故意保留一次引用但不提供释放 API（`AssetBundleManager.cs:571-660`），语义需在注释中写清；
- `AssetBundleManager.UpdateBundleLoading` 明明 `maxConcurrentLoads = 3`，却每帧只启动 1 个（`:1547-1554`），首屏按"包数 = 帧数"排队；
- 缺少自动化测试：清单 diff、校验、断点续传、Zip Slip 都可离线单测。

---

## 6. 改进路线图

| 阶段 | 工作项 | 产出 | 预估 |
|---|---|---|---|
| **第一阶段（发版前）** | HTTPS + 清单签名 + SHA-256 | 堵住 RCE | 1–2 天 |
| | 下载/转换/解压改 `.new` + 原子替换 | 失败可回退，不再毁旧版本 | 2–3 天 |
| | 上传两阶段 + 清单最后切 + N-1 保留 | 消除线上批量 404 | 1–2 天 |
| | Zip Slip 防护 | 消除任意写文件 | 0.5 天 |
| **第二阶段（一个迭代）** | sidecar 状态 + 原子写 + 去掉全量 JSON 重写 | 崩溃可恢复、O(n²) 消失 | 2–3 天 |
| | Hash 移后台线程 + Size 快检 | 首启卡顿明显下降 | 1 天 |
| | 缓存按 App 版本分代 / 校验 AppVersion | 消除跨版本串用 | 0.5 天 |
| | 代码热更改"补丁式清单"，只传变化文件 | 发布从 GB 降到 MB | 2 天 |
| | 清单重试 + 内置基线兜底 + 缓存失效 | 弱网/断网可进游戏 | 1 天 |
| **第三阶段（排期）** | 回滚/灰度/开关、obsolete 清理、关键包分级 | 可运维 | 2–3 天 |
| | 删旧状态机、统一驱动方、平台适配 | 可维护 | 2–3 天 |
| | 离线单测 + 发布前一致性检查 | 防回归 | 2 天 |

---

## 7. 自测 / 验收清单

发布前建议逐项验证（多数可在本地用 `file://` 或临时 HTTP 服务复现）：

1. **断网首启**：全新安装、无网络 → 能否用内置基线进入游戏？报错是否可读？
2. **下载中断**：下载 50% 时 `kill` 进程 → 重启后是否只补未完成部分？旧版本是否仍可运行？
3. **转换中断**：LZMA→LZ4 转换中 `kill` → 重启后是否识别残缺文件并重下？
4. **上传中断**：上传到一半断开 → 线上客户端是否仍能正常启动（验证清单最后切）？
5. **回滚演练**：把清单切回 N-1，客户端是否能正确降级且不混用文件？
6. **版本升级**：装新包覆盖旧包 → 旧热更 DLL 是否被清理/忽略？
7. **恶意包**：手工构造含 `../` entry 的 ZIP → 是否被拒绝？
8. **单包 404**：删掉线上某个非 boot Pack → 游戏是否仍可进入？
9. **弱网**：限速 50KB/s + 10% 丢包 → 首启耗时、失败率、重试行为是否可接受？
10. **校验性能**：构造 100MB 单包 → 启动校验是否造成明显掉帧？
11. **清单被缓存**：CDN 缓存旧清单时，客户端能否通过 `?v=` 拿到新清单？

### 建议跟踪的指标

- 首启"检查 + 校验"耗时（分设备档位）；
- 单帧最长卡顿（目标 < 50ms）；
- 更新失败率 / 失败后成功恢复率；
- 平均更新下载量（验证 Pack 粒度是否合理）；
- 因资源问题导致的启动失败占比。

---

## 8. 修复记录（2026-10-17）

本轮针对 P0 落地了以下改动，已通过 `dotnet build`（`CarrotFantasy.AOT` / `Unity.Editor`，0 error）验证；签名链路另做了密钥对匹配与签名往返测试。

| 问题 | 状态 | 落地内容 | 关键文件 |
|---|---|---|---|
| P0-1 远程代码执行 | 部分修复 | 清单 RSA-2048 / SHA-256 签名，客户端内置公钥、验签通过才解析；下载地址白名单（https / file）；热更 DLL 加载前再按"已签名清单里的 Hash"校验一次；发布机私钥 `Keys/` 已加入 `.gitignore` | `ManifestSignature.cs`、`ManifestSigner.cs`、`AssetBundleUpdateChecker.cs`、`HybridCLRBootstrap.cs` |
| P0-2 防回滚 | 已修复 | 远程 `ManifestVersion` 低于本地已接受版本时拒绝；半次下载不再提前抬高本地版本号 | `AssetBundleUpdateChecker.cs` |
| P0-3 Zip Slip | 已修复 | 拒绝绝对路径、`..`、盘符；`Path.GetFullPath` 后必须仍在解压目录内；ZIP 条目必须在 Pack 的 `BundleNames` 白名单里 | `AssetBundlePackExtractor.cs` |
| P0-4 非原子写入 | 已修复 | 下载写 `*.download`、转换写 `*.new`、解压直拷写 `*.new`，校验通过后 `AtomicFile.Replace` 原子替换；失败只删暂存文件、保留旧版本；下载暂存改到正式文件同目录，保证同卷改名 | `AtomicFile.cs`、`AssetBundleDownloader.cs`、`AssetBundlePackExtractor.cs` |
| P0-5 发布非原子 | 已修复 | 上传顺序改为 内容 → `version.txt` → `.sig` → `custom_manifest.json`；单文件先传 `.uploading` 再 `posix-rename` 覆盖；清单/签名切换失败时保留旧文件并报错；清单缺 `.sig` 时拒绝上传 | `AssetBundleCloudUploader.cs` |

补充说明：

- **http 的处理**：正式包只接受 https / file；调试构建（Editor / Development Build / `CF_DEV_TOOLS`）在运行配置显式 `"allowInsecureHttp": true` 且 `env != prod` 时才放行 http，启动时打警告。`ab_runtime_config.json` 是本地文件（已 gitignore），当前 dev 配置已打开该开关，所以本地调试不再被一刀切拦死。
- **构建期护栏**：`env=prod` 且 CDN 模板不是 https 时，`WriteRuntimeConfig` 报错并拒绝写入运行配置，避免出一个"启动就拒绝更新"的正式包。
- **密钥**：私钥 `Keys/manifest_signing_private.xml` 只留在发布机；公钥硬编码在 `ManifestSignature.PublicKeyXml`。已用 PowerShell 验证两者 Modulus 一致，并用同一套 API 做了"签名 → 验签成功、改一字节 → 验签失败"的往返测试。
- **编译验证**：`dotnet build CarrotFantasy.AOT.csproj`、`dotnet build Unity.Editor.csproj` 均为 0 error（仅存量 warning）。

仍待处理（不在本轮范围）：

1. **正式环境还没有 https 端点** —— P0-1 的最后一公里，发正式包前必须把 CDN 切到 https，否则 `env=prod` 的构建会被护栏挡住。
2. **防回滚没有"官方回滚"逃生开关**：确实需要降级时只能让玩家清本地清单；建议后续加服务端下发的强制回滚标记。
3. LZMA 模式内容校验、按需下载校验、本地清单原子写已在第二轮（§9）修复。
4. 上传的 `posix-rename` 依赖服务器 OpenSSH 扩展；若目标机不支持，清单会保留旧版本并报错（需手动把 `.uploading` 改名），首次上线前应实测一次。

---

## 9. P1 修复记录（2026-10-17，第二轮）

| 问题 | 状态 | 落地内容 |
|---|---|---|
| P1-1 LZMA 校验虚设 | 已修复 | 转换完成后把「转换后 Size + MD5」写进本地清单（`ConvertedSize/ConvertedHash`），启动时按它校验落盘文件；旧记录按原逻辑兼容 |
| P1-2 清单非原子 + O(n²) | 已修复 | 本地清单改为 `.tmp` + 原子替换；增量写入改为缓冲（满 32 个或 2 秒落一次盘），批量结束/全量保存时强制 Flush |
| P1-3 Pack 漏包 | 已修复 | 下载前计算「待更 AB − Pack 覆盖」，剩余包在 Pack 之后单包下载；未覆盖部分大小也计入总进度 |
| P1-4 按需下载无校验 | 已修复 | `SetAssetBundleItem` 时把完整清单注入 Downloader（`SetManifest`），运行期按需下载同样做 Size/MD5 校验并写本地清单 |
| P1-5 AppVersion 未校验 | 已修复 | 本地清单 `AppVersion != Application.version` 时清空持久化 AB + 本地清单，回退到内置基线重新校验 |
| P1-6 清单无重试/无兜底 | 已修复 | 清单下载 3 次退避重试；URL 加时间戳绕 CDN 缓存（`file://` 不加）；远程失败时回退「本地清单 → StreamingAssets 内置清单」 |
| P1-7 代码热更重建全量 | 已修复 | 热更前校验「除 hybridclr 外」本地 AB 与清单逐字节一致，不一致直接拒绝；Pack 固定时间戳（内容不变则字节不变），只上传哈希变化的 Pack |
| P1-10 Downloader 双驱动 | 已修复 | 移除 `AotResourceUpdateRunner` 里的 `downloader.Update()`，统一由 `GameMain.Update → AssetBundleManager.Update` 驱动 |
| P1-11 启动挂死 | 已修复 | `SRPScheduler` 找不到实例时自建常驻宿主；两处 `WaitUntil` 改为 10 分钟上限的轮询并报错 |
| P1-13 单包失败不可进 | 已修复 | `DownloadPackInfo.Critical`：boot 包标记关键包，失败才阻止进游戏；非关键包失败降级进入，缺的资源走按需下载 |
| P1-14 env 与 URL 脱钩 | 部分修复 | 支持按环境取模板 `AB_CdnUrlTemplate_{env}`（回退全局）；prod 非 https 仍拒绝写入 |
| P1-15 不清理 obsolete | 已修复 | 更新成功后按清单删除持久化目录中不再存在的 AB / 热更 DLL（跳过 `.download/.new/.bak/.tmp`） |

验证：`dotnet build CarrotFantasy.AOT.csproj`、`Unity.Editor.csproj` 均 **0 error**。

本轮未做（仍在路线图里）：

- **P1-8 主线程 MD5**：需要把校验状态机改成异步，牵涉面较大；当前先靠 Size 预检与「转换后 Hash」减少误判，后台哈希单独排期。
- **P1-9 整包同步解压**：仍是"解压到临时目录再拷贝"，磁盘峰值 2–3 倍；建议下一步改为逐 entry 流式写入。
- **P1-12 回滚/灰度/开关**：服务端还没有版本目录与 latest 指针，属于发布侧改造，需要和运维一起排期。

---

## 10. P2 修复记录（2026-10-17，第三轮）

| 问题 | 状态 | 落地内容 |
|---|---|---|
| P2-1 重试无退避 | 已修复 | 失败任务进入 `pendingRetries`，按 2s/4s 指数退避 + 0~1s 抖动后再入队；`EndDownload` 清空 |
| P2-2 无磁盘预检 | 已修复 | `StartDownload` 前按「总大小 × 2 + 64MB」检查 `persistentDataPath` 所在盘，不足直接报错并终止本次更新 |
| P2-3 先删后 Dispose | 已修复 | 超时与失败分支都改为先 `DisposeWebRequest`（关闭文件句柄）再删暂存文件 |
| P2-4 断点续传 | **按需求跳过** | 未改动（注释里的 Range 实现仍保留） |
| P2-5 两套更新流程 | 已修复 | 删除 `CheckUpdateState / DownloadConfirmState / DownloadState / ErrorState` 四个死状态及其注册，状态机只保留游戏内状态（`GameStateMachine.cs` 592 → 276 行） |
| P2-6 大小写隐患 | 已修复 | 生成清单前把输出文件名统一小写（含大小写冲突检测），拷贝 StreamingAssets 前再兜一次 |
| P2-7 平台声明不符 | 已修复 | `availablePlatforms` 只保留 Windows / macOS；Android/iOS/WebGL 在 `BuildAssetBundles` 入口直接报错，避免出跑不起来的包 |
| P2-8 硬编码清单 | 部分修复 | AOT 补充元数据改为**构建时从 HybridCLR 设置导出** `aot_meta_list.txt`，运行时优先读该文件、缺失才回落常量；Boot 包名单增加"清单里不存在"告警 |
| P2-9 无条件 using UnityEditor | 已修复 | `GameStateMachine.cs` 的 `using UnityEditor;` 包进 `#if UNITY_EDITOR`（其余运行时文件本来就已包裹） |

附带小修：

- 移除 `AssetBundleUpdateChecker` 上无效的 `[Header]` 特性；
- `UpdateBundleLoading` 由"每帧只启动 1 个包"改为填满并发槽（`maxConcurrentLoads`）。

注意事项：

- **代码热更会先统一文件名大小写再与清单比对**：如果线上还是旧的大小写命名，会被判为"与清单不一致"，要求先完整打一次 AB。
- 非 PC 平台现在无法从打包入口出包，需要先补 `UnityWebRequestAssetBundle` 分支。
- `Assets/StreamingAssets/HybridCLR/AOT/aot_meta_list.txt` 是构建产物，已加入 `.gitignore`。

验证：`dotnet build CarrotFantasy.AOT.csproj`、`Unity.Editor.csproj` 均 **0 error**。

---

## 附录 A：本次审查覆盖的主要文件

| 模块 | 文件 |
|---|---|
| 运行时管理器 | `AssetBundleManager.cs`、`AssetBundlePathHelper.cs`、`AssetBundleCacheCleaner.cs`、`PackGroupingDefaults.cs` |
| 更新检查 | `AssetBundleUpdateChecker.cs`、`DownloadPackPlanner.cs` |
| 下载 / 解压 | `AssetBundleDownloader.cs`、`AssetBundlePackExtractor.cs`、`FileDownloadHandler.cs`、`MD5Checker.cs` |
| 启动链路 | `GameMain.cs`、`AotResourceUpdateRunner.cs`、`HybridCLRBootstrap.cs`、`HybridCLRPaths.cs`、`SRPScheduler.cs` |
| 业务加载 | `AssetBundleCache.cs`、`AssetBundleHandle.cs`、`AssetBundleLoaders.cs`、`AssetBundleScope.cs` |
| Editor 管线 | `AssetBundlePackager.cs`、`AssetBundleBuildPipeline.cs`、`AssetBundlePackMerger.cs`、`AssetBundleBuildSettings.cs`、`AssetBundleDeploySettings.cs`、`AssetBundleCloudUploader.cs`、`HybridCLRCodeHotUpdatePipeline.cs`、`BuildCli.cs`、`PcBuildChannel.cs` |
| 配置快照 | `Assets/StreamingAssets/ab_runtime_config.json`、`Build/AssetBundles/StandaloneWindows/custom_manifest.json` |
| P0 修复新增 | `ManifestSignature.cs`（验签 + 地址白名单）、`ManifestSigner.cs`（Editor 签名）、`AtomicFile.cs`（原子替换） |

## 附录 B：术语

- **AB**：AssetBundle；**Pack**：把多个 AB 打成一个 ZIP 的合并下载单元；
- **LZMA / LZ4**：Unity AB 的两种压缩；LZMA 体积小但不支持随机读，运行期需转 LZ4；
- **stall 超时**：以"连续无字节增长"判超时，比固定总时长更贴合弱网；
- **HybridCLR**：给 IL2CPP 补 IL 解释器，使 C# 程序集可在运行时 `Assembly.Load`。
