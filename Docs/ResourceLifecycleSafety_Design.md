# 资源生命周期兜底设计：归属登记 + 对账扫描

> 状态：**设计稿**（仅接口与契约，尚未实现）
> 定位：主设计文档 `GameObjectSpawnManager_Design.md` 的 **D3 缓解方案**（§8.2 展开）
> 解决的问题：无 marker 前提下，"绕过显式 `Destroy`" 导致的模板引用永久钉住 / AB 常驻

---

## 1. 问题回顾：D3 的泄漏面

主文档决策 **D3**：不做 marker 组件自动兜底，只靠显式 `Destroy`。代价是以下路径不会归还模板引用：

| 泄漏路径 | 触发频率 |
|---|---|
| 调用方直接 `GameObject.Destroy(go)` | 偶发（编码疏忽） |
| 父节点被销毁，子实例被连带销毁 | 偶发 |
| **场景卸载**：Unity 直接销毁整场景实例 | **必然，且批量** |
| owner 被丢弃，但实例仍挂在场景里 | 偶发 |

主文档 §6.2 的 `Destroy(instance)` 只能在**被显式调用**时归还引用。本方案补上"不依赖调用方"的兜底。

---

## 2. 两个机制的分工

| 机制 | 回答的问题 | 解决什么 |
|---|---|---|
| **A. 分帧对账 `TickReconcile()`** | "实例还在吗？" | 实例被**外部销毁**（含场景卸载）后归还引用 |
| **B. 归属作用域 owner** | "它的主人还在吗？" | 主人**消失**但实例还活着时，统一回收 |

### 2.1 覆盖矩阵

| 泄漏场景 | A 对账扫描 | B owner 作用域 |
|---|---|---|
| 直接 `GameObject.Destroy(go)`，owner 存活 | ✅ | ❌ 抓不到（owner 还活着） |
| 父节点被销毁（连带子实例） | ✅ | ❌ |
| **场景卸载（批量）** | ✅ | 仅当 owner 也随场景消失 |
| owner 被丢弃但实例仍在场景中 | ❌ | ✅ |
| owner 存活且忘记清理，实例也存活 | ❌ | ❌（仍需人工，靠 `DumpAll` 巡检） |

**结论：两者互补，都必须做。** A 覆盖面最广、成本最低（不改任何调用签名）；B 补上 owner 侧盲区，并提供"一个 owner 一次清理"的易用性。

### 2.2 `DestroyByParent` 已被移除（v3）

> **v3 决策**：`DestroyByParent` 从生命周期 API 中**移除**，其用例由 `ReleaseAll(owner)` 与 `ReconcileNow()` 覆盖。理由见主文档 §0.3.1。

它原本是"owner 拥有容器节点"时的一种作用域代理（`BVUIComponent` 有 `UIContainer`、`BaseView` 有 `rootView`、`BVTowerComponent` 有 `TowerContainer`），但作为**用层级结构猜归属**的手段有三个失败模式：

| 失败模式 | 说明 |
|---|---|
| 过度销毁 | 同一容器下可能挂着多个 owner 生成的对象，会连带干掉仍存活 owner 的实例 |
| re-parent 即失效 | 移出则漏（泄漏），移入则被误杀 |
| parent 先死即废 | `parent == null` 直接 no-op——而这是最常见场景（`BaseView.Release()` 已把子树连实例一起销毁），此时真正该干活的是 `ReconcileNow()` |

替代关系：

| 原用例 | 替代 |
|---|---|
| 面板关闭清场 | `ReleaseAll(this)` |
| 战斗组件 `ClearGameInfo` | `ReleaseAll(this)` |
| 父节点 / 子树被销毁 | `ReconcileNow()`（机制 A） |
| 调试时清空某节点 | 保留为调试工具并显式标注"非生命周期路径"，或删除 |

owner 作用域真正的独有价值在于：它是**唯一能驱动 weak-ref 自动兜底**的机制（§4.2 层③），并且是**唯一能把实例、句柄、pin 三类资源一次释放**的主键。

---

## 3. 机制 A：实例对账扫描（分帧增量版）

> **重要澄清**：本机制**只**回答"实例还在不在"。它**不能**替代 §4 的 owner 存活判定——一份 `InstanceRecord` 列表没有任何信息能告诉你 owner 是否已被 GC。
>
> 想让记录"知道" owner 死没死，就必须持有**不具所有权**的引用（弱引用）；若在记录里存 `object Owner` 强引用，owner 将永远不会被 GC，"owner 已死"这个事件永远不会发生。这是逻辑死结，不是实现选择。两条绕开弱引用的路都比弱引用更麻烦：终结器方案（`~ResourceScope()` 入队 + 主线程排空，不确定性更大）或只支持 `UnityEngine.Object` 作为 owner（普通 C# owner 完全失去兜底）。

### 3.1 存储结构：列表 + id→index 索引

分帧续扫要求"可按下标访问"，所以权威存储**必须是 `List`**（`Dictionary` 枚举顺序不可索引，做不了可续游标）；而 `Destroy(go)` 在热路径上需要 O(1) 查实例，因此再配一张索引表：

```csharp
// 唯一权威存储。可续游标要求按下标访问 → 必须 List
private readonly List<InstanceRecord> _records = new List<InstanceRecord>();

// instanceID -> _records 下标。Destroy(go) / 身份校验走这里，O(1)
private readonly Dictionary<int, int> _indexById = new Dictionary<int, int>();

internal sealed class InstanceRecord
{
    public string Key;                              // 模板的 bundle|asset
    public GameObject Instance;
    public ResourceHandle<GameObject> TemplateHandle;
    public int OwnerToken;                          // 0 = 无归属
    public int Index;                               // 自己在 _records 中的下标（swap-remove 时维护）
}
```

一个容器 + 一张索引表，比"dict 存记录 + list 再存一遍"少一份同步负担。

**O(1) swap-remove（同时承担游标规则 2）**：

