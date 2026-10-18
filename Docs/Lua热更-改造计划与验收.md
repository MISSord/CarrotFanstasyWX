# CarrotFantasy Lua 热更改造 —— 完整计划与验收标准

| 项目 | 内容 |
|---|---|
| 状态 | **计划稿**（Sprint 1 尚未开始） |
| 编写日期 | 2026-10-18 |
| 代码基线 | `8f5926e`（已移除 HybridCLR，程序集 9 → 6） |
| 目标形态 | **资源热更（AssetBundle）+ 代码热更（Lua）双链路** |
| 关联文档 | `构建与热更-操作手册.md`、`AB框架-问题清单与改进建议.md`、`资源加载-框架改版评审.md` |
| 估算总量 | 约 10~12 人日（不含 §4 中标注"可选"的项） |

> 本文只回答三个问题：**还缺什么、怎么做、怎么验**。已完成的部分见 §2，不再重复设计。

---

## 1. 目标与非目标

### 1.1 目标

1. **代码逻辑可以在不发新包的前提下修复与迭代**——以 Lua 承载业务规则、UI 流程、GM 命令等易变逻辑。
2. **Lua 脚本走现有 AB 管线**：与资源共用清单、签名、差分、Pack 分组，而不是另起一套下载器。
3. **能出包**：IL2CPP + 代码裁剪下 Lua 调用可用（这是当前最大的硬缺口）。
4. **写错不崩**：Lua 语法错误、运行时错误、下载失败都不能阻断玩家进入游戏。
5. **可讲清楚**：每一项能力都有明确的验收方式，能作为面试/作品集的技术叙事。

### 1.2 非目标（明确不做）

| 不做 | 原因 |
|---|---|
| 恢复 C# 代码热更（HybridCLR / ILRuntime） | 团队技术栈为 Lua；HybridCLR 与 Unity 2023.1 不兼容，已移除 |
| 自研 Lua 虚拟机 / 绑定层 | xLua 是事实标准，自研无收益 |
| Lua 侧完整 MVVM 框架 | 超出"热更链路"范围，业务框架按需增量演进 |
| iOS 出包 | 当前只验证 PC 与 Android，iOS 待有真机环境再补 |
| 灰度放量 / 服务端开关 | 需要服务端配合，本计划只做客户端侧的回滚能力 |

---

## 2. 现状基线

### 2.1 已完成能力

| 能力 | 实现位置 | 状态 |
|---|---|---|
| xLua 2.1.16 接入（独立程序集） | `Assets/XLua/`，`XLua.Runtime` / `XLua.Editor` asmdef | ✅ |
| LuaEnv 生命周期、Tick、Shutdown | `Assets/Scripts/Framework/Lua/LuaManager.cs` | ✅ |
| Lua 脚本从 AB 包加载 | 预载 + `AddLoader`，包名 `lua_scripts` | ✅ |
| 脚本索引 | `Assets/Game/LuaScripts/lua_index.json` | ✅ 手工维护（待自动化，见 L3-1） |
| AB 归属规则 | `PrefabABPathPostprocessor.IsLuaScriptAsset` → `lua_scripts` | ✅ |
| GM 执行入口 | `/gm lua <代码>`、`/gm lua reload`、`/gm lua` | ✅ |
| 不重启生效 | `reload` 重读脚本 + 清 `package.loaded` | ✅ 已验证 v1 → v2 |
| 资源热更管线 | 清单 / RSA 签名 / MD5 / 原子切换 / Pack 合并 | ✅ 原有能力 |
| 代码热更（HybridCLR） | — | ✅ 已移除（见 `8f5926e`） |

### 2.2 启动链路

```text
GameMain (Assets/Scripts/Framework/GameMain.cs)
  │
  ├─ ① AotResourceUpdateRunner.Run()         资源更新（清单 → 下载 → 校验 → 切换）
  │
  ├─ ② LuaManager.InitializeRoutine(this)    读 lua_index.json → 预载脚本 → new LuaEnv → AddLoader
  │
  └─ ③ new GameEntry()                       业务初始化（ServerProvision / BusinessProvision / 主场景）
        │
        └─ Update: LuaManager.Tick()         回收 LuaBase 对象
```

