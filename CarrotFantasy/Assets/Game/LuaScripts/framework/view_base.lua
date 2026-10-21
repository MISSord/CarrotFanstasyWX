--[[
    framework/view_base.lua —— C# BaseView 的 Lua 对应物（本文件直接定义全局 BaseView）

    ===== C# LuaPanel 桥契约（PascalCase）=====
        view:BindBridge(bridge)
        view:HandleLoaded(root, uiTable)
        view:OnOpenCallback(index)
        view:HandleFlushed(index, info)
        view:ApplyPauseState(isTopmost)
        view:HandleClosed()
        view:HandleReleased()
        view:OnUnload()          -- 子类覆盖；热更解绑

    ===== 子类可覆盖的生命周期（PascalCase）=====
        OnInitData / OnLoad / OnOpen / OnFlush / OnPause / OnResume / OnClose / OnRelease
]]

BaseView = class()

function BaseView:__init(cfg)
    cfg = cfg or {}
    self.viewName = cfg.name
    self.layer = cfg.layer or 1
    self.bundle = cfg.bundle
    self.asset = cfg.asset

    self.bridge = nil
    self.root = nil
    self.ui = nil
    self.isOpen = false
    self.isLoaded = false
    self._closed = false
end

function BaseView:OnInitData() end
function BaseView:OnLoad() end
function BaseView:OnOpen(index) end
function BaseView:OnFlush(index, info) end
function BaseView:OnPause() end
function BaseView:OnResume() end
function BaseView:OnClose() end
function BaseView:OnRelease() end
function BaseView:OnUnload() end

function BaseView:BindBridge(bridge)
    self.bridge = bridge
end

function BaseView:HandleLoaded(root, uiTable)
    self.root = root
    self.ui = uiTable
    self.isLoaded = true

    local ok, err = pcall(self.OnLoad, self)
    if not ok then
        log.Error('[%s] OnLoad failed: %s', tostring(self.viewName), tostring(err))
    end
end

function BaseView:OnOpenCallback(index)
    self.isOpen = true
    local ok, err = pcall(self.OnOpen, self, index or 0)
    if not ok then
        log.Error('[%s] OnOpen failed: %s', tostring(self.viewName), tostring(err))
    end
end

function BaseView:HandleFlushed(index, info)
    local ok, err = pcall(self.OnFlush, self, index or 0, info)
    if not ok then
        log.Error('[%s] OnFlush failed: %s', tostring(self.viewName), tostring(err))
    end
end

function BaseView:ApplyPauseState(isTopmost)
    if isTopmost then
        self:OnResume()
    else
        self:OnPause()
    end
end

function BaseView:HandleClosed()
    self.isOpen = false
    local ok, err = pcall(self.OnClose, self)
    if not ok then
        log.Error('[%s] OnClose failed: %s', tostring(self.viewName), tostring(err))
    end
end

function BaseView:HandleReleased()
    local ok, err = pcall(self.OnRelease, self)
    if not ok then
        log.Error('[%s] OnRelease failed: %s', tostring(self.viewName), tostring(err))
    end

    self.isLoaded = false
    self.root = nil
    self.ui = nil
end

function BaseView:Node(uiName)
    if self.ui == nil then
        return nil
    end

    local ok, go = pcall(function()
        return self.ui:GetGameObjectSafely(uiName)
    end)

    if ok and go ~= nil then
        return go
    end

    local ok2, go2 = pcall(function()
        return self.ui[uiName]
    end)
    if ok2 and go2 ~= nil then
        return go2
    end

    log.Warn('[%s] UI node not found: %s', tostring(self.viewName), tostring(uiName))
    return nil
end

function BaseView:AddClick(uiName, fn)
    local go = self:Node(uiName)
    if go == nil then
        return nil
    end

    if self.bridge == nil then
        log.Error('[%s] AddClick 失败：bridge 为空（C# LuaPanel 未注入）', tostring(self.viewName))
        return nil
    end

    return self.bridge:AddButtonClick(go, fn)
end

function BaseView:Open(index)
    self._closed = false

    if self.bridge == nil then
        log.Warn('[%s] Open 请求被忽略：bridge 为空（C# LuaPanel 未注入）', tostring(self.viewName))
        return
    end

    self.bridge:Open(index or 0)
end

function BaseView:Close()
    if self._closed then
        return
    end
    self._closed = true

    if self.bridge ~= nil then
        self.bridge:Close()
    else
        log.Warn('[%s] Close 请求被忽略：bridge 为空', tostring(self.viewName))
    end
end

function BaseView:Unload()
    local ok, err = pcall(self.OnUnload, self)
    if not ok then
        log.Error('[%s] OnUnload failed: %s', tostring(self.viewName), tostring(err))
    end
    self._closed = true
end

return BaseView
