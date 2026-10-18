--[[
    framework/view_base.lua —— C# BaseView 的 Lua 对应物（生命周期契约）

    ⚠️ 重要约束：Unity 的 prefab 上只能挂 C# MonoBehaviour，不能挂 Lua。
    因此 C# 侧保留一个通用薄壳 LuaPanel : BaseView，它负责：
        · SetUILoadInfo(bundle, asset) 与 prefab 加载（ViewLoader）
        · Instantiate / Destroy / Canvas sortingOrder 等 Unity 原子操作
        · 把生命周期转发给本文件的 Lua 实例
    本文件负责"逻辑"：状态、UI 引用、事件、业务回调。

    ===== C# LuaPanel 需要调用的 Lua 方法（桥契约 v1）=====
        view:bindBridge(bridge)              -- 传入 C# 桥对象
        view:handleLoaded(root, uiTable)     -- prefab 加载完成；root=GameObject, ui=UINameTableDic
        view:onOpenCallback(index)           -- 每次 Open/切 index
        view:handleFlushed(index, info)      -- 刷新回调
        view:applyPauseState(isTopmost)      -- 视图栈暂停/恢复
        view:handleClosed()                  -- Close
        view:handleReleased()                -- 延迟 Release
        view:onUnload()                      -- 热更卸载（解绑事件、清状态）

    ===== C# 桥需要提供给 Lua 的能力（bridge）=====
        bridge:LoadPrefab()                  -- 触发加载（异步，完成后回调 handleLoaded）
        bridge:Close()                        -- 请求关闭（走 BaseView.Close）
        bridge:SetCanvasOrder(order)          -- 设置排序
        bridge:AddButtonClick(gameObject, luaFn)   -- 绑定按钮（内部包 UnityAction）
        bridge:RemoveAllButtonClick(gameObject)    -- 解绑
]]

local class = require 'framework.class'
local log = require 'framework.logger'

local BaseView = class('BaseView')

--- cfg = { name = 'HelpPanel', layer = 1, bundle = 'ui/view/helppanel_prefab', asset = 'HelpPanel' }
function BaseView:ctor(cfg)
    cfg = cfg or {}
    self.viewName = cfg.name
    self.layer = cfg.layer or 1
    self.bundle = cfg.bundle
    self.asset = cfg.asset

    self.bridge = nil      -- C# LuaPanel
    self.root = nil        -- GameObject
    self.ui = nil          -- UINameTableDic
    self.isOpen = false
    self.isLoaded = false
    self._closed = false
end

-- ===================== 子类可覆盖的生命周期 =====================

--- 数据初始化（对应 C# InitData，此时还没有 prefab）
function BaseView:onInitData() end

--- prefab 加载完成：这里做 UI 引用抓取与按钮绑定
function BaseView:onLoad() end

--- 打开 / 切换 index
function BaseView:onOpen(index) end

--- 刷新（对应 C# OnFlush）
function BaseView:onFlush(index, info) end

--- 视图栈：被覆盖时暂停
function BaseView:onPause() end

--- 视图栈：回到栈顶时恢复
function BaseView:onResume() end

--- 关闭
function BaseView:onClose() end

--- 延迟释放（真正销毁 prefab 前）
function BaseView:onRelease() end

--- 热更卸载：必须解绑所有事件/定时器，否则热更后旧闭包仍在响应
function BaseView:onUnload() end

-- ===================== 由 C# LuaPanel 驱动 =====================

function BaseView:bindBridge(bridge)
    self.bridge = bridge
end

function BaseView:handleLoaded(root, uiTable)
    self.root = root
    self.ui = uiTable
    self.isLoaded = true

    local ok, err = pcall(self.onLoad, self)
    if not ok then
        log.error('[%s] onLoad failed: %s', tostring(self.viewName), tostring(err))
    end
end

function BaseView:onOpenCallback(index)
    self.isOpen = true
    local ok, err = pcall(self.onOpen, self, index or 0)
    if not ok then
        log.error('[%s] onOpen failed: %s', tostring(self.viewName), tostring(err))
    end
end

function BaseView:handleFlushed(index, info)
    local ok, err = pcall(self.onFlush, self, index or 0, info)
    if not ok then
        log.error('[%s] onFlush failed: %s', tostring(self.viewName), tostring(err))
    end
end

function BaseView:applyPauseState(isTopmost)
    if isTopmost then
        self:onResume()
    else
        self:onPause()
    end
end

function BaseView:handleClosed()
    self.isOpen = false
    local ok, err = pcall(self.onClose, self)
    if not ok then
        log.error('[%s] onClose failed: %s', tostring(self.viewName), tostring(err))
    end
end

function BaseView:handleReleased()
    local ok, err = pcall(self.onRelease, self)
    if not ok then
        log.error('[%s] onRelease failed: %s', tostring(self.viewName), tostring(err))
    end

    self.isLoaded = false
    self.root = nil
    self.ui = nil
end

-- ===================== 供子类使用的便捷方法 =====================

--- 从 nameTable 取节点，拿不到返回 nil（避免直接索引报错）
function BaseView:node(uiName)
    if self.ui == nil then
        return nil
    end

    local ok, go = pcall(function()
        return self.ui:GetGameObjectSafely(uiName)
    end)

    if ok and go ~= nil then
        return go
    end

    -- 兼容：索引器形式
    local ok2, go2 = pcall(function()
        return self.ui[uiName]
    end)
    if ok2 and go2 ~= nil then
        return go2
    end

    log.warn('[%s] UI node not found: %s', tostring(self.viewName), tostring(uiName))
    return nil
end

--- 绑定按钮点击；返回可用于解绑的句柄
function BaseView:addClick(uiName, fn)
    local go = self:node(uiName)
    if go == nil then
        return nil
    end

    if self.bridge == nil then
        log.error('[%s] addClick 失败：bridge 为空（C# LuaPanel 未注入）', tostring(self.viewName))
        return nil
    end

    return self.bridge:AddButtonClick(go, fn)
end

--- 请求打开自己（由 C# 壳执行实际加载与 Open 流程）
function BaseView:open(index)
    self._closed = false

    if self.bridge == nil then
        log.warn('[%s] open 请求被忽略：bridge 为空（C# LuaPanel 未注入）', tostring(self.viewName))
        return
    end

    self.bridge:Open(index or 0)
end

--- 请求关闭自己（由 C# 壳执行实际关闭流程）
function BaseView:close()
    if self._closed then
        return
    end
    self._closed = true

    if self.bridge ~= nil then
        self.bridge:Close()
    else
        log.warn('[%s] close 请求被忽略：bridge 为空', tostring(self.viewName))
    end
end

--- 热更卸载：子类覆盖 onUnload 后由 ViewManager 统一调用
function BaseView:unload()
    local ok, err = pcall(self.onUnload, self)
    if not ok then
        log.error('[%s] onUnload failed: %s', tostring(self.viewName), tostring(err))
    end
    self._closed = true
end

return BaseView