```csharp
private void RemoveAt(int index)
{
    int last = _records.Count - 1;

    if (index != last)
    {
        InstanceRecord moved = _records[last];
        _records[index] = moved;
        moved.Index = index;                        // 维护被移动记录的下标
    }

    _records.RemoveAt(last);
    _indexById.Remove(/* 被删记录的 instanceID */);

    // ★ 游标规则 2：扫描外删除（Destroy / ReleaseAll）导致元素前移，游标跟着退
    if (index < _scanCursor) _scanCursor--;
}
```

### 3.2 分帧扫描器：双预算 + 连续轮转

#### 为什么是**两个**预算而不是一个计分

| 动作 | 成本 | 每帧预算建议 |
|---|---|---|
| 扫描一条（`rec.Instance == null`，一次 `Object.operator==`） | **纳秒级**，且成本均匀 | `scanBudget = 256` |
| 真正回收一条（`GameObject.Destroy` + `Dispose` → 可能触发 `ReleaseTemplate` → `AssetLoadManager.Release` → `AssetBundleManager.UnloadAsset`） | **微秒~毫秒级** | `reclaimBudget = 2` |

两者差 3 个数量级。用单一计分把二者混在一起，要么扫描太慢、要么回收爆帧。若确实想要统一计分，等价做法是"扫描 1 分 / 回收 50 分 / 帧预算 400 分"，但两个计数器语义更清楚，也不会出现"预算被谁吃掉了"的困惑。

#### 游标不变量（三条规则，缺一即漏扫或重扫）

```
游标 _scanCursor 语义 = "下一个待检查的下标"

规则 1  扫描中回收了当前下标 i 本身（swap-remove 会把末尾元素换到 i）：
          → _scanCursor 保持 i（不前进），让换进来的元素下一轮被检查
          否则该元素本轮被漏掉

规则 2  扫描之外（Destroy / ReleaseAll）删除了下标 r：
          → if (r < _scanCursor) _scanCursor--
          已集中在 RemoveAt() 内处理

规则 3  一轮走完（_scanCursor 越界）：
          → _scanCursor = 0，并顺带做一次完整的 owner 侧检查
```

#### 实现

```csharp
private int  _scanCursor;                       // 下一个待检查的下标
private int  _reclaimsThisFrame;
private const int ScanBudgetPerFrame    = 256;
private const int ReclaimBudgetPerFrame = 2;

/// <summary>由 GameObjectSpawnManager.Update() 每帧调用（紧跟 AssetBundleManager.Update 之后）。</summary>
public void TickReconcile()
{
    if (_records.Count == 0) { _scanCursor = 0; return; }

    _reclaimsThisFrame = 0;
    int scanned = 0;

    while (scanned < ScanBudgetPerFrame && _scanCursor < _records.Count)
    {
        int i = _scanCursor;
        InstanceRecord rec = _records[i];

        // ★ 必须用 Unity 重载的 ==（坑 1）
        if (rec.Instance == null)
        {
            if (_reclaimsThisFrame >= ReclaimBudgetPerFrame)
            {
                break;                          // 帧回收预算用完，_scanCursor 仍为 i，下帧继续
            }

            ReclaimStale(rec, i);               // swap-remove；末尾元素落到 i
            _reclaimsThisFrame++;
            scanned++;
            continue;                           // ★ 规则 1：_scanCursor 保持 i
        }

        _scanCursor = i + 1;                    // 正常前进
        scanned++;
    }

    // ★ 规则 3：一轮走完
    if (_scanCursor >= _records.Count)
    {
        _scanCursor = 0;                        // 先归零，再回收 owner，避免与规则 2 相互干扰
        ReclaimDeadOwners();                    // owner 数量远小于实例，无需分帧
        _owners.PruneEmpty();                   // 剔除名下已空的 owner 条目
    }
}

private void ReclaimStale(InstanceRecord rec, int index)
{
    GameLogController.Warning(
        $"对账：托管实例已被外部销毁，归还模板引用 {rec.Key}" +
        $"（请改为调用 GameObjectSpawnManager.Destroy）", LogModule);

    DetachFromOwner(rec);
    RemoveAt(index);
    if (rec.TemplateHandle.IsValid) rec.TemplateHandle.Dispose();
}
```

**终止性**：每次 `ReclaimStale` 都使 `_records.Count` 减 1，且回收预算上限为 2；`_scanCursor` 不变但列表变短，因此循环必然在"预算耗尽"或"游标越界"时退出，不会死循环。

**开发期自检**：记录每轮 `scanned` 总数，若某轮在 `_records.Count > 0` 的情况下 `scanned == 0`，说明游标卡住，应 `Error` 告警。

#### 连续轮转 vs 定时全量

既然已有预算封顶，就不必再等定时器：**一轮扫完立刻从头开始下一轮**（轮转）。

| 方案 | stale 窗口 | 每帧成本 |
|---|---|---|
| 原方案：2s 定时 + O(N) 全量 | 最长 2s | 每 2s 一次尖峰 |
| **本方案：预算封顶 + 连续轮转** | **`N / scanBudget` 帧**（N=500、B=256 → 约 2 帧 ≈ 33ms） | 恒定，≤256 次 null 判断 |

> **诚实说明**：以本项目当前规模（几百个实例），原来的 2s 全量扫描**完全够用**，此改动主要是为大规模场景（上千怪物/子弹）与低端机帧率稳定性买的保险，不是当前的性能瓶颈。即使不引入也不影响**正确性**——§3.4 坑 3 的身份校验已让正确性不依赖扫描频率，扫描只影响"泄漏引用被归还的及时性"。

### 3.3 接口

```csharp
namespace CarrotFantasy
{
    public sealed partial class GameObjectSpawnManager
    {
        /// <summary>
        /// 分帧增量对账。由 Update() 每帧调用，无需外部驱动。
        /// 走双预算 + 续扫游标，单帧成本有上界。
        /// </summary>
        public void TickReconcile();

        /// <summary>
        /// 立即做一次【不分帧】的完整对账（预算视为无限）。
        /// 用于场景切换后、以及调试时"我要现在就看到干净状态"。
        /// 返回本次回收的条目数。
        /// </summary>
        public int ReconcileNow(string contextName = null);

        /// <summary>场景切换后调用，等价 ReconcileNow()，额外附带 sceneName 告警上下文。</summary>
        public int ReconcileAfterSceneChange(string sceneName);
    }
}
```