### 2.3 已知限制

| 限制 | 影响 | 对应任务 |
|---|---|---|
| 没有 xLua 代码生成（`XLua/Gen/` 为空） | IL2CPP 包内 Lua 调用 C# 走反射，可能失败 | L1-1 |
| `lua_index.json` 手工维护 | 新增脚本易漏，漏了就是"模块加载失败" | L3-1 |
| `reload` 只清 `package.loaded` | 旧的 C# 回调仍持有旧闭包 → 内存泄漏 + 双份逻辑 | L1-5 |
| 全量预载所有脚本 | 脚本增多后启动变慢 | L2-1 |
| 只带 Windows 原生库 | 无法出 Android 包 | L4-3 |

---

## 3. 缺口分析

```text
L1 脚本运行时   缺：代码生成(Gen)、错误处理、日志、内存、模块卸载
L2 热更管线     缺：脚本懒加载、下载失败兜底、版本兼容
L3 工程化       缺：索引自动生成、一键打 Lua 包、语法预检、CI
L4 出包与平台   缺：IL2CPP 验证、link.xml、Android 原生库、裁剪扫描
L5 业务落地     缺：Lua 侧最小框架、迁移一个真模块
```

### 3.1 为什么 L4 是硬门槛

当前 `ProjectSettings` 的 `scriptingBackend` 为 **IL2CPP**（`Android` / `Standalone` / `iPhone` 均为 1）。Lua 调用 C# 的默认路径是 xLua 的反射绑定（`MethodWarpsCache`），在 IL2CPP 下有两个**互相独立**的风险：

1. **反射能力受限（与裁剪无关）**：IL2CPP 没有 `Reflection.Emit`，部分泛型实例化与委托创建路径不可用 → 复杂签名或高频调用会直接失败。
2. **代码裁剪**：`Managed Stripping Level` 的 IL2CPP 默认值是 **`Minimal`**，而 `Minimal` 只搜索 UnityEngine 与 .NET 类库，**不裁剪用户程序集**——所以这个问题现在被默认值掩盖着。一旦为了压包体调到 `Medium` / `High`，只被 Lua 动态访问、没有静态引用的类型就会被删掉，表现为 `attempt to index a nil value (field 'XXX')`，看起来像 Lua 写错了，实际是 C# 侧被裁。

因此：

- Gen 代码是**必要条件**——解决风险 1，同时让风险 2 的类型变成静态引用；
- S1 就要求把裁剪级别设为 **`High`**，**主动暴露**风险 2，而不是等发布前才发现。

---

## 4. 功能清单与验收标准

> **必要性**：★★★ 必须 / ★★ 需要 / ★ 可选
> **估时**：单人为参考值，含自测

### 4.1 L1 脚本运行时

| 编号 | 功能 | 必要性 | 关键实现点 | 验收方式 | 估时 |
|---|---|---|---|---|---|
| L1-1 | xLua 代码生成 | ★★★ | 新建 `XLuaConfig.cs`：`[LuaCallCSharp]` 列**类型**、`[CSharpCallLua]` 列 delegate；执行菜单 `XLua/Generate Code`；生成物 `Assets/XLua/Gen/` 提交 git | ① `Gen/` 出现 `XXXWrap.cs` 与 `XLua_Gen_Initer_Register__.cs`；② IL2CPP 包内 Lua 能调用 Gen 过的类型 | 0.5d |
| L1-2 | Lua 错误处理 | ★★★ | 捕获 `LuaException`，提取 Lua traceback（含文件名 + 行号）；模块入口统一 `xpcall` 包装；错误写入 `GameLogController` | 故意执行 `nil + 1` → 弹出/打印含**脚本名与行号**的错误，游戏不崩 | 0.5d |
| L1-3 | Lua 日志重定向 | ★★ | 覆写 Lua 全局 `print` → `Debug.Log("[Lua] ...")`，附带 `debug.traceback` 精简信息 | Lua 内 `print("hi")` → Console 出现 `[Lua] hi`，可定位来源文件 | 0.2d |
| L1-4 | Lua 内存管理 | ★★ | 保留每帧 `LuaEnv.Tick()`；增加低频 `FullGc()`（如场景切换、手动 GM）；提供 GM 指令读取 `collectgarbage("count")` | `/gm lua return collectgarbage("count")` 有输出；reload 10 次后内存不持续增长 | 0.5d |
| L1-5 | 模块卸载协议 | ★★★ | 模块约定导出 `OnUnload()`；`reload` 前遍历已加载模块调用；C# 侧事件/回调需可注销 | reload 后旧事件不再响应（不出现重复扣血 / 双份日志） | 1d |
| L1-6 | Lua 沙箱 | ★ | `setfenv` 限制模块环境；屏蔽 `os.execute` / `io` / `loadfile` | 沙箱内调用 `os.execute` 报错，正常业务不受影响 | 0.3d |

