--[[
    services/map_service.lua —— 服务层迁移的**模式样例**（不是完整移植）

    ⚠️ 迁移边界（重要）：
        Assets/Scripts/Business/Map/MapServer.cs 本身是「装配壳」：
            · 创建 MapModel / eventDispatcher
            · 注册 protobuf socket 监听（GetUserMapResponse / SetSingleMapResponse）
            · 创建并注册两个面板
        这些都属于 C# 职责，**不应搬到 Lua**。

        真正该迁到 Lua 的是 MapModel 里的**业务规则**：
            · ParseMapInfo（地图快照解析与状态推进）
            · 解锁规则（unlockTo / setCell 的合法性判断）
            · 进度快照的生成与脏标记

    因此本文件只做两件事：
        1. 演示「Lua 持有业务规则 + 网络收发委托给 C#」的写法；
        2. 把已确认的规则（RememberLastBattleLevel 的入参校验）先落下来，
           其余标注 TODO，等读完 MapModel.cs 再补，避免写出与现有行为不一致的实现。

    依赖的 C# 桥（待实现）：
        bridge:SendSetSingleMapInfo(json)    -- 交给 C# 的 SimpleBinaryOpcodes 发包
        bridge:GetInitMapInfo()              -- 对应 MapInfoHelper.GetInitMapInfo()
]]

local log = require 'framework.logger'
local Event = require 'framework.event'

local MapService = {}

function MapService.new(bridge)
    local self = setmetatable({}, { __index = MapService })
    self.bridge = bridge
    self.eventBus = Event.new()

    -- 对应 C# MapServer.RememberLastBattleLevel 的两个只读属性
    self.lastEnteredBigLevelId = 0
    self.lastEnteredLevelId = 0

    -- 地图状态（TODO：等 MapModel 迁移后替换为结构化数据）
    self.mapSnapshot = nil
    self.isDirty = false

    return self
end

-- ---------------------------------------------------------------- 已确认规则

--- 对应 C# MapServer.RememberLastBattleLevel：任一参数 <= 0 直接忽略
function MapService:rememberLastBattleLevel(bigLevelId, levelId)
    if bigLevelId == nil or levelId == nil then
        return
    end

    if bigLevelId <= 0 or levelId <= 0 then
        return
    end

    self.lastEnteredBigLevelId = bigLevelId
    self.lastEnteredLevelId = levelId
    self.isDirty = true
end

-- ---------------------------------------------------------------- 待迁移（读 MapModel.cs 后补）

--- 对应 C# MapServer.ApplyMapSnapshot：
---   空串时回退到 MapInfoHelper.GetInitMapInfo()，再交给 MapModel.ParseMapInfo
function MapService:applyMapSnapshot(snapshot)
    if snapshot == nil or snapshot == '' then
        if self.bridge ~= nil then
            snapshot = self.bridge:GetInitMapInfo()
        end
    end

    self.mapSnapshot = snapshot

    -- TODO: 解析规则迁自 MapModel.ParseMapInfo，需要先确认：
    --   1) 快照的字段结构（大关/小关/星星/解锁位）
    --   2) 解析失败时的降级行为（是否回退初始地图）
    --   3) 与面板的事件通知时序（解析完立即广播，还是等下一次 Tick）
    log.info('MapService.applyMapSnapshot: 已缓存快照，解析逻辑待迁移')
    self.isDirty = true
end

--- 对应 C# MapServer.SendSetSingleMapInfo：业务规则在 Lua，发包在 C#
function MapService:sendSetSingleMapInfo(payload)
    if self.bridge == nil then
        log.error('sendSetSingleMapInfo 失败：bridge 为空')
        return
    end

    -- TODO: payload 的结构需要与 SingleMapInfo 对齐；序列化交给 C#（protobuf 生成的 C# 代码）
    self.bridge:SendSetSingleMapInfo(payload)
end

--- 对应 C# MapServer.ClearPendingProgressOnSessionExpired：丢弃未同步进度
function MapService:clearPendingProgressOnSessionExpired()
    self.isDirty = false

    -- TODO: 迁自 MapModel 的重置实现（重置到初始地图，注意不要清 lastEntered* 字段）
    log.info('MapService.clearPendingProgressOnSessionExpired: 重置逻辑待迁移')
end

-- ---------------------------------------------------------------- 热更卸载

--- 由 LuaManager 在 reload 前调用
function MapService:onUnload()
    if self.eventBus ~= nil then
        self.eventBus:clear()
    end
    self.bridge = nil
    log.debug('MapService 已卸载')
end

return MapService
