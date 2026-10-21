# Lua 热更与 XLua 生成 —— 踩坑记录

> 一次从 0 搭 Lua 面板、打通 XLua 代码生成、再到 AB 打包的全过程复盘。
> 每条按「现象 → 根因 → 解法 → 一句话」组织；第 7 节是排查方法论，第 8 节可直接用于面试复盘。
>
> 配套文档：`构建与热更-操作手册.md`（操作步骤）、`Lua热更-改造计划与验收.md`（计划与验收）。

---

## 0. 先看最终形态

```text
Lua 侧（Assets/Game/LuaScripts）
  framework/class.lua              类系统（New / __init 链 / __fini）
  framework/logger.lua             全局 log
  framework/event.lua              全局 Event
  framework/view_base.lua          面板基类（生命周期 + Node/AddClick + BindBridge/BindHost）
  framework/view_manager.lua       视图注册与栈
  framework/module_controller.lua  全局类装载 + Ctrl 单例
  framework/config_manager.lua     Luban 表按需读
  framework/lua_view_host.lua      C# → Lua 的调用入口（EnsureView / Invoke）
  panels/help_panel.lua            帮助面板（已迁移）
  panels/set_panel.lua             设置面板（已迁移）
  gen/luban_data/*.lua             Luban 导出的有序数据表

C# 侧
  Framework/Lua/LuaManager.cs       LuaEnv 管理 + Execute/CallLuaView/EnsureLuaView
  Framework/Lua/LuaView.cs          逻辑在 Lua 的视图基类（cfg / host / 节点表 / 生命周期）
  Framework/Lua/XluaGenConfig.cs    生成配置（类型清单 + 成员过滤器）
  Business/Hud/LuaHelpPanelView.cs  15 行适配器
  Business/Hud/LuaSetPanelView.cs   设置 API + host 委托表
  Assets/XLua/Extensions/LuaViewBridge.cs   Lua 唯一直接调用的 C# 对象（属 XLua.Runtime）

程序集边界（关键）
  Assets/XLua/XLua.Runtime.asmdef  覆盖 Src/** + Gen/** + Extensions/**
  Assets/XLua/Src/Editor/XLua.Editor.asmdef  嵌套，引用 XLua.Runtime
  CarrotFantasy.asmdef             游戏代码，引用 XLua.Runtime
```

打包结果（一次完整构建）：**296 个 AB → 44 个下载 Pack**，启动包清单 2 个
（`ui/view/login_prefab`、`ui/view/loadingview_prefab`）。

---

## 1. xLua / 程序集边界（最硬的一组坑）

### 坑 1：生成代码必须和 xLua 运行时**同程序集**

```text
现象  575 条 CS0436，加一堆 CS1061(Push/Assignable/FastGetCSObj) 与 CS0029
根因  生成的 Gen 落在 Assembly-CSharp，而 Src 在 XLua.Runtime
      xLua 生成的是 partial class ObjectTranslator —— partial 不能跨程序集合并
      Assembly-CSharp 里凭空多出一个同名类型（CS0436 只是警告，两个类型身份不同）
解法  XLua.Runtime.asmdef 从 Src/ 上移到 Assets/XLua/，同时覆盖 Src + Gen
一句话 "partial class 是编译期合并，合并单位是程序集；跨程序集就是两个类型"
```

### 坑 2：被生成的类型不能**反向依赖**外层程序集

```text
现象  CS0246 'CarrotFantasy' 找不到（生成的 wrapper 里 typeof(CarrotFantasy.LuaViewBridge)）
根因  Gen 在 XLua.Runtime，而 LuaViewBridge 在 CarrotFantasy；
      CarrotFantasy → XLua.Runtime 已存在，反向引用成环，Unity 直接拒绝
解法  ① 把桥挪进 XLua.Runtime 覆盖范围：Assets/XLua/Extensions/LuaViewBridge.cs
      ② 用 ILuaViewHost 接口切断对 LuaView（游戏程序集）的依赖
一句话 "生成代码所在程序集是边界；边界内的类型不能反过来认识边界外的类型"
```