`ReconcileNow` 与 `TickReconcile` 共用同一套扫描代码，只是把两个预算设为 `int.MaxValue` 并把游标复位到 0。场景切换后场景刚刚加载、帧预算宽松，适合一次扫干净。

### 3.4 ★ 四个 Unity 实现坑

#### 坑 1：判空必须走 Unity 重载的 `==`

`UnityEngine.Object` 重载了 `==`：已销毁对象 `obj == null` → `true`，但其托管包装本身非空。

| 写法 | 对已销毁对象 | 是否正确 |
|---|---|---|
| `rec.Instance == null` | `true` | ✅ |
| `ReferenceEquals(rec.Instance, null)` | `false` | ❌ 漏判 |
| `rec.Instance is null` | `false` | ❌ 漏判 |
| `rec.Instance?.name` | 抛异常 / 读到脏值 | ❌ |

**唯一的例外是 §3.4 的身份校验**——那里必须用 `ReferenceEquals`（理由见坑 3）。

#### 坑 2：`GameObject.Destroy` 是帧末延迟生效

`GameObject.Destroy(go)` 之后，**本帧内 `go == null` 仍为 false**，对象在帧末才真正销毁。因此：

- `Destroy(go)` 必须**立即**从记录表移除，不能依赖"稍后 null 判断"来清理。
- 对账扫描因此**不能**用它来发现"刚要销毁"的对象——它只发现"已经销毁"的。

补充：`GameObject.DestroyImmediate` 立即生效，对账扫描同样能捕获（但会先于显式 `Destroy` 移除而被误判为僵尸——所以显式 `Destroy` 里先移除记录这一步不能省）。

#### 坑 3：Unity 会复用 instanceID

`GetInstanceID()` 在对象销毁后可能被新对象复用。若记录表里仍留着旧条目：

```
帧1: spawn go_A → id=1000 → _indexById[1000] = i,  _records[i] = rec_A
帧1: go_A 被外部销毁（条目残留）
帧2: spawn go_B → Unity 复用 id=1000 → 新记录写入 _records，_indexById[1000] 指向它
     ← rec_A 若无碰撞检测就被静默覆盖，其模板引用无声泄漏
帧3: Destroy(go_A)  ← 陈旧引用
      → _indexById[1000] 命中 rec_B
      → 若不做身份校验：会把 rec_B 移除并 Dispose rec_B 的模板引用
      → go_B 从此变成"非托管" → 后续 Destroy(go_B) 走非托管分支 → 再泄漏一次
```

两处防护：

**① `Destroy` 增加身份校验**

```csharp
public bool Destroy(GameObject instance)
{
    if (instance == null) return false;                 // Unity 重载：幂等

    int id = instance.GetInstanceID();
    if (!TryGetByInstanceId(id, out InstanceRecord rec))
    {
        GameLogController.Warning($"Destroy: 非托管实例 {instance.name}，退化为 GameObject.Destroy", LogModule);
        GameObject.Destroy(instance);
        return false;
    }

    // ★ 用 ReferenceEquals 做托管身份校验（不要用 Unity 重载的 ==，语义不同）
    if (!ReferenceEquals(rec.Instance, instance))
    {
        // instanceID 被复用：rec 是陈旧条目
        GameLogController.Warning(
            $"Destroy: instanceID {id} 已被复用，先回收陈旧条目 {rec.Key}", LogModule);
        DetachFromOwner(rec);
        RemoveAt(rec.Index);                            // ★ 必须走 RemoveAt 以维护游标（规则 2）
        if (rec.TemplateHandle.IsValid) rec.TemplateHandle.Dispose();
        GameObject.Destroy(instance);
        return false;
    }

    // ★ 立即移除，不能等帧末（坑 2）
    RemoveAt(rec.Index);                                // ★ 内含游标规则 2
    GameObject.Destroy(instance);

    DetachFromOwner(rec);
    if (rec.TemplateHandle.IsValid) rec.TemplateHandle.Dispose();
    return true;
}
```

**② `Spawn` 检测 ID 碰撞**

```csharp
int id = go.GetInstanceID();
if (TryGetByInstanceId(id, out InstanceRecord old))
{
    // 只可能是陈旧条目（旧对象已销毁但未被对账回收）
    GameLogController.Warning(
        $"Spawn: instanceID {id} 碰撞，先回收陈旧条目 {old.Key}", LogModule);
    DetachFromOwner(old);
    RemoveAt(old.Index);
    if (old.TemplateHandle.IsValid) old.TemplateHandle.Dispose();
}

var rec = new InstanceRecord { Instance = go, Index = _records.Count, /* … */ };
_records.Add(rec);
_indexById[id] = rec.Index;
```

> **重要性质**：有了身份校验，**正确性不再依赖扫描频率**——即使对账很稀疏、陈旧条目长期存在，也只会晚一点归还引用，不会误放他人的引用。这让扫描频率（`scanBudget`）可以纯粹按"回收及时性"来选（§3.2），而不必为正确性妥协。
>
> **唯一不能省的是 `RemoveAt`**：所有删除路径都必须经过它，否则 `Index`、`_indexById` 与 `_scanCursor` 三者会失去同步。

#### 坑 4：记录的键与索引

- 索引表 `_indexById` 的键用 `int instanceID`（省内存、可比），**记录里必须同时存 `GameObject Instance`** 以支持坑 1 与坑 3 的两种判断。
- 不要把 `_indexById` 换成 `Dictionary<GameObject, …>`：`UnityEngine.Object` 的 `Equals`/`GetHashCode` 同样基于 instanceID，**ID 复用碰撞问题一模一样**，换键并不能规避。
- **`_records` 的下标会被 swap-remove 打乱**，所以 `InstanceRecord.Index` 必须在 `RemoveAt` 里跟着维护；任何"记住了下标"的地方都不得跨帧信赖它（唯一例外是扫描器自己的 `_scanCursor`，它有 §3.2 的三条规则兜着）。

### 3.5 触发时机

