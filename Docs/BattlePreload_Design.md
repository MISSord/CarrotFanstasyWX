# 战斗/关卡预加载设计（外部框架调研）

> 定位：**通用设计参考**，依据来自 Unity 官方文档、YooAsset、Addressables 及若干开源实现，不绑定任何具体工程。
> 结论均有出处，见 §10 参考资料。

---

## 0. TL;DR

预加载做不好，90% 的原因是**只做了其中一层**。业界把"资源可用"拆成四个互相独立、都要单独处理的层级（§1）：

| 层 | 内容 | 漏掉的后果 |
|---|---|---|
| **L1** | 网络下载 + 落盘缓存 | 进战斗时才开始下载 → 卡进度条几十秒 |
| **L2** | AssetBundle 载入内存 | 第一次取资源触发包加载 → 单帧尖峰 |
| **L3** | 资源对象解码（Prefab/Sprite/Clip） | 第一次 Instantiate 才解码 → 明显卡顿 |
| **L4** | GPU / 运行时准备（着色器变体、对象池、字体） | **第一次渲染才编译着色器 → 最典型的首战卡顿** |

**L4 是最常被忽略、也是体感最强的一层。**

---

## 1. 预加载的四个层级

### L1 · 下载与落盘

远端内容必须先落到本地缓存，否则后续所有层都无从谈起。