> **L1-5 是面试高频题**（对应岗位调研文档第 5 节 A9："热更一个 Lua 模块后，旧对象/事件没清干净会怎样？"）。这一项做出来才有实战细节可讲。

### 4.2 L2 热更管线

| 编号 | 功能 | 必要性 | 关键实现点 | 验收方式 | 估时 |
|---|---|---|---|---|---|
| L2-1 | 脚本懒加载 | ★★ | 索引区分 `required`（启动必需）与 `lazy`；`require` 时按需从 AB 加载，不再全量预载 | 模块数 1 → 50 时，启动耗时增量 < 100ms | 1d |
| L2-2 | 下载失败兜底 | ★★★ | `lua_scripts` 标记为非关键包：下载失败不阻断启动，仅 Lua 功能降级 | 断网启动 → 能进游戏，Console 有明确警告，GM 面板提示 Lua 不可用 | 0.5d |
| L2-3 | 脚本版本兼容 | ★★ | `lua_index.json` 增加 `apiVersion`；C# 侧版本低于脚本要求 → 拒绝加载并提示更新客户端 | 人工改高 `apiVersion` → 拒绝加载 + 可读提示 | 0.5d |
| L2-4 | 独立热更演练 | ★★★ | 改 Lua → 只重打 `lua_scripts` → 上传 → 客户端只下载该包 | 下载体积 < 200KB，其余包零下载（对比清单 diff） | 0.5d |

### 4.3 L3 工程化

| 编号 | 功能 | 必要性 | 关键实现点 | 验收方式 | 估时 |
|---|---|---|---|---|---|
| L3-1 | 索引自动生成 | ★★★ | 编辑器扫描 `Assets/Game/LuaScripts/**.lua.txt`，按路径推导模块名，重写 `lua_index.json`；导入/删除时自动刷新 | 新增一个 `.lua.txt` → 索引自动包含；删除 → 自动移除 | 0.5d |
| L3-2 | 一键打 Lua 包 | ★★★ | 菜单 `Tools/Lua/打包 Lua 脚本`：只重打 `lua_scripts` + 刷新清单 + 更新 Pack | 单次 < 5s；清单 `ManifestVersion` +1；仅该包 hash 变化 | 0.5d |
| L3-3 | Lua 语法预检 | ★★ | 打包前用 `LuaEnv.LoadString` 逐个编译（不执行），语法错直接中止 | 故意写坏语法 → 打包被拦，指出文件名与行号 | 0.5d |
| L3-4 | CI 集成 | ★ | `unity-batch.ps1` 增加 `-cfCommand=luaPack` | 命令行可独立打出 Lua 包 | 0.5d |

> **L3-3 的价值**：Lua 语法错误在**打包时**拦住，而不是等玩家下载后崩溃。

### 4.4 L4 出包与平台

| 编号 | 功能 | 必要性 | 关键实现点 | 验收方式 | 估时 |
|---|---|---|---|---|---|
| L4-1 | IL2CPP 出包验证 | ★★★ | Gen 代码 + `Managed Stripping Level = High` + 出 PC 包 | 包内 `/gm lua require('gm.demo').ping()` 正常返回 | 1d |
| L4-2 | link.xml | ★★★ | 确认 `Generate Code` 是否产出 link.xml（xLua 自带 `Src/Editor/LinkXmlGen/` 模板）；若未产出则手写 preserve | 裁剪级别 High 下，所有 Gen 类型在包内可被 Lua 调用 | 0.5d |
| L4-3 | Android 原生库 | ★★ | 复制 `Assets/Plugins/Android/libs/<abi>/libxlua.so`（约 2MB） | Android 包能启动并初始化 Lua | 0.5d |
| L4-4 | Android 出包 | ★★ | 补一份 Android 产物（岗位要求以移动端为主） | APK 可跑，Lua 功能正常 | 1d |
| L4-5 | 裁剪风险扫描 | ★★ | 扫描 Lua 脚本中的 `CS.X.Y` 引用，与 `[LuaCallCSharp]` 列表比对，输出缺口报告 | 报告缺口为 0，或缺口项已明确标注为"故意不 Gen" | 1d |