| 触发点 | 时机 | 作用 |
|---|---|---|
| **每帧 `TickReconcile()`** | `GameObjectSpawnManager.Update()`，紧跟 `AssetBundleManager.Update()` | 主兜底。双预算封顶，连续轮转，stale 窗口 ≈ `N/scanBudget` 帧 |
| **场景切换后 `ReconcileNow()`** | `SceneServer` 加载完新场景、`InitSceneObject()` 之后 | 一次不分帧扫干净（此时帧预算宽松），回收上场景批量产生的僵尸条目 |
| `ReleaseAll(owner)` 顺带 | owner 清理时 | 保证记录表与 owner 表一致 |
| `Destroy(instance)` 顺带 | 单实例回收时 | 从 owner 表摘除该实例，并走 `RemoveAt` 维护游标（规则 2） |
| `ExplainBundle` / `DumpAll()` 顺带 | 人工排查时 | 保证诊断结果准确 |

**不要复用 `AssetBundleManager` 那个 60s 的 `UnloadCheckCoroutine`**：粒度太粗，会让僵尸条目在 60s 内持续占用模板引用，期间包无法卸载。

**驱动位置**：`TickReconcile()` 挂在 `GameObjectSpawnManager` 自己的 `Update()` 上（由 `GameMain.Update` 驱动，见 `GameMain.cs:58`），不要复用 `AssetBundleManager` 的巡检协程。

场景切换接入点（`SceneServer.LoadSceneProgress`，`SceneServer.cs:107-112`）：

```csharp
this.currentScene.InitSceneObject();
GameObjectSpawnManager.Instance.ReconcileAfterSceneChange(unitySceneName);   // ← 新增，不分帧
this.eventDispatcher.DispatchEvent(SceneEventType.LOAD_SCENE_FINISH);
this.currentScene.Init();
```

### 3.6 记录池与全局清理

> **先澄清一个常见误解**：对象池**不能**解决"强引用 owner 导致层③失效"的问题。两者是正交的：
>
> | 机制 | 解决什么 |
> |---|---|
> | 记录池 | **分配churn / GC 压力**（每次 Spawn 都 new 一个 `InstanceRecord`） |
> | 弱引用 | **在不阻止 owner 被回收的前提下，检测 owner 已消失** |
>
> 池化复用记录**容器**，但不改变"记录指向谁"。若记录里存 `object Owner` 强引用，那么只要实例还活着、记录还在用（无论它是不是从池里租出来的），owner 就被钉住 → **恰好在我们需要检测的那一刻无法检测**。池化反而让这个窗口更长（记录被反复复用）。
>
> **好消息**：现行设计里 `InstanceRecord` 存的是 `int OwnerToken`，**本来就没有对 owner 的强引用**。唯一持有弱引用的是 `OwnerEntry`，而那是层③结构上必需的——是否保留层③见 Q18；若决定放弃层③，则可以让 `InstanceRecord` 直接存强引用 `object Owner` 并省掉整张 token 表，那也是一条自洽的路线（代价见 §4.2）。

#### 3.6.1 `InstanceRecordPool`

`InstanceRecord` 与 `RequestRecord` 都是"每次 Spawn 分配一次"的高churn小对象（一场战斗可能产生数千个），值得池化：

```csharp
internal sealed class InstanceRecordPool
{
    private readonly Stack<InstanceRecord> _pool = new Stack<InstanceRecord>();
    private const int MaxRetained = 512;      // 上限，避免大战后退还内存

    public InstanceRecord Rent()
    {
        return _pool.Count > 0 ? _pool.Pop() : new InstanceRecord();
    }

    public void Return(InstanceRecord rec)
    {
        rec.Reset();                          // ★ 必须先清干净
        if (_pool.Count < MaxRetained) _pool.Push(rec);
    }

    public void Clear() => _pool.Clear();
    public int Count => _pool.Count;
}
```

`InstanceRecord.Reset()` **必须**清空全部字段——漏一个就是"池化把已销毁对象钉在池里"的经典泄漏：

```csharp
public void Reset()
{
    Key = null;                     // string，避免长期驻留
    Instance = null;                // ★ 不清 → 池成为已销毁 GameObject 的强引用根
    TemplateHandle = default;       // ★ struct，不清 → 复用时误 Dispose 别人的引用
    OwnerToken = 0;
    Index = -1;
    InPool = false;
}
```

**归还顺序**：`Dispose()` 模板句柄 → `DetachFromOwner()` → `RemoveAt(index)` → `Return(rec)`。也就是说**池里永远不会存有未释放句柄的记录**。

**双归还防护**：`Reset()` 里 `InPool = true`，`Rent()` 里断言 `InPool == false`；若同一个记录被归还两次，第二次应 `Error` 告警并丢弃——否则池里出现同一条记录的两份引用，下次租出会直接破坏身份校验。

> `RequestRecord`（在途请求）同样处理。`TemplateEntry`（每个 `bundle|asset` 一条，量级几百）与 `OwnerEntry`（owner 数量级几十）**不需要池化**。

#### 3.6.2 全局清理：`ClearAll()`

```csharp
public sealed partial class GameObjectSpawnManager
{
    /// <summary>
    /// 释放本管理器登记的全部资源并清空所有容器。
    /// 【顺序不可调换】：先释放、再清空；只清空容器 = 直接泄漏 AB 引用。
    /// </summary>
    /// <param name="destroyInstances">true = 连带销毁实例；false 仅在实例已被销毁时使用。</param>
    public void ClearAll(bool destroyInstances = true);

    /// <summary>只清空闲置记录池，不动任何存活登记。</summary>
    public void ClearPool();
}
```

**`ClearAll()` 的固定顺序**（每一步都不能省，否则引用计数失衡）：

```
ClearAll(destroyInstances):
  1. 逐条释放 _records
       ├─ destroyInstances && rec.Instance != null → GameObject.Destroy(rec.Instance)
       ├─ rec.TemplateHandle.Dispose()            ← ★ 必须，否则 AB 引用永久泄漏
       ├─ DetachFromOwner(rec)
       └─ Return(rec) 归还池
  2. PrefabLoadManager.Instance.UnpinAll(owner) 之类：解除全部 pin
  3. ResourceReleaser.ReleaseHandles(owner)：释放全部资源句柄
  4. _records.Clear();  _indexById.Clear();
  5. 未交付的 _requests：逐个标记 Cancelled（底层回调到达时会被 §3.3 坑 B 的标记拦截，安全）
  6. _owners.Clear()
  7. ★ _ownerTokens 重建：ConditionalWeakTable 【在 .NET Framework 上没有 Clear()】，
       为可移植性直接把字段改成非 readonly 并整体替换：
           _ownerTokens = new ConditionalWeakTable<object, OwnerTokenBox>();
       不重建的话，ClearAll 之后 ResolveToken(owner) 会返回一个已不存在的 token。
  8. _scanCursor = 0;  _reclaimsThisFrame = 0;
```

