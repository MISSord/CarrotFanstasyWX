# 战斗 combat 流程（Model 层）

本文描述 PVE 单逻辑帧内：**防御塔索敌 → 子弹移动 → 碰撞结算** 的顺序与职责划分。  
入口注册见 `Battle/Pve/PveBattleComponentSetup.cs`。

---

## 1. 单逻辑帧 Tick 顺序

`BaseBattle.SimulateOneLogicFrame` 按 `componentList` 注册顺序依次 `OnTick`，PVE 战斗为：

| 顺序 | 组件 | 本帧主要行为 |
|------|------|----------------|
| 1 | Input | 处理建造/升级/卖塔等指令 |
| 2 | Monster | 怪物移动，`Transform` 更新 |
| 3 | **Tower** | **集火分配** → 各塔索敌/开火 → `BULLET_BUILD` |
| 4 | **Bullet** | 本帧新建的弹 + 已在飞的弹 **移动** |
| 5 | **HitTest** | **RefreshSpatialGrid** → 子弹 vs 怪物/物品 **碰撞** |
| 6 | Scheduler | 延迟任务 |

同帧内 **LateTick**：各 Unit 的 `lastFrameX/Y` 与视图同步（在全部 `OnTick` 之后）。

```
Input → Monster(移动) → Tower(集火+开火) → Bullet(移动) → HitTest(网格+碰撞)
```

**设计意图**：子弹先移动，再碰撞，避免「未移动就判撞」。

---

## 2. Transform 与空间网格（为何 HitTest 要 Refresh）

两套数据不要混为一谈：

| | `UnitTransformComponent` | `BattleSpatialGrid` |
|---|---|---|
| 更新时机 | `SetPosition` 时立刻改 `x/y` 与 `bodyHitTestShape` | 仅 `Clear + InsertAll` 时重建 |
| 用途 | **窄相位**（圆-圆精确相交） | **宽相位**（按格子筛候选） |

单位移动后 **圆心已是最新**，但网格里仍可能把它挂在 **上一帧的格子**。  
因此 **`BattleSimpleHitTestComponent.OnTick` 开头** 必须调用 `RefreshSpatialGrid()`，再跑怪物/物品 vs 子弹的 broad phase。

> 到 HitTest 执行时，怪物/塔/子弹的本帧 `OnTick` 都已跑完，Refresh 用的是 **当帧最终 Transform**。

**集火分配不在 HitTest 里做 Refresh**：它在 Tower 阶段、HitTest 之前调用，且塔数量少，改为 **遍历全部塔 + 当前碰撞圆**（见下节），不依赖网格。

---

## 3. 防御塔索敌

### 3.1 集火（玩家选中目标）

- 视图点击物品 → `BattleEvent.TARGET_CHANGE` → `HitTest.SetTarget` 记录全局 `targetUnit`。
- 每帧 **TowerComponent.OnTick 开头** 调用 `AssignTowerFocusTargets()`：
  1. 清空所有塔的 `targetUnit`
  2. 若有集火目标：**遍历全部塔**，用当前 `bodyHitTestShape` 与目标做圆-圆检测，在射程内的塔写入 `targetUnit`

不使用空间网格，避免「Tower 阶段早于 HitTest Refresh」导致误解或重复重建。

### 3.2 单塔选目标（`BattleUnit_Tower.OnTick`）

优先级：

1. **`targetUnit`（集火）** — 仍在射程内则打它（怪物或物品）
2. **否则** — `CollectMonstersInRange()` 扫描射程内怪物，选 **距终点最近** 的一只（`EndPointDistance` 最小）
3. **无目标** — 不开火，且 `timeVal = attackCD`（不堆 excess CD，避免久未攻击连发）

注意：**自动索敌只扫怪物，不扫物品**；打物品需玩家集火。

### 3.3 开火

选中目标后派发 `BULLET_BUILD` → `BattleBulletComponent.BuildNewBullet`：

- 子弹配置：`towerId * 100 + curLevel + 1`（`tbbullet.json`）
- 出生点：`tower.birthPosition`（格子世界坐标）
- 移动组件：由塔表 `BulletMoveType` 决定（见第 4 节）

---

## 4. 子弹移动与弹道类型

配置：`Tower.xlsx` → `BulletMoveType`（Luban 枚举 `cfg.BulletMoveType`）。

| 枚举 | 组件 | 行为 |
|------|------|------|
| `Homing` | `UnitMoveComponent_Bullet` | 追踪弹：对 **怪物** 每帧重算朝向；速度标量不变 |
| `Straight` | `UnitMoveComponent_Bullet_One` | 直线弹：开火时锁定方向，之后不改向 |
| `None` | 同 Homing | 工厂层归一化为 Homing |

