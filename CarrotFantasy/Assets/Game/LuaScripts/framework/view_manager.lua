--[[
    framework/view_manager.lua —— C# ViewManager 的 Lua 对应物

    迁移依据（对照 Assets/Scripts/Server/UI/ViewManager.cs）：
        保留在 C# / 由 C# 壳执行的原子操作：
            Instantiate / Destroy prefab、Canvas.sortingOrder、UIRoot/UICamera 获取
        搬到 Lua 的纯逻辑：
            注册表、按模块名创建、层级排序算法、视图栈暂停/恢复、批量关闭

    排序规则与原 C# 一致：
        baseOrder = layer * LAYER_INTERVAL + BASE_ORDER
        order     = baseOrder + indexInLayer * VIEW_INTERVAL

    用法：
        local vm = ViewManager.new()
        vm:register(require 'panels.help_panel')
        vm:open('panel.help', 0)
        vm:update()          -- 每帧调用，脏标记时才重排
        vm:unloadAll()       -- 热更前调用，逐个 onUnload
]]

local log = require 'framework.logger'

local ViewManager = {}
ViewManager.__index = ViewManager

local LAYER_MIN, LAYER_MAX = 1, 4
local LAYER_INTERVAL = 1500
local VIEW_INTERVAL = 100
local BASE_ORDER = 10000
local MAX_VIEW_PER_LAYER = 15

function ViewManager.new()
    return setmetatable({
        byName = {},      -- viewName  -> view
        byModule = {},    -- moduleName -> view
        layers = {},      -- layer -> { view, ... }（按打开顺序）
        needFlush = false,
    }, ViewManager)
end

-- ---------------------------------------------------------------- 注册

--- 注册一个 view 实例
function ViewManager:register(view)
    if view == nil or view.viewName == nil then
        log.error('register failed: view 或 view.viewName 为空')
        return false
    end

    if self.byName[view.viewName] ~= nil then
        log.error('view 已注册: %s', tostring(view.viewName))
        return false
    end

    self.byName[view.viewName] = view
    log.debug('view 注册: %s', tostring(view.viewName))
    return true
end

--- 按模块名懒加载并注册；模块约定：返回一个可从 new() 构造的类
function ViewManager:ensureRegistered(moduleName)
    local existing = self.byModule[moduleName]
    if existing ~= nil then
        return existing
    end

    local ok, mod = pcall(require, moduleName)
    if not ok then
        log.error('require 模块失败: %s (%s)', tostring(moduleName), tostring(mod))
        return nil
    end

    if type(mod) ~= 'table' or mod.new == nil then
        log.error('模块 %s 未返回可 new() 的类', tostring(moduleName))
        return nil
    end

    local view = mod.new()
    if not self:register(view) then
        return nil
    end

    self.byModule[moduleName] = view
    return view
end

function ViewManager:getView(name)
    return self.byName[name]
end

-- ---------------------------------------------------------------- 打开 / 关闭

function ViewManager:open(name, index)
    local view = self.byModule[name] or self.byName[name]
    if view == nil then
        view = self:ensureRegistered(name)
    end

    if view == nil then
        log.error('OpenView 失败，未找到: %s', tostring(name))
        return nil
    end

    view:open(index or 0)

    local layer = view.layer or 1
    local list = self.layers[layer]
    if not list then
        list = {}
        self.layers[layer] = list
    end

    for i = 1, #list do
        if list[i] == view then
            self.needFlush = true
            self:_refreshStackPause()
            return view
        end
    end

    list[#list + 1] = view
    if #list >= MAX_VIEW_PER_LAYER then
        log.error('UILayer=%s 上打开的 View 已达 %d 个，检查是否未 Close', tostring(layer), #list)
    end

    self.needFlush = true
    self:_refreshStackPause()
    return view
end

function ViewManager:close(name)
    local view = self.byModule[name] or self.byName[name]
    if view == nil then
        return
    end
    view:close()
end

--- 由 C# 壳在 Close 完成后回调，把 view 移出打开列表
function ViewManager:onViewClosed(view)
    if view == nil then
        return
    end

    local list = self.layers[view.layer or 1]
    if list then
        for i = #list, 1, -1 do
            if list[i] == view then
                table.remove(list, i)
            end
        end
    end

    self.needFlush = true
    self:_refreshStackPause()
end

--- 关闭所有打开的 view（按叠放顺序自顶向下）
function ViewManager:closeAll()
    local ordered = self:_collectOrdered()
    for i = #ordered, 1, -1 do
        local ok, err = pcall(ordered[i].close, ordered[i])
        if not ok then
            log.error('closeAll 时关闭 %s 失败: %s', tostring(ordered[i].viewName), tostring(err))
        end
    end
end

-- ---------------------------------------------------------------- 排序与栈状态

function ViewManager:update()
    if not self.needFlush then
        return
    end

    for layer = LAYER_MIN, LAYER_MAX do
        local list = self.layers[layer]
        if list then
            local base = layer * LAYER_INTERVAL + BASE_ORDER
            for i = 1, #list do
                local view = list[i]
                local order = base + (i - 1) * VIEW_INTERVAL
                if view.bridge ~= nil then
                    view.bridge:SetCanvasOrder(order)
                end
            end
        end
    end

    self.needFlush = false
end

--- 收集所有打开的 view，按 层级升序 + 层内打开顺序 排列；最后一个是栈顶
function ViewManager:_collectOrdered()
    local ordered = {}
    for layer = LAYER_MIN, LAYER_MAX do
        local list = self.layers[layer]
        if list then
            for i = 1, #list do
                ordered[#ordered + 1] = list[i]
            end
        end
    end
    return ordered
end

--- 栈顶 onResume，其余 onPause
function ViewManager:_refreshStackPause()
    local ordered = self:_collectOrdered()
    if #ordered == 0 then
        return
    end

    local top = ordered[#ordered]
    for i = 1, #ordered do
        local view = ordered[i]
        local ok, err = pcall(view.applyPauseState, view, view == top)
        if not ok then
            log.error('%s applyPauseState 失败: %s', tostring(view.viewName), tostring(err))
        end
    end
end

-- ---------------------------------------------------------------- 热更卸载

--- 热更前调用：逐个卸载并清空注册表（由 C# LuaManager 在 reload 时触发）
function ViewManager:unloadAll()
    local count = 0
    for name, view in pairs(self.byName) do
        if view.unload then
            local ok, err = pcall(view.unload, view)
            if not ok then
                log.error('卸载 %s 失败: %s', tostring(name), tostring(err))
            else
                count = count + 1
            end
        end
    end

    self.byName = {}
    self.byModule = {}
    self.layers = {}
    self.needFlush = false

    log.info('ViewManager 已卸载 %d 个 view', count)
    return count
end

return ViewManager