> **L4-5 是防线**：Lua 里写了 `CS.Foo.Bar` 但 `Foo` 没进 Gen 列表 → IL2CPP 下必崩且极难排查。用工具一次性排掉。

### 4.5 L5 业务落地

| 编号 | 功能 | 必要性 | 关键实现点 | 验收方式 | 估时 |
|---|---|---|---|---|---|
| L5-1 | Lua 侧最小框架 | ★★★ | `class` / `module` / `event` / `timer` / 协程封装，约 300~500 行 | 用该框架写一个模块并跑通 | 2d |
| L5-2 | 迁移一个真模块 | ★★★ | 推荐 **GM 命令层**（已有 `GmCommandDispatcher`，天然需要热更、不碰核心战斗） | 改 Lua 中的 GM 命令 → `reload` → 行为变化 | 1.5d |
| L5-3 | Lua / C# 边界约定 | ★★ | 文档写明：Lua 负责业务规则、UI 流程、配置驱动；C# 负责框架、资源、性能敏感逻辑 | 文档评审通过，后续新增功能按此分工 | 0.5d |

---

## 5. 路线图

| Sprint | 目标 | 包含项 | 产出 | DoD（完成定义） | 估时 |
|---|---|---|---|---|---|
| **S1** | 让它**能出包** | L1-1 → L4-1 → L4-2 | PC IL2CPP 包可运行 Lua | 包内 `/gm lua` 全部指令正常；裁剪 High 下无缺失类型 | 1.5d |
| **S2** | 运行时健壮性 | L1-2 → L1-3 → L1-5 → L1-4 | Lua 写错不崩、reload 无泄漏 | §6.2 反例全部通过 | 3d |
| **S3** | 工程化 | L3-1 → L3-2 → L3-3 → L2-4 | 3 秒完成"改 Lua → 出包" | §6.1 正例 ①~⑥ 通过 | 2d |
| **S4** | 业务落地与验收 | L5-1 → L5-2 → L2-2 | 完整作品集案例 | §6.1 全流程 10 步通过 | 3d |
| **S5**（可选） | 平台与深度 | L4-3 → L4-4 → L4-5 → L2-1 → L2-3 | Android 可用 + 防线工具 | APK 跑通；扫描报告缺口为 0 | 2d |

### 5.1 依赖关系

```text
S1 (Gen + IL2CPP)  ──► S2 (健壮性)  ──► S4 (业务落地 + 端到端验收)
                          │                    ▲
                          └──► S3 (工程化) ────┘

S5 独立于 S1~S4，可随时插入（Android 库与 Gen 无耦合）
```

**关键路径**：`S1 → S2 → S4`。S3 可与 S2 并行。

---

## 6. 端到端验收流程

### 6.1 正例：线上热修演练（10 步）

> 场景：线上 GM 命令有 bug，需把 `gm_demo` 的版本号从 1 改成 2 并生效。

| 步骤 | 操作 | 预期结果 |
|---|---|---|
| ① | 编辑 `Assets/Game/LuaScripts/gm_demo.lua.txt`：`local VERSION = 1` → `2` | — |
| ② | 菜单 `Tools/Lua/打包 Lua 脚本` | Console：`lua_scripts 打包完成，1 个脚本，XX KB，清单 version → N+1` |
| ③ | 上传（AB 打包窗口 → 上传） | 只上传 `lua_scripts` + 清单 + 相关 Pack，其余 AB 零上传 |
| ④ | 用**已安装的旧版本客户端**启动 | 检测到 1 个包需更新 |
| ⑤ | 观察下载 | 下载体积 < 200KB，且只下载该包 |
| ⑥ | 进游戏后执行 `/gm lua reload` | Console：`[LuaManager] Lua 脚本已重新加载 1/1 个` |
| ⑦ | 执行 `/gm lua require('gm.demo').ping()` | 返回 `pong, gm.demo v2` |
| ⑧ | 确认未重装客户端 | `persistentDataPath` 中 `DownloadedAssetBundles` 的清单 version 已更新 |
| ⑨ | 执行回滚（服务端清单回退上一版） | 客户端重启后回到 v1（或提示可用 N-1） |
| ⑩ | 记录包体积对比 | `lua_scripts` 包大小 vs 全量 AB 大小，作为"小包热更"的量化证据 |