工厂：`BulletMoveComponentFactory.CreateFromBirthParam`。

其它配置（与弹道无关）：

- `IsRemove`（`tbbullet.json`）：`0` 命中销毁，`1` 穿透（如 4 号塔）
- `BodyRadius`：写入 `birthParam["bodyRadius"]`，**逻辑碰撞半径**（勿写死 0.2）

### 4.1 命中职责划分（移动 vs HitTest）

| 弹道 | 目标 | 移动阶段 | HitTest |
|------|------|----------|---------|
| **Straight** | 怪物 / 物品 | 只位移，**不**调用 `TryResolveTargetHit` | **唯一**伤害入口 |
| **Homing** | 怪物 | 改向 + 步长限制，**不**移动内判定 | **唯一**伤害入口 |
| **Homing** | 物品 | 不改向 + 步长限制 + **`TryResolveTargetHit`** | 同帧可能重叠，靠 `haveBeHit` 去重 |

`TryResolveTargetHit`（仅追踪弹 + 物品）：与绑定目标距离 ≤ 两半径之和时触发双向 `BeHitCallBack`；非穿透弹命中后本帧不再位移。

直线弹 **不做** 步长压缩（`ShouldLimitStepTowardTarget` 仅追踪弹为 true）。

实现开关：

- `UsesMoveResolveTargetHit()` — 直线弹 `Bullet_One` 覆写为 `false`
- `ShouldTryResolveTargetHitNow()` — 追踪弹且目标为 **物品** 时为 `true`

### 4.2 追踪弹步长限制

仅 **Homing** 且目标仍有效时：本帧位移不超过「到碰撞圆边缘剩余距离」，防止穿过静止目标；靠近时可能 **视觉变慢**（标量不变、步长变短）。

---

## 5. 碰撞（HitTest）

### 5.1 流程

1. `RefreshSpatialGrid()`
2. `ChooseSingleBeHit(MONSTER, BULLET)` / `ChooseSingleBeHit(ITEM, BULLET)` — 以怪物/物品为 receiver，查附近子弹
3. `ExeTheCallBack` — 对每个 receiver：`bullet.BeHitCallBack(receiver)` + `receiver.BeHitCallBack(bullet)`

### 5.2 与移动内判定的关系

- **HitTest**：所有弹道对路径上怪物/物品的 **主通路**（含穿透多目标）。
- **TryResolveTargetHit**：仅 **Homing + 物品** 的补充分通路（物品静止、追踪弹不对物品每帧改向，靠抵达判定提高命中率）。
- 同帧两通路均触发时，靠 `haveBeHit`（`bullet.uid`）防重复扣血。

非穿透弹销毁：由 `BattleUnit_Bullet.BeHitCallBack` → `RequestRemove`（两通路任一触发即可）。

---

## 6. 目标丢失

`BattleBulletComponent.UpdateBullInfo`：非塔单位 `BATTLE_UNIT_REMOVE` 时，对所有在飞子弹 `RemoveMoveDirect`（清除绑定目标引用， **不改** 已锁定的 `moveSpeedX/Y`）。

---

## 7. 关键文件

| 主题 | 文件 |
|------|------|
| 组件注册与 Tick 顺序 | `Battle/Pve/PveBattleComponentSetup.cs` |
| 集火 + 碰撞 | `Components/HitTest/BattleSimpleHitTestComponent.cs` |
| 空间网格 | `Components/HitTest/BattleSpatialGrid.cs` |
| 塔建造与 Tick | `Components/Core/BattleTowerComponent.cs` |
| 塔索敌 | `Unit/BattleUnit_Tower.cs` |
| 子弹生命周期 | `Components/Core/BattleBulletComponent.cs` |
| 子弹移动 | `Unit/Components/Move/UnitMoveComponent_Bullet*.cs` |
| 弹道工厂 | `Unit/Components/Move/BulletMoveComponentFactory.cs` |
| 塔弹道配置 | `ConfigTools/Config/Datas/Tower.xlsx` → `BulletMoveType` |

---

## 8. 改顺序或加新碰撞类型时

1. 保持 **子弹移动在 HitTest 之前**（除非改为连续碰撞扫描 CCD）。
2. 凡用 `BattleSpatialGrid` 做 broad phase，须在当次查询前 **Refresh**；小集合可像集火一样 brute-force。
3. 新增 receiver 类型时：注册 `registerHitTestShapeDic` + `curShouldCallBackDic`，并实现 `ShouldReceiveHit`。
4. 新增弹道类型：扩展 `BulletMoveType` 枚举 + `BulletMoveComponentFactory` + 新 `UnitMoveComponent` 子类。