### 坑 3：没有生成绑定时，**只有编辑器走反射**

```csharp
// Assets/XLua/Src/ObjectTranslator.cs:153
#if !GEN_CODE_MINIMIZE && !ENABLE_IL2CPP && (UNITY_EDITOR || XLUA_GENERAL) && !FORCE_REFLECTION ...
    Utils.ReflectionWrap(L, type, ...);   // ← 只有编辑器/Mono 会走这里
```

```text
现象  编辑器里一切正常，IL2CPP 包抛 "not gen"
解法  跑 XLua/Generate Code，把用到的类型登记进 LuaCallCSharp
一句话 "xLua 的反射回退是编辑器福利，真机必须生成"
```

### 坑 4：编辑器专用成员被生成 → **打 Player 才炸**

```text
现象  error CS1061: 'Graphic' does not contain a definition for 'OnRebuildRequested'
根因  uGUI 里它被 #if UNITY_EDITOR 包着（Graphic.cs:793）；
      生成器在编辑器跑反射，看得见、照常产出包装；Player 编译时成员不存在
解法  XluaGenConfig 增加 EditorOnlyMemberNames 名单，成员过滤器优先排除
一句话 "反射看到的是编辑器世界，Player 是另一个世界"
```

### 坑 5：`Span<T>` / ref struct 不能做泛型实参

```text
现象  error CS0306: 'Span<Vector3>' may not be used as a type argument
根因  Unity 2022+ 给 Transform 加了 Span 重载（TransformDirections/Points/Vectors 等），
      生成器产出 translator.Assignable<System.Span<Vector3>>(...)
解法  Func<MemberInfo,bool> 过滤器：参数或返回值含 Span/ReadOnlySpan/指针 → 跳过
一句话 "生成器只认反射签名，不认 C# 语言限制"
```

### 坑 6：黑名单有两种形态，选错等于**静默失效**

```csharp
// Generator.isMethodInBlackList
if (mb.DeclaringType.ToString() == exclude[0] && mb.Name == exclude[1])
{
    var parameters = mb.GetParameters();
    if (parameters.Length != exclude.Count - 2) continue;   // ← 元素个数必须 = 2 + 参数数
    for (...) if (parameters[i].ParameterType.ToString() != exclude[i + 2]) ...
}
```

```text
List<List<string>> 形式：{类型, 方法名} 只匹配「无参方法」，
                        有参数的成员必须把参数类型逐个列出（易因类型字符串变化失效）
Func<MemberInfo,bool> 形式：方法/属性/字段统一过滤，可按可见性/签名判断 —— 推荐
一句话 "配置规则要确认匹配语义，否则是静默失效（不报错、也不生效）"
```

### 坑 7：生成器坏了会**锁死修复路径**

```text
链路  生成物报错 → XLua.Runtime 编译失败 → XLua.Editor（生成器）也编不出来
      → 菜单点不动 → 旧产物永远不会被重写 → 死循环
解法  先删 Assets/XLua/Gen（可再生成）让工程恢复编译，再跑生成
一句话 "当生成工具自身依赖被生成的程序集时，修复顺序必须是先破后立"
```

### 坑 8：C# 委托 → Lua 函数需要 `[CSharpCallLua]`

```text
现象  DelegatesGensBridge.cs 只有 29 行，GetDelegateByType 直接 return null
根因  Action/Action<int>/Func<bool> 没有登记，生成不出 DelegateBridge
      → self.host.ToggleMusic() 在编辑器走反射能跑，IL2CPP 必失败
解法  写进 CSharpCallLua 后重新生成（29 → 108 行）
一句话 "传 LuaFunction 不用生成；传委托必须生成"
```

---

## 2. Lua 框架设计约定