### 6.2 反例：健壮性验收（8 条）

| 编号 | 场景 | 预期行为 |
|---|---|---|
| R1 | 断网启动 | 游戏能进入；Lua 功能降级并给出明确提示；不黑屏、不卡死 |
| R2 | Lua 语法错误 | 打包阶段被拦截，报出文件名与行号 |
| R3 | Lua 运行时错误（`nil + 1`） | 错误被捕获并上报，含 traceback；游戏不崩 |
| R4 | Lua 调用未 Gen 的类型 | L4-5 扫描报告指出缺口，而不是运行时神秘崩溃 |
| R5 | `reload` 10 次 | `collectgarbage("count")` 不持续增长；无重复事件 |
| R6 | `lua_index.json` 指向不存在的资产 | 跳过该模块并警告，其余模块正常 |
| R7 | AB 中 `lua_scripts` 包缺失 | 启动流程不中断，Lua 标记为不可用 |
| R8 | `apiVersion` 不匹配（L2-3 后） | 拒绝加载脚本并提示更新客户端 |
| R9 | 裁剪级别 High 出包 | 所有 Gen 类型与 `[LuaCallCSharp]` 类型在包内可用 |

---

## 7. 关键技术决策

### 7.1 为什么是 xLua 而不是继续 HybridCLR

| 维度 | HybridCLR | xLua |
|---|---|---|
| 与 Unity 版本关系 | 强耦合（2023 分支要求 ≥ 2023.2） | 基本无关 |
| 接入成本 | 需维护 AOT 清单、Generate/All、DLL 同步 | 拖入 `Assets/` 即可 |
| 失败模式 | 真机才暴露（AOT 泛型缺失） | 启动即报（脚本未找到、wrapper 未生成） |
| 团队技术栈 | 团队以 Lua 为主 | 一致 |

### 7.2 Gen 代码与代码裁剪的关系（重要）

`[LuaCallCSharp]` **本身不防裁剪**，标签只是给生成器的输入。防裁剪靠的是**生成代码中的静态引用**。

xLua 的巧妙做法（见 `Assets/XLua/Src/Editor/Template/LuaRegister.tpl.txt`）：

```csharp
namespace XLua
{
    public partial class ObjectTranslator              // 用 partial 扩展 xLua 自己的类
    {
        static XLua.CSObjectWrap.XLua_Gen_Initer_Register__ s_gen_reg_dumb_obj
            = new XLua.CSObjectWrap.XLua_Gen_Initer_Register__();
    }
}

public class XLua_Gen_Initer_Register__
{
    static XLua_Gen_Initer_Register__() { XLua.LuaEnv.AddIniter(Init); }
}
```

静态引用链：

```text
LuaEnv → ObjectTranslator → [静态字段] XLua_Gen_Initer_Register__
  → 静态构造 → AddIniter(Init) → wrapInitN() → XXXWrap.__Register()
    → typeof(目标类型) + 成员直接引用        ← 链接器在此看见，不会被裁
```

**由此推出三条铁律**：

1. 只加标签、不执行 `XLua/Generate Code` = 什么都没发生；
2. 生成后不重新编译/打包 = 同上；
3. 把类型从列表里删掉却不重新生成 = 旧 Gen 文件仍在，形成"意外防裁剪"的假象。

### 7.3 模块卸载协议（为什么不能只清 `package.loaded`）

当前 `reload` 只做两件事：重读脚本文本 + `package.loaded[module] = nil`。