**Addressables 的做法**（[Preloading dependencies](https://docs.unity3d.com/Packages/com.unity.addressables@1.20/manual/DownloadDependenciesAsync.html)）：

```csharp
// ① 先问要下多少字节
AsyncOperationHandle<long> sizeOp = Addressables.GetDownloadSizeAsync(key);
yield return sizeOp;
// 已全部缓存时 Result == 0

// ② 按 key 批量下载全部依赖
if (sizeOp.Result > 0)
{
    yield return Addressables.DownloadDependenciesAsync(key, autoReleaseHandle: true);
}
```

关键设计点：**用 label 做"预加载集合"**。官方明确建议把要预下载的资源打同一个标签（例如 `preload`），用标签作为 key —— 这样"某个关卡需要哪些包"就是构建期配置，而不是运行期硬编码列表。

> "if you have a set of assets that you want to pre-download, you can assign the same label, such as `preload`, to the assets and use that label as the key"

**YooAsset 的做法**（[关于预下载](https://www.yooasset.com/docs/minigame/MiniGamePreload)、[预下载后续版本内容](https://www.yooasset.com/docs/solution/PrefetchManifest)）：

```csharp
var options    = new ResourceDownloaderOptions(maximumConcurrency: 8, retryCount: 3);
var downloader = package.CreateResourceDownloader(options);
if (downloader.TotalDownloadCount == 0) yield break;   // 已完整
downloader.StartDownload();
yield return downloader;
```

并通过 `IWebPreloadStrategy` 接入小游戏平台的"按 URL 预下载到平台缓存"能力。三条来自该文档的**硬约束**：

1. **小游戏平台不支持同步下载，必须用异步下载器。**
2. **部分平台无法返回可靠的下载字节数与进度**，下载完成后需要用包大小回填。
3. **预下载 URL 与资源加载 URL 必须一致，否则命中不了平台缓存。**

### L2 · AssetBundle 载入内存

包体本身要先进内存，之后从中取资源才快。YooAsset 在讲小游戏同步加载时给出的折中方案，本质就是"**把 L2 与 L3 拆成两步**"（[关于同步加载](https://www.yooasset.com/docs/minigame/SyncLoading)）：

```csharp
_bundleHandle = package.LoadBundleFileAsync("prefab_location");  // L2：只加载包体并持有句柄
yield return _bundleHandle;
GameLogic();                                                     // 此时业务层可以随便取资源
```

**这是整个预加载设计的枢纽**：把"包驻留"和"资源加载"解耦后，才能做到"进入战斗前把包全部驻留，战斗中随取随用"。

### L3 · 资源对象加载

即把包内的具体资源解码成引擎对象。这一层的接口在两大框架里都是 `LoadAssetAsync` / `LoadAssetSync`。注意 YooAsset 的实现细节（[Loading 源码说明](https://github.com/Besty0728/Unity-Skills/blob/3382be60/SkillsForUnity/unity-skills~/skills/yooasset-design/LOADING.md)）：

> "All `Load*Sync` calls internally call `handle.WaitForAsyncComplete()` after starting **the same async provider**."

**即所谓同步只是"异步 + 阻塞等待"，不是更快的路径。** 所以预加载应该一律异步，同步 API 只在万不得已时用。

另外这一层要注意 **`priority` 参数**（YooAsset 每个 `Load*Async` 都带 `uint priority`）：它决定队列里谁先被 pump，能改善"多个请求竞争时谁先就绪"，但**不能缩短单个请求的最短完成时间**。

### L4 · GPU 与运行时准备（最容易被漏掉）

Unity 官方对着色器加载的描述（[着色器加载](https://docs.unity3d.com/cn/2022.1/Manual/shader-loading.html)）：

> "Unity 第一次需要使用着色器变体渲染几何体时，它将该变体的数据传递给图形驱动程序。图形驱动程序在 GPU 上创建该变体的表示……**缺点是第一次使用变体时可能会出现明显的停顿**。"

> "To avoid visible stalls at performance-intensive times, Unity can ask the graphics driver to create GPU representations of shader variants before they're first needed. This is called **prewarming**."

**预热的三种官方手段**：

| 手段 | 说明 |
|---|---|
| `ShaderVariantCollection.WarmUp` | 按收集好的变体集合预热 |
| `Experimental.Rendering.ShaderWarmup` | 可精确提供顶点布局与渲染状态 |
| `Shader.WarmupAllShaders` | 预热内存中所有 Shader 的所有变体（最重） |

也可以在 Graphics Settings 的 **Preloaded shaders** 里挂变体集合，应用启动时自动预热。

**⚠️ 一个关键陷阱（官方明确警告）**：

> "If you build for DirectX 12, Metal or Vulkan, the graphics driver can only create an accurate GPU representation of a shader variant **if it knows the exact vertex data layout and render state**. If a prewarmed GPU representation is inaccurate, **Unity might still stall** when it needs to create the accurate representation."
> "To create accurate variants and avoid stalling, you should prewarm by **rendering Materials off-screen**."

也就是说：**在 DX12/Metal/Vulkan 上，光调 `ShaderVariantCollection.WarmUp` 可能不够** —— 需要在加载阶段把战斗里会用到的材质**离屏渲染一遍**，才能真正消除首帧卡顿。这个做法常被忽略，但它是"预热了还是卡"的根因。

**L4 还包括**：
- **对象池预热** —— 池首次 `Instantiate` 时的组件初始化、网格合并、渲染初始化都是尖峰。开源池实现（如 [unity-prefab-pool](https://github.com/gilzoide/unity-prefab-pool)）都支持在 Inspector 里配置初始池大小，即构建期声明预热数量。
- **字体/图集** —— 首个中文文本渲染会触发字体图集重建。
- **动画控制器/Avatar** —— 首次播放时的重定向与状态机初始化。

**度量手段**：Profiler 标记 `Shader.Parse`（创建变体数据表示并送 GPU）与 `CreateGPUProgram`（上传程序并等待 GPU）。

---

## 2. 预加载的四种时机

| 时机 | 特征 | 适用 | 代价 |
|---|---|---|---|
| **A. 启动预加载** | 全屏 loading，用户容忍度高 | 全局共享资源、首包外的必需包 | 拉长冷启动 |
| **B. 进入战斗前（阻断）** | 全屏 loading，**有明确进度** | 本局战斗必需的一切（L2+L3+L4） | 玩家等待；**必须给进度与文案** |
| **C. 战斗中预测（后台）** | 不阻断，低优先级 | 下一波怪、下一关、可能的 UI | 与当前战斗抢 IO/CPU；**必须限预算** |
| **D. 常驻** | 永不清除 | 字体、通用音效、核心 UI 图集 | 常驻内存 |

**C 与 B 的取舍**是一个常见分歧点。两条经验规则：

1. **能在 B 做完的，不要留给 C。** C 的不确定性（时机、预算、玩家操作）远高于 B。
2. **C 只用于"确定会用但不确定何时用"的资源**，且必须限帧预算（§4），否则会变成战斗卡顿的新来源。

**YooAsset 的启动页方案**（[启动页面热更](https://www.yooasset.com/docs/solution/BootSceneUpdate)）给了一条值得借鉴的边界原则：把启动画面单独打成一个 package（`BootPackage`），并明确要求"**启动流程结束后，记得优先销毁 BootPackage**"。即**启动专用的资源要与游戏资源隔离，用完立刻回收**，避免启动态内存一直挂着。

---

## 3. 战斗预加载清单（checklist）

按层级组织，**加粗的是最常被漏掉的**：

### L1 下载
- [ ] 本局所需的全部 AB 包（含依赖包）已落盘
- [ ] 落盘校验（大小/哈希）通过，避免半包
- [ ] 平台缓存 URL 与实际加载 URL 一致（小游戏）

### L2 包驻留
- [ ] 本局全部 AB 包已 `LoadBundleFileAsync` 并**持有句柄**
- [ ] **依赖包也已驻留**（只驻留主包是最常见的漏项）
- [ ] 句柄生命周期覆盖整局战斗

### L3 资源对象
- [ ] 本局出现的**全部塔/单位/子弹/道具**模板
- [ ] **全部波次的怪**（或按波次分批，见下）
- [ ] 地图/关卡配置数据
- [ ] 战斗内 UI 资源（HUD 图标、按钮三态、进度条）
- [ ] 特效预制体
- [ ] **共享材质/贴图**（通常与模板不同包）

### L4 GPU / 运行时
- [ ] **战斗用到的材质离屏渲染一遍**（DX12/Metal/Vulkan 必需）
- [ ] 着色器变体集合预热
- [ ] **对象池按预期峰值预热实例**
- [ ] 战斗内字体/图集已生成
- [ ] 首个动画控制器已初始化

### 释放侧（与预加载对称，别忘）
- [ ] 战斗结束释放本局 pin 的包与模板
- [ ] 保留项（常驻/冷却期）已明确列出
- [ ] 启动专用资源（BootPackage 类）已销毁

> **按波次分批的取舍**：把全部波次的怪一次性预加载，内存峰值可能过高（尤其小游戏）。可行折中是"预加载前 N 波 + 战斗中预测加载后续波次"，但这就把风险转移到了 C，需要 §4 的预算控制兜底。

---

## 4. 分帧与预算

预加载最怕两件事：**单帧尖峰**（把一次 200ms 的活塞进一帧）和**并发过高**（IO/CPU 争抢反而更慢）。

### 时间预算：Unity 官方包的现成范式

Unity 自家的 glTFast 包提供了一个标准实现 [`TimeBudgetPerFrameDeferAgent`](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.12/api/GLTFast.TimeBudgetPerFrameDeferAgent.html)，可直接借鉴其接口设计：

> "Claims a certain fraction of the target frame time and keeps track of whether this time frame was surpassed."

| 成员 | 语义 |
|---|---|
| `SetFrameBudget(float newFrameBudget = 0.5)` | **每帧预算，单位是"目标帧时间的比例"，默认 0.5** |
| `ShouldDefer()` / `ShouldDefer(float duration)` | 剩余工作是否应推迟到下一帧；可传入**预估耗时** |
| `BreakPoint(float duration)` | 条件让出：需推迟则 `Task.Yield()`，否则同步继续 |
| `[DefaultExecutionOrder(-10)]` | 保证在常规逻辑之前跑 |

**三个值得抄的设计点**：

1. **预算用"目标帧时间的比例"而不是绝对毫秒数** —— 这样在高帧率设备上自动收紧、低帧率设备上自动放宽，无需按机型调参。
2. **`ShouldDefer` 接受"预估耗时"** —— 让调用方在动手前就能判断"这活要不要留到下一帧"，而不是做了一半才发现超了。
3. **`BreakPoint` 是条件让出而非无脑 `yield`** —— 预算没用完就同步继续，避免每步都白等一帧。

### 并发上限

YooAsset 的下载器示例给出 `maximumConcurrency: 8, retryCount: 3` 作为起点。但注意：

- 下载并发与**加载并发**是两个不同的池。下载受网络带宽约束，加载受 IO/CPU 约束。
- 小游戏/WebGL 上并发过高收益递减甚至倒退（单线程 + 平台层排队）。

### 串行 vs 并行

| 可并行 | 必须串行 |
|---|---|
| 网络下载（受最大并发限制） | **着色器预热/离屏渲染**（GPU 与主线程） |
| 多个独立包的 IO | **`Instantiate`**（主线程） |
| 资源解码（引擎内部异步） | 依赖关系（依赖包必须先于主包，资源必须先于实例化） |

---

## 5. 进度与反馈设计

### 5.1 进度口径：计数 vs 字节（Addressables 官方明确区分）

> "`PercentComplete`: reports the percentage of **sub-operations that have finished**. For example, if an operation uses six sub-operations to perform its task, the `PercentComplete` indicates the entire operation is 50% complete when three of those operations have finished (**it doesn't matter how much data each operation loads**)."
> "`GetDownloadStatus`: reports the percentage **in terms of total download size**."

**这是进度条跳变的经典原因**：用 `PercentComplete` 做进度条，6 个子任务里前 3 个是 1KB 的小包、后 3 个是 10MB 的大包，进度条会"先冲到 50% 然后卡住"。**下载阶段应该用字节口径（`GetDownloadStatus` / `TotalDownloadSize`），加载阶段才用计数口径。**

### 5.2 节流上报

Addressables 官方示例里的做法值得直接照搬：

```csharp
if (percentageComplete > progress * 1.1) // Report at most every 10% or so
{
    progress = percentageComplete;
    ProgressEvent.Invoke(progress);
}
```

**每 ~10% 才上报一次**。原因：进度回调本身会触发 UI 重建，高频上报反而拖慢加载。

### 5.3 加权总进度

整个"进入战斗"是多个阶段串起来的，直接取平均会让进度条前快后慢。应当**按各阶段的实测耗时加权**：

```
总进度 = Σ(阶段权重 × 阶段内进度)
权重按实测耗时标定，而不是按"感觉"分配
```

### 5.4 反馈设计（避免"假死"观感）

| 手段 | 目的 |
|---|---|
| 阶段文案（"正在准备战场…"） | 让玩家知道没卡死 |
| 进度条 + 百分比 | 基础预期管理 |
| **最小展示时长**（如 0.5s，防闪烁） | 快时不要闪一下；慢时不要突然跳出 |
| 旋转/呼吸动画 | 掩盖阶段内的进度停滞 |
| 可交互内容（提示、小贴士） | 拉高等待容忍度 |

> 反面模式：**用假进度条**（定时器匀速走）。一旦真实加载比假进度慢，就会出现"100% 之后还等很久"，体感比没有进度条更差。

---

## 6. 释放与预加载的对称性

预加载的反面是释放，两者必须**成对设计**，否则会出现 Unity 官方命名的 **asset churn**：

> "Asset churn happens if you release an object that's the last item in an AssetBundle, and then immediately reload either that asset or another asset in the AssetBundle. **You should keep assets for as long as possible before unloading them.**"
> —— [Addressables: Managing Addressable asset memory](https://docs.unity3d.com/Packages/com.unity.addressables@2.7/manual/memory-assets.html)

同一页还有一条结构性事实，说明"即时释放"是错觉：

> "you can load an AssetBundle, or its partial contents, but **you can't unload part of an AssetBundle**."

**YooAsset 干脆不提供逐资源即时卸载**，而是提供独立的 sweep API，并明确建议调用时机（[资源卸载](https://www.yooasset.com/docs/2.3.x/guide-runtime/ResourceUnload)）：

> "卸载所有引用计数为零的资源包。**可以在切换场景之后调用资源释放方法或者写定时器间隔时间去释放。**"

即：**释放应当是"批量 + 延迟 + 在受控时机"，而不是"引用归零立即释放"。**

### 战斗结束的两类处理

| 类别 | 处理 | 理由 |
|---|---|---|
| **本局专属**（本关地图、本关独有的怪） | 释放 | 不会再用了 |
| **跨局共享**（通用塔、通用音效、UI 图集） | **保留**（常驻或走冷却期） | 下一关立刻还要用，释放必触发 churn |

**对称性检查**：预加载清单（§3）里每一项，都要在设计时回答"什么时候释放"。答不出来的项，就是将来的内存泄漏点。

---

## 7. 常见陷阱

| # | 陷阱 | 症状 | 修法 |
|---|---|---|---|
| 1 | 只预加载包（L2），不加载资源（L3） | 第一次 Instantiate 仍卡 | L2 之后补一遍 `LoadAssetAsync` |
| 2 | 只预加载资源（L3），不预热着色器（L4） | 第一次渲染卡一下 | 材质离屏渲染 / `ShaderVariantCollection.WarmUp` |
| 3 | 在 DX12/Metal/Vulkan 上只用 `WarmUp` 不渲染 | **预热了还是卡** | 必须离屏渲染材质提供准确顶点布局与渲染状态 |
| 4 | 只预加载不预热对象池 | 首波怪生成尖峰 | 按预期峰值预热实例 |
| 5 | 只驻留主包，漏掉依赖包 | 取资源时才发现缺包 | 预加载时展开全部依赖 |
| 6 | 进度条用计数口径做下载进度 | 进度先冲后卡 | 下载用字节口径 |
| 7 | 进度回调每帧上报 | 加载被 UI 拖慢 | 节流到每 ~10% |
| 8 | 一次性预加载全部波次 | 内存峰值过高，小游戏被 OOM 杀 | 分批 + 预测加载，配帧预算 |
| 9 | 预加载与释放互相打架 | 反复加载卸载（churn） | 释放延迟化、批量化；共享资源常驻 |
| 10 | 用假进度条 | "100% 后还要等" 体感更差 | 真实进度 + 阶段文案 + 最小展示时长 |
| 11 | 小游戏上用同步加载 | 操作永不完成（单线程阻塞请求） | 全异步（见 [关于同步加载](https://www.yooasset.com/docs/minigame/SyncLoading)） |
| 12 | 预加载 URL 与加载 URL 不一致 | 平台缓存不命中，重复下载 | 统一 URL 来源 |

---

## 8. 度量与验收

预加载必须**可度量**，否则无法判断"做好了没有"。

| 指标 | 采集方式 | 目标 |
|---|---|---|
| 进战斗总耗时 | 打点：点击开始 → 战斗可操作 | 按机型分档设定上限 |
| **首帧耗时** | 战斗场景第一帧 | 无 >50ms 尖峰 |
| **首波怪出现耗时** | 打点 | 稳定，无异常 |
| 首帧卡顿帧数 | Profiler `Shader.Parse` / `CreateGPUProgram` | **趋近 0** |
| 内存峰值 | Profiler / 平台工具 | 低于目标机型上限 |
| 加载期卡顿帧数 | Profiler 帧时间曲线 | 无长帧 |
| 重复进出同一关的耗时 | 打点 | 第二次应显著更快（验证缓存生效） |

**最后一项是很好的自检**：如果第二次进同一关没有变快，说明缓存/保留策略没生效。

---

## 9. 参考实现骨架

```csharp
// 分帧预算代理：思路照搬 TimeBudgetPerFrameDeferAgent
sealed class FrameBudget
{
    float _budgetFraction = 0.5f;   // 目标帧时间的比例
    float _spent;
    float FrameMs => 1000f / Application.targetFrameRate;

    public void BeginFrame() => _spent = 0f;

    /// 预估即将进行的工作耗时；返回 true 表示应推迟到下一帧
    public bool ShouldDefer(float estimatedMs)
        => _spent + estimatedMs > FrameMs * _budgetFraction;

    public void Account(float actualMs) => _spent += actualMs;
}

// 进战斗的四阶段流程
IEnumerator EnterBattle(LevelContext ctx)
{
    var budget = new FrameBudget();

    // ── 阶段 1：L1 下载（若有远端内容）
    yield return EnsureDownloaded(ctx.PreloadLabel, onProgress: p => ui.SetPhase(0, p));  // 字节口径

    // ── 阶段 2：L2 包驻留（含依赖）
    var bundleHandles = new List<BundleHandle>();
    foreach (var bundle in ctx.ResolveBundlesWithDependencies())
    {
        var h = package.LoadBundleFileAsync(bundle.Key);
        bundleHandles.Add(h);
        ui.SetPhase(1, (float)bundleHandles.Count / ctx.TotalBundles);
        yield return h;
    }

    // ── 阶段 3：L3 资源对象（分帧 + 预算）
    var assetHandles = new List<AssetHandle>();
    foreach (var entry in ctx.ResolveAssets())      // 模板 / 图集 / 音频 / 配置
    {
        var h = package.LoadAssetAsync(entry.Key, priority: entry.Priority);
        assetHandles.Add(h);

        yield return h;                              // 异步完成

        if (budget.ShouldDefer(estimatedMs: 2f))
            yield return null;                       // 让出一帧
        ui.SetPhase(2, (float)assetHandles.Count / ctx.TotalAssets);
    }

    // ── 阶段 4：L4 GPU / 运行时准备
    yield return WarmupShaders(ctx.Materials);       // ★ 必须离屏渲染材质，而不只是 WarmUp
    ui.SetPhase(3, 0.3f);
    yield return PoolWarmup(ctx.PoolPlan);           // 按峰值预热
    ui.SetPhase(3, 1f);

    // ── 交接：句柄交由战斗生命周期持有（pin）
    BattleSession.Current = new BattleSession(bundleHandles, assetHandles);
}
```

**交出句柄的这一刻就是"pin"**：整局战斗中这些句柄不释放，从而保证 L2/L3 全部驻留；战斗结束由 `BattleSession.Dispose()` 统一释放，并决定哪些转为常驻（§6）。

---

## 10. 参考资料

**Unity 官方**
- [着色器加载 / Shader loading](https://docs.unity3d.com/cn/2022.1/Manual/shader-loading.html) —— 预热的三种 API、DX12/Metal/Vulkan 必须离屏渲染、Profiler 标记
- [Addressables: Managing Addressable asset memory](https://docs.unity3d.com/Packages/com.unity.addressables@2.7/manual/memory-assets.html) —— "Avoid asset churn"、不能只卸载包的一部分
- [Addressables: Preloading dependencies](https://docs.unity3d.com/Packages/com.unity.addressables@1.20/manual/DownloadDependenciesAsync.html) —— `DownloadDependenciesAsync` / `GetDownloadSizeAsync`、label 预加载集合、进度口径与节流
- [Addressables: Synchronous loading](https://docs.unity3d.com/Packages/com.unity.addressables@2.7/manual/SynchronousAddressables.html) —— `WaitForCompletion` 的风险与 WebGL 限制
- [glTFast: TimeBudgetPerFrameDeferAgent](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.12/api/GLTFast.TimeBudgetPerFrameDeferAgent.html) —— 分帧预算的官方范式
- [Loading Times: How to avoid staring at a loading screen](https://learn.unity.com/tutorial/loading-times-how-to-avoid-staring-at-a-loading-screen) —— 加载阶段剖析、并行化、按需加载
- [Unity Cache.expirationDelay](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Cache-expirationDelay.html) —— 缓存默认 150 天才过期（懒惰淘汰的先例）

**YooAsset**
- [关于同步加载](https://www.yooasset.com/docs/minigame/SyncLoading) —— 小游戏不能同步；"异步预热包体 + 业务层同步取资源"折中
- [关于预下载](https://www.yooasset.com/docs/minigame/MiniGamePreload) —— 下载器并发/重试、平台缓存 URL 一致性、小游戏平台限制
- [资源卸载](https://www.yooasset.com/docs/2.3.x/guide-runtime/ResourceUnload) —— 不逐资源即时卸载；sweep 时机（切场景 / 定时器）
- [启动页面热更](https://www.yooasset.com/docs/solution/BootSceneUpdate) —— BootPackage 隔离与用完即销毁
- [Loading 源码说明](https://github.com/Besty0728/Unity-Skills/blob/3382be60/SkillsForUnity/unity-skills~/skills/yooasset-design/LOADING.md) —— 五类加载 API、`priority` 语义、`Load*Sync` 内部即 `WaitForAsyncComplete`

**开源实现**
- [gilzoide/unity-prefab-pool](https://github.com/gilzoide/unity-prefab-pool) —— Inspector 可配置初始池大小（预热数量声明式化）