### 坑 9：子类不要覆盖 `New`

```lua
-- 反例：把配置塞进 New，构造语义被拆成两处
local _create = SetPanel.New
function SetPanel.New()
    return _create({ name = 'SetPanel', layer = 1, bundle = ..., asset = ... })
end

-- 正解：class() 的 __init 链会「父类先、子类后」调用，参数一路透传
function SetPanel:__init(cfg)
    self.pageId = 1
    self._clicks = {}
end
```

```text
根因  class.lua 的 _init_object 已经保证：父类 __init → 子类 __init，参数原样传
      覆盖 New 只会让「构造配置」和「字段初始化」分散在两处，且每个面板重复一份
解法  配置由 C# 的 LuaView 通过 New(cfg) 传入，BaseView.__init(cfg) 消费
一句话 "构造只有一个入口，初始化只有一个回调"
```

### 坑 10：自定义 loader 必须放行 xLua 自带模块

```text
现象  每次启动 Error：找不到模块文件 .../LuaScripts/xlua/util.lua（module=xlua.util）
根因  package.searchers 顺序：
        2. LoadBuiltinLib
        3. LoadFromCustomLoaders   ← 我们的 loader
        4. LoadFromResource        ← Assets/XLua/Resources（xlua/util.lua.txt）
      我们的 loader 把所有名字都拼成本地路径，找不到就打 Error
解法  xlua./perf./tdr. 白名单直接 return null，交给内置 Resources loader
一句话 "自定义 loader 只该接管自己的命名空间，其余静默放行"
```

### 坑 11：全局类不要用 `_G[name] = mod` 动态映射

```lua
-- 改前：FILE_LIST 里带 global 字段，运行时 _G[item.global] = mod
-- 改后：每个模块在自己文件里声明全局，module_controller 只按依赖顺序 require
FILE_LIST = { 'framework.class', 'framework.logger', ... }
for i = 1, #FILE_LIST do require(FILE_LIST[i]) end
```

```text
好处  依赖关系一眼可见、无动态映射、IDE/LuaLS 能直接解析全局符号
一句话 "约定优于映射"
```

### 坑 12：Lua 面板分层 —— Lua 编排，C# 提供能力

```text
Lua 能直接碰的：UnityEngine / UGUI（生成包装后就可用）
Lua 碰不到的：游戏程序集（会重现坑 2 的循环引用）

所以：
  交互流程（按钮绑定、页签切换、状态流转） → Lua
  业务 API（音频、分辨率持久化、多语言）   → C#，以 host 委托表注入

  LuaView.BuildLuaHost() → LuaTable { "ToggleMusic", (Action)..., ... }
  Lua 侧 self.host.ToggleMusic()
一句话 "Lua 管流程，C# 管能力，边界用 host 委托表"
```

---

## 3. AB / 资源管线

### 坑 13：包名「文件 vs 目录」冲突

```text
现象  Moving Temp/unitystream.unity3d to .../animatorcontroller/monster：拒绝访问
      Failed to replace file .../animatorcontroller/monster
根因  AB 名直接映射为输出文件路径：
        .../monster      （父包 → 文件）
        .../monster/1    （子包 → 目录）
      同一路径不可能既是文件又是目录
解法  统一加 _prefab 后缀：.../monster_prefab（文件）+ .../monster/1_prefab（文件）
一句话 "AB 名就是文件路径，父子同名必然冲突"
```

### 坑 14：目录被写入 assetBundleName → 隐式合并整个目录

```text
现象  Assets/Game/UI/RawImages.meta 里出现 assetBundleName: ui/rawimages_prefab
根因  规则 path.Contains("UI/RawImage") 没有限定扩展名，目录名也命中
      → 目录带包名 = 该目录下所有资源隐式并入该包（重复打包、包体膨胀）
解法  规则加图片扩展名判断；重分配菜单主动清理目录上的包名
一句话 "包名只能落在资源上，落在目录上等于批量隐式打包"
```