**问题**：旧模块在 C# 侧注册的回调（事件、委托、协程、UI 监听）仍持有旧闭包 →
- 内存泄漏；
- 双份逻辑同时响应（重复扣血、日志翻倍）。

**方案**：

```lua
-- 每个模块可选导出
function M.OnUnload()
    -- 解绑事件、停定时器、清缓存
end
```

`reload` 流程改为：

```text
① 遍历已加载模块，调用 OnUnload()
② 清 package.loaded
③ 重新从 AB 读取脚本文本
④ 等待下次 require 重新加载
```

`OnUnload` **不强制**（未导出则跳过），但模块注册了 C# 侧监听时必须实现。Lua 侧框架（L5-1）应提供 `Event:On(target, name, fn)` 之类的封装，统一记录以便自动解绑。

### 7.4 懒加载 vs 全量预载

| 策略 | 优点 | 缺点 | 适用 |
|---|---|---|---|
| 全量预载（当前） | `require` 同步、实现简单 | 启动耗时随脚本数线性增长 | 脚本 < 20 个 |
| 索引 + 懒加载 | 启动快 | `require` 可能阻塞，需要"预读热点模块" | 脚本 > 20 个 |

**决策**：先把脚本量做起来再优化；L2-1 安排在 S5，并采用"索引标注 `required` / `lazy`"的折中方案。

### 7.5 Lua 与 C# 的职责边界

| 归 Lua（可热更） | 归 C#（AOT） |
|---|---|
| 业务规则、数值公式 | 资源加载 / AB 管理 |
| UI 流程与页面跳转编排 | 战斗核心循环（性能敏感） |
| 配置表的二次加工 | 网络协议编解码 |
| GM 命令 | 框架与工具 |

原则：**改得频繁的进 Lua，性能敏感与底层设施留在 C#。**

---

## 8. 风险与对策

| 风险 | 影响 | 对策 |
|---|---|---|
| IL2CPP 下 Gen 覆盖不全 | 线上神秘崩溃 | L4-5 扫描 + R9 反例验收 |
| 裁剪级别调高后才暴露问题 | 发布前返工 | 从开发初期就设 `High` |
| `reload` 泄漏 | 内存持续增长 | L1-5 模块卸载协议 + R5 验收 |
| Lua 脚本无人管质量 | 线上频繁出错 | L3-3 语法预检 + 代码规范 |
| 全量预载拖慢启动 | 启动体验差 | L2-1 懒加载 |

---

## 9. 与岗位要求的映射

| 岗位要求（摘自调研文档） | 本计划对应项 | 完成后可讲的内容 |
|---|---|---|
| 熟悉 Lua 及至少一种热更框架 | L1-1 ~ L1-5、L5-1 | AB → Lua 加载链路、Gen 代码、错误兜底、模块卸载 |
| 熟悉 Lua 与 C# 的交互机制、调用开销、生命周期 | L1-1、L1-4、L1-5 | `LuaFunction` 装箱开销、`table.Get<Delegate>` 优化、GC 观测 |
| 深入理解 AssetBundle 与资源更新流程 | 已有能力 + L2-4 | Lua 包纳入同一套清单/签名/差分/Pack |
| 有热更新与多版本发布实战经验 | L2-4、§6.1 演练 | 只更新单个小包的完整链路与体积数据 |
| 负责打包流程建设 | L3-2、L3-4 | 一键打 Lua 包 + CI 集成 |

---

## 10. 验收清单（可勾选）

**S1 出包**
- [ ] `Assets/XLua/Gen/` 已生成并提交 git
- [ ] IL2CPP PC 包内 `/gm lua` 全部指令正常
- [ ] `Managed Stripping Level = High` 下无类型缺失
- [ ] link.xml 已确认（自动生成或手写）

**S2 健壮性**
- [ ] 反例 R1 / R2 / R3 / R5 通过
- [ ] 模块卸载协议已实现，至少一个模块导出 `OnUnload`

**S3 工程化**
- [ ] 新增 `.lua.txt` 后索引自动更新
- [ ] 一键打包可在 5s 内完成
- [ ] 语法错误在打包阶段被拦截
- [ ] 正例 ①~⑥ 通过，下载体积有量化数据

