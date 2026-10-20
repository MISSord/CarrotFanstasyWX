--[[
    framework/view_manager.lua —— 视图注册与栈管理（Ctrl 单例，module_controller 注入全局 ViewManager）

    用法：
        ViewManager:Register(require 'panels.help_panel')
        ViewManager:Open('panel.help', 0)
        ViewManager:Update()
        ViewManager:UnloadAll()
]]

local ViewManager = class()

local LAYER_MIN, LAYER_MAX = 1, 4
local LAYER_INTERVAL = 1500
local VIEW_INTERVAL = 100
local BASE_ORDER = 10000
local MAX_VIEW_PER_LAYER = 15

function ViewManager:__init()
    self.byName = {}
    self.byModule = {}
    self.layers = {}
    self.needFlush = false
end

function ViewManager:__fini()
    self:UnloadAll()
end

function ViewManager:Register(view)
    if view == nil or view.viewName == nil then
        log.Error('Register failed: view 或 view.viewName 为空')
        return false
    end

    if self.byName[view.viewName] ~= nil then
        log.Error('view 已注册: %s', tostring(view.viewName))
        return false
    end

    self.byName[view.viewName] = view
    log.Debug('view 注册: %s', tostring(view.viewName))
    return true
end

function ViewManager:EnsureRegistered(moduleName)
    local existing = self.byModule[moduleName]
    if existing ~= nil then
        return existing
    end

    local ok, mod = pcall(require, moduleName)
    if not ok then
        log.Error('require 模块失败: %s (%s)', tostring(moduleName), tostring(mod))
        return nil
    end

    if type(mod) ~= 'table' or mod.New == nil then
        log.Error('模块 %s 未返回可 New() 的类', tostring(moduleName))
        return nil
    end

    local view = mod.New()
    if not self:Register(view) then
        return nil
    end

    self.byModule[moduleName] = view
    return view
end

function ViewManager:GetView(name)
    return self.byName[name]
end

function ViewManager:Open(name, index)
    local view = self.byModule[name] or self.byName[name]
    if view == nil then
        view = self:EnsureRegistered(name)
    end

    if view == nil then
        log.Error('OpenView 失败，未找到: %s', tostring(name))
        return nil
    end

    view:Open(index or 0)

    local layer = view.layer or 1
    local list = self.layers[layer]
    if not list then
        list = {}
        self.layers[layer] = list
    end

    for i = 1, #list do
        if list[i] == view then
            self.needFlush = true
            self:_refresh_stack_pause()
            return view
        end
    end

    list[#list + 1] = view
    if #list >= MAX_VIEW_PER_LAYER then
        log.Error('UILayer=%s 上打开的 View 已达 %d 个，检查是否未 Close', tostring(layer), #list)
    end

    self.needFlush = true
    self:_refresh_stack_pause()
    return view
end

function ViewManager:Close(name)
    local view = self.byModule[name] or self.byName[name]
    if view == nil then
        return
    end
    view:Close()
end

function ViewManager:OnViewClosed(view)
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
    self:_refresh_stack_pause()
end

function ViewManager:CloseAll()
    local ordered = self:_collect_ordered()
    for i = #ordered, 1, -1 do
        local ok, err = pcall(ordered[i].Close, ordered[i])
        if not ok then
            log.Error('CloseAll 时关闭 %s 失败: %s', tostring(ordered[i].viewName), tostring(err))
        end
    end
end

function ViewManager:Update()
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

function ViewManager:_collect_ordered()
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

function ViewManager:_refresh_stack_pause()
    local ordered = self:_collect_ordered()
    if #ordered == 0 then
        return
    end

    local top = ordered[#ordered]
    for i = 1, #ordered do
        local view = ordered[i]
        local ok, err = pcall(view.ApplyPauseState, view, view == top)
        if not ok then
            log.Error('%s ApplyPauseState 失败: %s', tostring(view.viewName), tostring(err))
        end
    end
end

function ViewManager:UnloadAll()
    local count = 0
    for name, view in pairs(self.byName) do
        if view.Unload then
            local ok, err = pcall(view.Unload, view)
            if not ok then
                log.Error('卸载 %s 失败: %s', tostring(name), tostring(err))
            else
                count = count + 1
            end
        end
    end

    self.byName = {}
    self.byModule = {}
    self.layers = {}
    self.needFlush = false

    log.Info('ViewManager 已卸载 %d 个 view', count)
    return count
end

return ViewManager