### 坑 15：`.lua` 源码被当资产分配包名

```text
现象  framework/class.lua.meta → assetBundleName: luascripts/framework_prefab
根因  UseGameFolderRule 只排除了 .meta/.cs/.unity 等，没排除 .lua
解法  排除 .lua；lua_bundle.bytes 由 IsLuaScriptAsset 单独匹配，仍归 lua_scripts
一句话 "源码不该进 AB，Lua 的唯一产物应该是 lua_bundle.bytes"
```

### 坑 16：迁移目录必须连 `.meta` 一起搬

```text
做法  git mv 图片 + 图片.meta，GUID 不变
效果  git 识别为 rename；预制体里序列化的 m_Sprite 引用不断
验证  抽样确认 HelpPanel.prefab 仍能命中迁移后的贴图
反例  只搬 png 不搬 meta → GUID 重生成 → 所有引用变 null
一句话 "Unity 的引用是 GUID 不是路径；移动只改路径，不动 GUID"
```

### 坑 17：空 asmdef 会一直报「no scripts」

```text
现象  DOTween.Modules.asmdef：目录 0 个 .cs（9 个 .cs~）→ 警告 no scripts
根因  DOTween 的模块开关机制，全关时 asmdef 没有可编译脚本
      删除后 DOTween 编辑器脚本会自动重建 → 删不掉
解法  纳入版本管理，接受这条无害警告（或启用任意一个模块）
一句话 "有些警告来自第三方工具的自愈行为，删不掉就入库接受"
```

---

## 4. 资源加载的「静默失败」（最值得复盘的一类）

### 坑 18：Resources 路径不存在 → 恒 null，且无报错

```text
三处实测：
  Resources.Load("Pictures/GameOption/.../Carrot_1")   → Assets/Resources 没有 Pictures
  Resources.Load("Animator/.../Monster/1/1")           → 没有 Animator
  Resources.Load("AudioClips/NormalMordel/Tower/...")  → 没有 AudioClips

后果  结算萝卜图标空白、怪物没有动画、战斗音效全静音 —— 而 Console 安静
解法  改走 AB：
      · 图集图 → Image.SetAtlasSprite（UIImageLoader）
      · 动画控制器 → AssetBundleManager.LoadAsset(typeof(RuntimeAnimatorController))
      · 音频 → 逻辑路径推导 AB 坐标（目录小写 + _prefab，资源名 = 文件名）
一句话 "Resources.Load 失败不抛异常，是热更项目里最危险的静默失败源"
```

### 坑 19：大小写不匹配 → 退化「任意同类型」

```text
现象  [EditorAssetLoader] 请求=Level_1_BG_Left 实际=level_1_bg_left
      → 退化为「任意同类型」
根因  代码拼 PascalCase，图片批量改名后是小写；
      ResPath.GetRawImagePath 只对 bundle 做 ToLower，assetName 原样传；
      TryLoadBestMatch 里 obj.name == assetName 是区分大小写的比较
风险  文件内有多个同类型资源时，兜底可能静默返回错的
解法  拼名统一小写；保持加载器严格比较（它正好是命名问题的探针）
一句话 "大小写兜底看似兼容，实则掩盖错误、甚至返回错资源"
```

### 坑 20：自动回填要校验「这个包名能不能被加载」

```text
现象  图片迁移后，46 个 UIImageLoader 字段指向 ui/rawimages/...，图片不显示
根因  AtlasLoader 只认含 images_atlas 的包名；加载失败时 ReleaseCurrent 已先清空 sprite
      更隐蔽的是：守卫判断被放在了「已记录就跳过」之后 → 错误路径永远不被纠正
解法  ① 回填时校验包名（不匹配就 ClearRecordedPath）
      ② 校验顺序必须在缓存命中判断之前
一句话 "自动回填的合法性校验，要放在缓存命中判断之前"
```

