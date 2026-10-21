--[[
    panels/set_panel.lua —— 设置面板（Lua 版，替换 C# SetPanel 的交互流程）

    分工：
      · 本文件：按钮绑定、页面切换、音效开关的点击流程；
      · C#（LuaSetPanelView）：分辨率/显示模式枚举与持久化、AudioManager 开关、
        按钮图集刷新、多语言文本 —— 经 self.host 暴露成委托。

    host 方法（C# 注入，调用前判空）：
      host.ToggleMusic()          切换音乐开关并刷新按钮图
      host.ToggleEffect()         切换音效开关并刷新按钮图
      host.IsDisplaySupported()   当前平台是否支持显示设置
      host.MoveResolution(delta)  切换分辨率草稿（delta = ±1）
      host.MoveMode(delta)        切换显示模式草稿（delta = ±1）
      host.ApplyDisplay()         应用并持久化显示设置

    注意：**不要覆盖 New**。构造配置由 C# 的 LuaView 通过 cfg 传入，
    BaseView.__init(cfg) 消费；本文件只实现 __init 与生命周期方法。
]]

local SetPanel = class(BaseView)

local PAGE_OPTION = 1
local PAGE_PRODUCE = 2

function SetPanel:__init(_cfg)
    self.pageId = PAGE_OPTION
    self._clicks = {}
end

function SetPanel:OnLoad()
    self._clicks[#self._clicks + 1] = self:AddClick('Btn_BGAudio', function()
        self:_toggle_music()
    end)

    self._clicks[#self._clicks + 1] = self:AddClick('Btn_EffectAudio', function()
        self:_toggle_effect()
    end)

    self._clicks[#self._clicks + 1] = self:AddClick('Btn_Option', function()
        self:_show_page(PAGE_OPTION)
    end)

    self._clicks[#self._clicks + 1] = self:AddClick('Btn_Producer', function()
        self:_show_page(PAGE_PRODUCE)
    end)

    self:_bind_display_settings()

    log.Debug('SetPanel 已加载（Lua 版）')
end

function SetPanel:OnOpen(index)
    self:_show_page(PAGE_OPTION)
end

function SetPanel:OnUnload()
    for i = 1, #self._clicks do
        local handle = self._clicks[i]
        if handle ~= nil and self.bridge ~= nil then
            self.bridge:RemoveButtonClick(handle)
        end
    end

    self._clicks = {}
    log.Debug('SetPanel 已卸载')
end

-- ---------------------------------------------------------------- 内部

function SetPanel:_toggle_music()
    if self.host == nil then
        return
    end

    self.host.ToggleMusic()
end

function SetPanel:_toggle_effect()
    if self.host == nil then
        return
    end

    self.host.ToggleEffect()
end

function SetPanel:_show_page(pageId)
    if self.pageId == pageId then
        return
    end

    self.pageId = pageId
    -- 页面切换的具体表现暂与 C# 版保持一致（空实现）。
    -- 以后要做位移/淡入时改这里即可热更生效。
end

function SetPanel:_bind_display_settings()
    if self.host == nil then
        return
    end

    if not self.host.IsDisplaySupported() then
        log.Debug('SetPanel: 当前平台不支持显示设置，跳过绑定')
        return
    end

    if self:Node('node_DisplaySettings') == nil then
        return
    end

    self._clicks[#self._clicks + 1] = self:AddClick('Btn_ResolutionPrev', function()
        self.host.MoveResolution(-1)
    end)

    self._clicks[#self._clicks + 1] = self:AddClick('Btn_ResolutionNext', function()
        self.host.MoveResolution(1)
    end)

    self._clicks[#self._clicks + 1] = self:AddClick('Btn_DisplayModePrev', function()
        self.host.MoveMode(-1)
    end)

    self._clicks[#self._clicks + 1] = self:AddClick('Btn_DisplayModeNext', function()
        self.host.MoveMode(1)
    end)

    self._clicks[#self._clicks + 1] = self:AddClick('Btn_ApplyDisplay', function()
        self.host.ApplyDisplay()
    end)
end

return SetPanel