**为什么必须有这个方法**（三个场景）：

| 场景 | 说明 |
|---|---|
| 整体清场 | 比 `DestroyAll()` 更彻底：连 pin 与句柄一起收，是 `ReconcileNow()` 的"强制版" |
| 编辑器 Domain Reload 关闭时的残留（§9 R6） | 静态/长生命周期状态在两次 Play 之间不重置，需要在 `[RuntimeInitializeOnLoadMethod]` 里 `ClearAll()` |
| 测试与调试 | 用例之间要一个干净起点 |

**危险变体不要提供**：不要做"只清 `_records` / `_indexById` 而不释放句柄"的接口。那样 AB 引用计数只增不减，且外部已交付的实例变成"非托管"，后续 `Destroy` 走非托管分支——比不做还糟。

---

## 4. 机制 B：归属（owner）作用域

### 4.1 先澄清一个事实

> "类的实例销毁的时候，固定调用一次清理逻辑" —— 这个信号**在 C# 里没有自动版本**。

普通 C# 类没有析构钩子；`~Finalizer()` 的 GC 时机不确定，且不能安全调用 Unity API。因此"owner 销毁"只能靠三层信号，可靠性递减：

| 层 | 信号 | 可靠性 | 适用 owner |
|---|---|---|---|
| **①** | owner 在自己的 `Dispose()` / `ClearGameInfo()` / `Release()` 里显式调 `ReleaseAll(this)` | **确定性，首选** | 全部 |
| **②** | owner 是 `UnityEngine.Object`（`MonoBehaviour`/`ScriptableObject`）→ `owner == null` | 及时 | Unity 对象 |
| **③** | owner 是普通 C# 类 → `WeakReference` 被 GC | 自动，**时机不确定** | 普通类 |

本项目的 owner 绝大多数是**普通 C# 类**且已有显式拆除方法（`BaseView.Release`、`BaseBattleViewComponent.ClearGameInfo`、`BattleManager.Dispose`），所以 **① 是主路径**，②③ 是兜底。

### 4.2 为什么必须用弱引用

若用 `Dictionary<object, OwnerEntry>` 直接以 owner 为键，**字典会强引用 owner → owner 永远不会被 GC → 层③ 永不触发**，兜底形同虚设。

正确做法是两层结构（v3：下沉为共享组件 **`OwnerRegistry`**，见主文档 §6.5）。

#### 把它做成"可整体删除的一小块"

弱引用只服务于 §4.1 的**层③**。既然它是唯一显得麻烦的部分，就把它**隔离在一个独立小类**里，不污染主路径：

| 层 | 实现成本 | 建议 |
|---|---|---|
| ① 显式 `ReleaseAll(owner)` | 极低，本来就要做 | **必做** |
| ② `owner is UnityEngine.Object && owner == null` | 3 行，零额外结构 | **必做** |
| ③ `WeakReference` GC 兜底 | ~40 行 | **可选**，独立成 `WeakOwnerTracker` |

```csharp
/// <summary>
/// 层③：基于弱引用的"owner 是否已被 GC"追踪。
/// 【可整体删除】——删掉本类，并把 OwnerRegistry.IsAlive 的普通 C# 分支改为恒返回 true，
/// 即退化为只支持层①②，主流程与其它类型无需任何改动。
/// </summary>
internal sealed class WeakOwnerTracker
{
    private readonly ConditionalWeakTable<object, TokenBox> _tokens = new();
    private readonly Dictionary<int, WeakReference> _refs = new();   // token -> 弱引用
    private int _next = 1;

    public int  Track(object owner);    // 已登记则复用原 token
    public bool IsAlive(int token);     // 未登记的 token → 视为存活（保守）
    public void Forget(int token);      // 回收后清理
}

internal sealed class TokenBox { public int Token; }
```

**删掉层③的代价**：普通 C# owner 忘记 `ReleaseAll` 时**静默永久泄漏**——实例仍存活，机制 A 的对账扫描（§3）抓不到，因为 `rec.Instance != null`。这是明确接受的取舍，不是疏漏。（`UnityEngine.Object` owner 仍受层②保护。）

#### 对外只暴露 4 个方法

`OwnerRegistry` 的公开面收窄到主流程真正需要的部分，其余（`EnumerateTokens` / `CollectDeadTokens`）降为 `internal` 或并入诊断：

```csharp
public sealed class OwnerRegistry
{
    // owner -> token 的弱键映射。键被 GC 后条目自动清理，不阻止回收。
    private readonly ConditionalWeakTable<object, OwnerTokenBox> _ownerTokens = new();

    // token -> 可回收数据。我们自己持有，才能在 owner 死后据以回收。
    private readonly Dictionary<int, OwnerEntry> _owners = new();

    public int  ResolveToken(object owner);   // 主流程唯一写入口；null / 值类型 → 0
    public bool IsAlive(int token);           // 层①②③ 判定
    public OwnerEntry Get(int token);         // 取登记表（InstanceIds / PinnedKeys / HandleIds）
    public void Remove(int token);            // 回收后清理

    internal void PruneEmpty();               // 由 TickReconcile 的轮末调用
    internal IReadOnlyCollection<int> CollectDeadTokens();   // 同上
}

internal sealed class OwnerTokenBox { public int Token; }

internal sealed class OwnerEntry
{
    public string Name;                          // 诊断用（owner 死后仍可读）
    public WeakReference WeakOwner;              // 普通 C# 类 → GC 信号（层③）
    public UnityEngine.Object UnityOwner;        // MonoBehaviour → == null 信号（层②）
    public readonly HashSet<int> InstanceIds = new HashSet<int>();   // SpawnManager 登记
    public readonly HashSet<string> PinnedKeys = new HashSet<string>(); // PrefabLoadManager 登记
    public readonly HashSet<int> HandleIds = new HashSet<int>();     // 各 ResourceCache 登记
}
```

