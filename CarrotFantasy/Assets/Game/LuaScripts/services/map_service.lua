--[[
    services/map_service.lua —— MapCtrl 单例（由 module_controller NewCtrls 注入全局 MapCtrl）
]]

local MapCtrl = class()

function MapCtrl:__init()
    self.bridge = nil
    self.eventBus = Event.New()

    self.lastEnteredBigLevelId = 0
    self.lastEnteredLevelId = 0
    self.mapSnapshot = nil
    self.isDirty = false
end

function MapCtrl:__fini()
    if self.eventBus ~= nil then
        self.eventBus:Clear()
    end
    self.bridge = nil
end

function MapCtrl:SetBridge(bridge)
    self.bridge = bridge
end

function MapCtrl:RememberLastBattleLevel(bigLevelId, levelId)
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

function MapCtrl:ApplyMapSnapshot(snapshot)
    if snapshot == nil or snapshot == '' then
        if self.bridge ~= nil then
            snapshot = self.bridge:GetInitMapInfo()
        end
    end

    self.mapSnapshot = snapshot
    log.Info('MapCtrl.ApplyMapSnapshot: 已缓存快照，解析逻辑待迁移')
    self.isDirty = true
end

function MapCtrl:SendSetSingleMapInfo(payload)
    if self.bridge == nil then
        log.Error('SendSetSingleMapInfo 失败：bridge 为空')
        return
    end

    self.bridge:SendSetSingleMapInfo(payload)
end

function MapCtrl:ClearPendingProgressOnSessionExpired()
    self.isDirty = false
    log.Info('MapCtrl.ClearPendingProgressOnSessionExpired: 重置逻辑待迁移')
end

return MapCtrl