---

## 5. 配置与热更流程

### 坑 21：Luban 默认 map 模式导致 Lua 遍历无序

```text
默认输出          [1001] = { ... }        -- pairs 顺序不可依赖；tbitem 还从 [0] 开始，连 #t 都不准
__tables__.xlsx 的 mode 列填 list 后：
                  { ... }, { ... }        -- 顺序 = Excel 行序，ipairs 可用

意外收获  list 模式仍会生成 GetBy<索引字段>()，C# 只改了一行
          （GetOrDefault → GetByTowerId）
一句话 "配置表要的是有序表不是字典；map 只是 C# 侧的便利索引"
```

### 坑 22：热更分层

```text
资源层  AB（下载 → 校验 → 解包）        改图片/预制体/配置
代码层  Lua bundle（模块名扁平化）      改玩法逻辑，无需重出包
框架层  AOT C#                          改框架必须重新发版

判定口诀：改了 Lua 就不该动 C#；改了 C# 就必须重出包
一句话 "热更能力是分层的，边界画错就会出现『改了却不生效』"
```

---

## 6. 打包 / 编辑器环境

### 坑 23：Unity 运行时有 lockfile，不能 batchmode

```text
现象  Temp/UnityLockfile 存在、Unity 进程在跑
后果  -batchmode -executeMethod 会失败，甚至损坏 Library
做法  用编辑器菜单跑；要做 CI 就必须先关闭编辑器（或改用独立构建机）
一句话 "锁文件是硬约束，批处理与开着编辑器互斥"
```

### 坑 24：生成配置改完必须**重新生成**

```text
改了 LuaCallCSharp / CSharpCallLua / BlackList
  → 必须重跑 XLua/Generate Code，否则生成物还是旧的
  → 典型表现：编辑器能跑、真机不行；或改了配置但编译错误依旧
一句话 "生成配置是输入，Generate Code 是唯一生效方式"
```

---

## 7. 排查方法论（面试爱问「你怎么定位的」）

| 现象 | 定位手法 |
|---|---|
| 编辑器正常、真机失败 | 先查有没有 `#if UNITY_EDITOR` 条件编译路径（反射 / 编辑器专用 API） |
| 编译错误在 `Gen/` 下 | 优先怀疑「生成物过期」，而不是手写代码错 |
| 资源不显示但无报错 | 全链路搜 `Resources.Load` / `LoadAsset`，用脚本把「代码里的路径/名字」逐条对磁盘校验 |
| 怀疑大小写问题 | 把字面量小写后与磁盘 stem 比对，命中但不是精确匹配的就是嫌疑 |
| 打包报「拒绝访问」 | 先看输出目录里同名路径是文件还是目录（包名冲突） |
| 改了配置不生效 | 确认生成/重导流程跑过；确认规则真的命中了目标路径 |
| 警告刷屏 | 看是不是 `partial`/`CS0436` 这类「同一类型两份」问题 |

几条通用原则：

1. **静默失败优先**：Unity 大量 API 失败返回 null 不抛异常，先排除这类。
2. **先让工程能编译**：生成物/第三方坏了时，删产物比改代码更快恢复。
3. **静态核对 + 实机验证分层**：脚本能验证的（路径、名字、括号、引用）先验证；Unity 编译、真机 IL2CPP 必须实跑。

---

## 8. 面试问答准备（深挖点）

**Q：为什么 partial class 不能跨程序集？**
A：partial 是编译期文本合并，合并单位是程序集。跨程序集就是两个独立类型，同名也互不相干；
编译期只会给 CS0436 警告，运行期类型身份不同，于是调用会找不到成员。

**Q：xLua 的 module searcher 顺序？**
A：preload → LoadBuiltinLib → LoadFromCustomLoaders（我们加的）→ LoadFromResource（Resources）
→ StreamingAssets。自定义 loader 返回 null 才会继续往下找，所以「不接管」就 return null，
不要打错误日志。