`OwnerEntry` 同时持有三类资源的登记，`ReleaseAll(owner)` 才能一次清干净（主文档 §6.3）。

- `ConditionalWeakTable<TKey,TValue>`：`System.Runtime.CompilerServices`，.NET Standard 2.0+，Unity 2023 可用。要求 `TKey : class`。
- 对 Unity 对象**额外持有强引用**（`UnityOwner`）是无害的：托管包装很小，而原生对象的销毁由 `== null` 立即反映——这比等 GC 可靠得多。
- `OwnerEntry.Name` 在 owner 死后仍可用于日志（弱引用已失效，不能再读 owner）。
- **`PruneEmpty()`**：owner 存活但名下三类资源都已清空时，条目应被剔除，否则 owner 表只增不减（长时间运行的面板反复开关会积累）。建议在 `TickReconcile()` 的轮末顺带调用。

### 4.3 owner 存活判定

```csharp
private static bool IsOwnerAlive(OwnerEntry oe)
{
    // ② Unity 对象：== 重载，原生对象已销毁则立即为 false
    if (oe.UnityOwner != null) return oe.UnityOwner != null;

    // ③ 普通 C# 类：弱引用
    if (oe.WeakOwner != null && oe.WeakOwner.IsAlive)
    {
        object t = oe.WeakOwner.Target;
        return t != null;
    }

    return false;
}
```

注意顺序：**先判 `UnityOwner`**。若 owner 是 `MonoBehaviour`，`WeakOwner` 的 `Target` 可能因托管包装仍被 Unity 内部引用而为 `true`，此时只有 `== null` 能给出正确答案。

### 4.4 接口

```csharp
namespace CarrotFantasy
{
    public sealed partial class GameObjectSpawnManager
    {
        /// <summary>
        /// 生成一个【有归属】的实例。owner 通常是 this。
        /// ReleaseAll(owner) 会一次销毁该 owner 名下所有实例。
        /// </summary>
        public SpawnRequest SpawnOwned(object owner, string bundleName, string assetName,
                                       Action<GameObject> onReady,
                                       SpawnOptions options = default);

        /// <summary>
        /// 生成一个【无归属】的实例。用于单例 / 静态上下文（AudioManager、MapConfigReader 等），
        /// 其资源视为进程级，不参与自动回收。
        /// </summary>
        public SpawnRequest SpawnGlobal(string bundleName, string assetName,
                                        Action<GameObject> onReady,
                                        SpawnOptions options = default);

        /// <summary>
        /// 销毁该 owner 名下的所有托管实例并归还模板引用（同时解 pin、释放句柄）。
        /// 返回回收的实例数。owner 为 null 时返回 0
        /// （无归属实例需用 Destroy(instance) 或 Reconcile() 回收）。
        /// </summary>
        public int ReleaseAll(object owner);

        /// <summary>该 owner 当前名下还有多少活跃实例。</summary>
        public int GetOwnedCount(object owner);
    }
}
```

> **v3 变更**：归属不再是 `SpawnOptions` 里的可选字段（可选即"可以忘"），改为 `SpawnOwned` / `SpawnGlobal` 两个命名方法，强迫每个调用点做一次自觉选择。

### 4.5 典型用法

**改造前**（`BVTowerComponent.ClearGameInfo`，约 25 行手工清理）：

```csharp
public override void ClearGameInfo()
{
    foreach (var kv in _towerPrefabHandles) kv.Value.Dispose();
    _towerPrefabHandles.Clear();
    _towerPrefabTemplates.Clear();
    if (_buildEffectHandle.IsValid) { _buildEffectHandle.Dispose(); _buildEffectHandle = AssetLoadHandle.Invalid; }
    if (_sellEffectHandle.IsValid)  { _sellEffectHandle.Dispose();  _sellEffectHandle  = AssetLoadHandle.Invalid; }
    foreach (var info in this.towerViewDic) { GameGameObject.Destroy(...); ... }
    this.towerViewDic.Clear();
    this.RemoveListener();
}
```

**改造后**：

```csharp
public override void ClearGameInfo()
{
    GameObjectSpawnManager.Instance.ReleaseAll(this);   // ← 一行，不可能漏
    this.towerViewDic.Clear();
    this.RemoveListener();
}
```

### 4.6 边界与反例（必须明确）

| 情况 | 行为 | 说明 |
|---|---|---|
| **单例 / 静态上下文**（`AudioManager`、`MapConfigReader`、`MapServer`） | owner 传 `null` | 它们永不销毁，资源为进程级；登记归属无意义 |
| **池化对象**（`GameViewObjectPool` 里的 `BattleUnitView`） | 归还池时**必须显式** `ReleaseAll(this)` | 弱引用会让 owner 长期存活（池持有它），层③ 永不触发 |
| **owner 存活但实例被外部销毁** | 由机制 A 处理 | owner 侧看不到 |
| **owner 是值类型 / 字符串** | 拒绝登记 + 告警 | `ConditionalWeakTable` 要求引用类型 |
| **owner 死后自动回收实例** | **销毁实例 + 归还引用 + 告警** | 见 §6 |

---

## 5. `Spawn` 的登记路径

```
Spawn(bundle, asset, onReady, options)
 …
OnTemplateReady(requestId, template)
 ├─ template == null → onReady(null) + 归还 handle
 └─ 正常
      go = Instantiate(template, options.Parent, options.WorldPositionStays)
      应用 NameOverride / LocalPosition / LocalScale / SetActiveAfterSpawn

      int id = go.GetInstanceID()
      若 TryGetByInstanceId(id) 命中 → 按僵尸回收旧条目并告警（坑 3-②，走 RemoveAt）

      int ownerToken = ResolveOwnerToken(options.Owner)   // 0 = 无归属
      var rec = new InstanceRecord { Key, Instance = go, TemplateHandle = req.TemplateHandle, OwnerToken = ownerToken }
      rec.Index = _records.Count;  _records.Add(rec);  _indexById[id] = rec.Index
      if (ownerToken != 0) _owners[ownerToken].InstanceIds.Add(id)

      req.Callback(go)
```