**S4 业务落地**
- [ ] Lua 侧框架可用（class/module/event/timer）
- [ ] GM 命令层已迁移到 Lua 并验证热更
- [ ] 正例 ①~⑩ 全部通过

**S5 平台（可选）**
- [ ] Android 包可运行 Lua
- [ ] 裁剪风险扫描报告缺口为 0

---

## 11. 附录：涉及的文件清单

### 11.1 预计新增

| 文件 | 用途 | 对应任务 |
|---|---|---|
| `Assets/Scripts/Framework/Lua/XLuaConfig.cs` | `[LuaCallCSharp]` / `[CSharpCallLua]` 配置 | L1-1 |
| `Assets/Scripts/Framework/Lua/LuaErrorHandler.cs` | 错误捕获与上报 | L1-2 |
| `Assets/Scripts/Framework/Lua/LuaModuleRegistry.cs` | 模块状态与 `OnUnload` 调度 | L1-5 |
| `Assets/Editor/Lua/LuaScriptIndexer.cs` | 索引自动生成 | L3-1 |
| `Assets/Editor/Lua/LuaPackagerWindow.cs` | 一键打 Lua 包 | L3-2 |
| `Assets/Editor/Lua/LuaSyntaxChecker.cs` | 语法预检 | L3-3 |
| `Assets/Editor/Lua/LuaCSRefScanner.cs` | `CS.X.Y` 引用 vs Gen 列表比对 | L4-5 |
| `Assets/Game/LuaScripts/framework/*.lua.txt` | Lua 侧框架 | L5-1 |
| `Assets/Game/LuaScripts/gm/commands.lua.txt` | GM 命令迁移 | L5-2 |

### 11.2 预计修改

| 文件 | 改动 | 对应任务 |
|---|---|---|
| `Assets/Scripts/Framework/Lua/LuaManager.cs` | 错误处理、模块注册表、`FullGc`、`apiVersion` | L1-2 / L1-4 / L1-5 / L2-3 |
| `Assets/Editor/BuildEditor/PrefabABPathPostprocessor.cs` | 若需细分 Lua 包（按目录分包） | L2-1 |
| `Assets/Game/LuaScripts/lua_index.json` | 由手工改为自动生成，增加 `apiVersion` | L3-1 / L2-3 |
| `Assets/Editor/BuildEditor/AssetBundlePackMerger.cs` | `lua_scripts` 包标记为非关键 | L2-2 |
| `Assets/Scripts/Business/Gm/GmCommandDispatcher.cs` | GM 命令转发到 Lua | L5-2 |
| `ProjectSettings/ProjectSettings.asset` | `Managed Stripping Level = High` | L4-1 |
| `Assets/link.xml` | 追加 xLua 相关 preserve（若需） | L4-2 |

---

## 12. 参考资料

**xLua**
- 官方仓库：<https://github.com/Tencent/xLua>
- 本地文档：`Assets/XLua/Doc/`（若已随源码检出；当前工程为精简安装，未包含 Doc 目录）
- 关键源码：`Assets/XLua/Src/Editor/Template/LuaRegister.tpl.txt`（静态引用链）、`Assets/XLua/Src/GenAttributes.cs`（标签语义）

**Unity 代码裁剪**
- Managed code stripping（2023.1）：<https://docs.unity3d.com/2023.1/Documentation/Manual/ManagedCodeStripping.html>
- 中文 · Unity 链接器：<https://docs.unity3d.com/cn/current/Manual/unity-linker.html>
- 中文 · 使用注解保留代码：<https://docs.unity3d.com/cn/current/Manual/managed-code-stripping-preserving.html>
- Unity 链接器源码：<https://github.com/Unity-Technologies/linker>
- 链接器反射测试用例（哪些反射模式可被识别）：<https://github.com/Unity-Technologies/linker/tree/unity-master/test/Mono.Linker.Tests.Cases/Reflection>

**程序集**
- 中文 · 创建程序集资源：<https://docs.unity3d.com/cn/current/Manual/assembly-definitions-creating.html>

---

## 变更记录

| 日期 | 版本 | 说明 |
|---|---|---|
| 2026-10-18 | v1 | 初稿。基线 `8f5926e`（已移除 HybridCLR、程序集 9→6、xLua 最小闭环验证通过） |