**Q：为什么给生成的 AB 名加 `_prefab` 后缀就能解决冲突？**
A：AB 名直接映射为输出文件路径；父子同名时父是文件、子是目录，文件系统不允许同一路径
既是文件又是目录。加后缀让父名与子目录名不再相同。

**Q：编辑器专用 API 为什么能骗过生成器？**
A：生成器在 Unity 编辑器进程里跑反射，`UNITY_EDITOR` 已定义，成员可见；打 Player 时
符号未定义、成员被裁掉，编译期就找不到。

**Q：`Resources.Load` 为什么危险？**
A：失败返回 null 不抛异常；Resources 内容全打进包、无法热更；路径拼错时表现为「功能静默失效」，
排查成本极高。

**Q：Lua 面板怎么和 C# 交互？为什么不直接调 C# 类？**
A：Lua 调游戏程序集会形成程序集循环引用（生成代码在 XLua.Runtime，游戏程序集引用它）。
所以用 host 委托表：C# 把能力以委托形式注入 Lua，Lua 只调这些委托；
委托类型登记 CSharpCallLua 后生成 DelegateBridge，IL2CPP 下同样可用。

**Q：为什么让子类不覆盖 `New`？**
A：`class()` 的 `_init_object` 已经保证父类 `__init` 先、子类 `__init` 后，参数一路透传。
覆盖 New 会把「构造配置」和「字段初始化」拆两处，且每个面板都要重复一份闭包。

---

## 9. 关键文件索引

| 关注点 | 文件 |
|---|---|
| Lua 环境与调用入口 | `Assets/Scripts/Framework/Lua/LuaManager.cs` |
| Lua 视图基类（cfg/host/节点表/生命周期） | `Assets/Scripts/Framework/Lua/LuaView.cs` |
| 生成配置（类型清单 / 成员过滤器） | `Assets/Scripts/Framework/Lua/XluaGenConfig.cs` |
| Lua 唯一直接调用的 C# 对象 | `Assets/XLua/Extensions/LuaViewBridge.cs`（属 XLua.Runtime） |
| Lua 面板基类 | `Assets/Game/LuaScripts/framework/view_base.lua` |
| C# → Lua 调用入口 | `Assets/Game/LuaScripts/framework/lua_view_host.lua` |
| 已迁移面板示例 | `Assets/Game/LuaScripts/panels/help_panel.lua`、`panels/set_panel.lua` |
| 面板适配器示例 | `Assets/Scripts/Business/Hud/LuaHelpPanelView.cs`、`LuaSetPanelView.cs` |
| AB 命名规则 | `Assets/Editor/BuildEditor/PrefabABPathPostprocessor.cs` |
| 目录重分配菜单 | 同文件 `ReassignAllBundleNames`（`Tools/AssetBundle/重新分配所有资源包名`） |
| 启动包清单 | `Assets/Scripts/Framework/AssetBundleManager/PackGroupingDefaults.cs` |

---

## 10. 一页速查（贴在手边）

```text
改 Lua 面板          → 改 panels/xxx.lua，打 lua bundle，无需重出包
加 Lua 要调的 C# 类型 → XluaGenConfig.LuaCallCSharp ⊃ 类型，然后 Generate Code
加 C# 委托给 Lua      → XluaGenConfig.CSharpCallLua ⊃ 委托类型，然后 Generate Code
生成报 CS1061/CS0306  → 多半是编辑器专用成员或 Span/ref struct，加进成员过滤器
生成代码报 CS0436     → 生成物与运行时不在同一程序集（检查 asmdef 覆盖范围）
生成器点不动          → 生成物坏了锁死编译，先删 Assets/XLua/Gen
打包报「拒绝访问」    → 包名父子冲突（文件名 vs 目录名）
资源不显示但无报错    → 查 Resources.Load / 资源名大小写 / 包名可加载性
```