```csharp
private int ResolveOwnerToken(object owner)
{
    if (owner == null) return 0;

    if (owner.GetType().IsValueType)
    {
        GameLogController.Warning($"Spawn: owner 不能是值类型（{owner.GetType().Name}），已忽略归属", LogModule);
        return 0;
    }

    OwnerTokenBox box = _ownerTokens.GetValueOrCreate(owner, () => new OwnerTokenBox { Token = _nextOwnerToken++ });

    if (!_owners.TryGetValue(box.Token, out OwnerEntry oe))
    {
        oe = new OwnerEntry { Name = owner.GetType().Name };
        if (owner is UnityEngine.Object uo) oe.UnityOwner = uo;      // ② 信号
        else                                oe.WeakOwner  = new WeakReference(owner);   // ③ 信号
        _owners[box.Token] = oe;
    }
    return box.Token;
}
```

> `ConditionalWeakTable` 没有 `GetValueOrCreate`，需用 `GetValue(key, callback)`。

---

## 6. owner 死亡时的回收策略

owner 死后发现名下仍有实例，说明**没有任何托管代码还能持有这些实例的引用**——它们无法再被 `Destroy`，属于永久泄漏（GameObject 与模板引用都泄漏）。

两种策略：

| 策略 | 行为 | 评价 |
|---|---|---|
| **A（推荐）** | **销毁实例 + 归还引用 + 告警** | 实例已不可达、必然泄漏，销毁是唯一正确处置 |
| B（保守） | 只归还引用 + 告警，保留实例 | ⚠️ **危险**：归还引用后包可能空闲卸载，而残留实例仍引用其材质/贴图 → 变粉 |

**推荐 A**，但**必须告警**，让开发者发现缺失的显式 `ReleaseAll`。绝不允许静默自动销毁——那会让"对象莫名消失"变成难以定位的问题。

策略 A 的注意点：**不要在遍历 `_records` 的过程中直接 Destroy**，先快照再逐个走正常 `Destroy` 流程（保持重入保护一致）。本设计在轮末（`_scanCursor` 已归零）才做 owner 侧回收，因此 `RemoveAt` 的游标规则 2 不会与它相互干扰。

---

## 7. 扫描的完整流程

```
── 每帧：TickReconcile()（预算封顶，可续）
   _reclaimsThisFrame = 0;  scanned = 0

   while scanned < scanBudget(256) && _scanCursor < _records.Count:
       i = _scanCursor
       rec = _records[i]

       if rec.Instance == null:                       // 坑 1：Unity 重载 ==
           if _reclaimsThisFrame >= reclaimBudget(2):
               break                                  // _scanCursor 仍为 i，下帧继续
           Warn($"实例已被外部销毁，归还模板引用 {rec.Key}")
           DetachFromOwner(rec)
           RemoveAt(i)                                // swap-remove；内部含游标规则 2
           rec.TemplateHandle.Dispose()
           _reclaimsThisFrame++; scanned++
           continue                                   // ★ 规则 1：_scanCursor 保持 i

       _scanCursor = i + 1; scanned++                 // 正常前进

   if _scanCursor >= _records.Count:                  // ★ 规则 3：一轮走完
       _scanCursor = 0
       ReclaimDeadOwners()                            // owner 少，不分帧
       _owners.PruneEmpty()

── 一轮结束时：ReclaimDeadOwners()（机制 B）
   foreach token in _owners where !IsOwnerAlive(entry):     // 层①②③ 判定，见 §4.3
       oe = _owners[token]
       if oe.InstanceIds.Count > 0:
           Warn($"owner [{oe.Name}] 已销毁但仍有 {oe.InstanceIds.Count} 个托管实例，自动回收")
       foreach id in oe.InstanceIds.ToArray():        // 快照，避免边遍历边改
           if TryGetByInstanceId(id, out rec): Destroy(rec.Instance)   // 走正常流程（§6）
       _owners.Remove(token)                          // _ownerTokens 由 ConditionalWeakTable 自动清理
```

**重入保护**：`ReclaimDeadOwners` 内部调用 `Destroy` 会走 `RemoveAt` 并调整 `_scanCursor`。因为此处在规则 3 之后、且已把 `_scanCursor` 归零，规则 2 的 `index < _scanCursor` 恒为 false，不会相互干扰。若将来给 `Destroy` 增加用户回调（Q12），需把整段纳入 `_dispatchDepth` 保护。

**不分帧版本**：`ReconcileNow()` 走同一份代码，只是把两个预算设为 `int.MaxValue` 并把 `_scanCursor` 复位为 0。

---

## 8. 跨层推广：`ResourceHandle` 也纳入 owner 清理

`ResourceCache<T>` 的句柄同样存在"忘记 `Dispose`"的问题（图片、音频）。既然 owner 概念已建立，建议**让归属成为跨层能力**：

```csharp
public readonly struct ResourceHandle<T> : IDisposable
{
    // …既有成员
    /// <summary>该句柄的归属者（可为 null）。</summary>
    public object Owner { get; }
}

// 统一清理入口：同时覆盖 Spawn 出来的实例与所有类型缓存的句柄
public static class ResourceReleaser
{
    public static int ReleaseAll(object owner);
}
```

`ResourceReleaser.ReleaseAll(owner)` 依次调用：

```
GameObjectSpawnManager.Instance.ReleaseAll(owner)
PrefabLoadManager.Instance.ReleaseAll(owner)
ImageLoadManager.Instance.ReleaseAll(owner)
AudioLoadManager.Instance.ReleaseAll(owner)
TextLoadManager.Instance.ReleaseAll(owner)
```

这才真正实现"**一个 owner 一次清理**"的统一入口。需要 `ResourceCache<T>` 支持按 owner 反查请求，并保证被 owner 释放过的句柄后续 `Dispose()` 是幂等空操作（现有设计已是幂等，配合主文档 §3.2 的代际号还能顺带告警误用）。

### 8.1 v3 起这是"自然结果"而非额外功能

由于 `OwnerRegistry` 已下沉为**共享组件**（§4.2），且 `OwnerEntry` 本身就登记了三类资源：

```csharp
OwnerEntry {
    HashSet<int>    InstanceIds;   // GameObjectSpawnManager 登记
    HashSet<string> PinnedKeys;    // PrefabLoadManager 登记（pin）
    HashSet<int>    HandleIds;     // 各 ResourceCache 登记（句柄）
}
```

`ResourceReleaser.ReleaseAll(owner)` 只是"遍历 `OwnerEntry` 的三类登记，通知对应层各自释放"，不需要任何跨层反查基础设施。因此 v3 下**跨层统一清理的成本已经很低**，建议与 `GameObjectSpawnManager` 一起做，而不是留到下一步。

> 释放顺序仍是：① 销毁实例 → ② 解 pin → ③ 释放句柄（主文档 §6.3），保证中间不会出现"实例还在但引用已归零"的假 0。

---

## 9. 剩余风险

| # | 风险 | 说明 | 缓解 |
|---|---|---|---|
| R1 | **GC 时机不确定**（层③） | owner 被丢弃后可能很久才被回收，期间资源持续被钉住 | ① 为主路径；③ 仅为兜底，不承诺及时性 |
| R2 | **自动销毁的行为可预测性** | 对象可能在不可预期的帧消失 | 必须告警；绝不允许静默；由 `SpawnOwned` / `SpawnGlobal` 显式选择是否纳入归属 |
| R3 | **【实例池】与弱引用冲突** | `GameViewObjectPool` 这类**实例池**长期持有 owner → 层③ 永不触发。注意与 §3.6 的**记录池**（`InstanceRecordPool`）不是一回事 | 归还池时必须显式 `ReleaseAll`（§4.6）；池缓存实例须 `PinTemplate(..., poolOwner)`（主文档 §7.3） |
| R4 | **对账扫描开销** | 每帧最多 `scanBudget` 条 null 判断 + `reclaimBudget` 次回收，成本有上界 | 双预算取值见 Q19；按真机 profiling 调整（§3.2） |
| R5 | **owner 存活且双双忘记清理** | 两个机制都抓不到 | 只能靠 `ExplainBundle` / `DumpAll()` 人工巡检（主文档 §8.3、§8.4） |
| R6 | **`ConditionalWeakTable` 与 Unity 域重载** | 编辑器下关闭 Domain Reload 时状态可能残留 | 用 `ClearAll()`（§3.6.2）在 `[RuntimeInitializeOnLoadMethod]` 中重置；注意该表**没有 `Clear()`**，需整体替换实例 |
| R7 | **owner 表只增不减** | 面板反复开关会让 owner 条目累积 | `OwnerRegistry.PruneEmpty()`，在 `TickReconcile()` 轮末顺带调用（§4.2） |
| R8 | **【记录池】未清字段** | 池化 `InstanceRecord` 若 `Reset()` 漏清 `Instance`，池会变成已销毁 GameObject 的强引用根；漏清 `TemplateHandle` 会误 Dispose 他人引用 | `Reset()` 必须清全部字段；双归还防护（§3.6.1） |

---

## 10. 契约增补（并入主文档 C 系列）

| # | 契约 |
|---|---|
| C8 | 用 `SpawnOwned(owner, …)` 生成的实例，**不要**再对该实例单独调用 `Destroy`（会与 `ReleaseAll` 冲突；第二次是幂等的非托管分支 + 告警，但语义混乱）。二选一。 |
| C9 | 池化对象归还池时**必须**显式 `ReleaseAll(this)`。 |
| C10 | 单例 / 静态上下文传 `Owner = null`，其资源为进程级，不参与自动回收。 |
| C11 | `ReleaseAll(owner)` 后，该 owner 名下的实例已销毁、句柄已失效，不要再使用。 |

---

## 11. 待确认问题

| # | 问题 | 建议 |
|---|---|---|
| Q8 | ~~对账扫描频率：固定间隔还是每 N 帧~~ | **已由 §3.2 定案**：改为"双预算 + 连续轮转"，不再用定时器。stale 窗口 ≈ `N/scanBudget` 帧 |
| Q9 | owner 死亡时：策略 A（销毁实例）还是 B（只归还引用）？ | 建议 **A + 强制告警**（B 会有材质变粉风险） |
| ~~Q10~~ | ~~`SpawnOptions.Owner` 是否必填~~ | **已被 v3 取代**：改为 `SpawnOwned(owner, …)` / `SpawnGlobal(…)` 两个命名方法，强迫每个调用点自觉选择（主文档 §6） |
| Q11 | 是否本期就做跨层 `ResourceReleaser.ReleaseAll`（§8）？ | v3 起成本已很低（`OwnerEntry` 本身登记三类资源），建议**与 `GameObjectSpawnManager` 一起做** |
| Q12 | 是否需要 `SpawnOptions.OnDestroy` 回调（`Destroy` / `ReleaseAll` 时触发）？ | 视业务需要；注意与对账扫描的重入保护配合 |
| Q17 | `OwnerRegistry.PruneEmpty()` 的调用时机？ | 建议挂在 `TickReconcile()` 的轮末顺带调用 |
| **Q18** | **是否保留层③（弱引用 GC 兜底）**？删掉可省掉 `WeakOwnerTracker` 约 40 行，主流程无需改动 | 建议**保留但隔离**；若删，普通 C# owner 忘记 `ReleaseAll` 会静默永久泄漏（§4.2） |
| **Q19** | 分帧扫描的双预算取值（`scanBudget=256` / `reclaimBudget=2`） | 建议先用此值，再按真机 profiling 调整；两项都应可配置 |
| **Q20** | `InstanceRecord` / `RequestRecord` 是否池化（§3.6.1）？ | 建议**做**，约 30 行，收益是消除每帧数十~数百次小对象分配（低端机 GC 尖刺）。注意与 Q18 无关——池化不解决强引用问题 |
| **Q21** | `ClearAll()` 是否本期就做（§3.6.2）？ | 建议**做**，它是场景整体清场、编辑器 Domain Reload 重置、测试起点的共同需求 |
