--[[
    panels/help_panel.lua —— 首个业务面板迁移样例

    原 C# 实现：Assets/Scripts/Business/Hud/HelpPanel.cs（73 行）
    迁移内容：
        · 三个页面节点（HelpPage / MonsterPage / TowerPage）的显隐切换
        · 四个按钮的点击绑定
        · 关闭按钮
    仍留在 C# 的部分：
        · prefab 加载（ui/view/helppanel_prefab）
        · Instantiate / Canvas 排序 / 销毁
        · 按钮 UnityAction 的平台绑定（由 bridge:AddButtonClick 封装）

    UI 节点名必须与 prefab 上 UINameTable 的条目一致，否则 self:node() 会打警告返回 nil。
]]

local class = require 'framework.class'
local BaseView = require 'framework.view_base'
local log = require 'framework.logger'

local HelpPanel = class('HelpPanel', BaseView)

-- 与 Assets/Scripts/Util/Panel/UiViewAbPaths.cs 的 HelpPrefab 保持一致
local BUNDLE = 'ui/view/helppanel_prefab'
local ASSET = 'HelpPanel'

local PAGE_HELP = 1
local PAGE_MONSTER = 2
local PAGE_TOWER = 3

function HelpPanel:ctor()
    BaseView.ctor(self, {
        name = 'HelpPanel',
        layer = 1,     -- UILayer.Normal
        bundle = BUNDLE,
        asset = ASSET,
    })

    self.showId = PAGE_HELP
    self.nodeHelp = nil
    self.nodeMonster = nil
    self.nodeTower = nil
    self._clicks = {}
end

-- ---------------------------------------------------------------- 生命周期

function HelpPanel:onLoad()
    -- UI 引用（对应原 C# LoadCallBack 里的 nameTableDic 取值）
    self.nodeHelp = self:node('HelpPage')
    self.nodeMonster = self:node('MonsterPage')
    self.nodeTower = self:node('TowerPage')

    -- 按钮绑定：返回的句柄留给 onUnload 解绑
    self._clicks[#self._clicks + 1] = self:addClick('Btn_Return', function()
        self:close()
    end)
    self._clicks[#self._clicks + 1] = self:addClick('Btn_Help', function()
        self:showPage(PAGE_HELP)
    end)
    self._clicks[#self._clicks + 1] = self:addClick('Btn_Monster', function()
        self:showPage(PAGE_MONSTER)
    end)
    self._clicks[#self._clicks + 1] = self:addClick('Btn_Tower', function()
        self:showPage(PAGE_TOWER)
    end)

    self.showId = PAGE_HELP
    self:updateNodePosition()

    log.debug('HelpPanel 已加载')
end

function HelpPanel:onOpen(index)
    -- 每次打开都回到帮助页（与 C# 行为一致：LoadCallBack 里重置 showId）
    self.showId = PAGE_HELP
    self:updateNodePosition()
end

function HelpPanel:onClose()
    log.debug('HelpPanel 关闭')
end

--- 热更/销毁时必须解绑，否则旧闭包仍会响应点击
function HelpPanel:onUnload()
    for i = 1, #self._clicks do
        local handle = self._clicks[i]
        if handle ~= nil and self.bridge ~= nil then
            self.bridge:RemoveButtonClick(handle)
        end
    end
    self._clicks = {}
    log.debug('HelpPanel 已卸载')
end

-- ---------------------------------------------------------------- 页面切换

function HelpPanel:showPage(pageId)
    if self.showId == pageId then
        return
    end
    self.showId = pageId
    self:updateNodePosition()
end

local SHOW_OFFSET = { x = 0, y = 0, z = 0 }
local FADE_OFFSET = { x = 0, y = 3000, z = 0 }

--- 只有当前页归位，其余移到屏幕外（与原 C# 用 localPosition 位移的实现一致）
function HelpPanel:updateNodePosition()
    local nodes = {
        [PAGE_HELP] = self.nodeHelp,
        [PAGE_MONSTER] = self.nodeMonster,
        [PAGE_TOWER] = self.nodeTower,
    }

    for pageId, node in pairs(nodes) do
        if node ~= nil then
            local offset = (pageId == self.showId) and SHOW_OFFSET or FADE_OFFSET
            local t = node.transform
            local ok, err = pcall(function()
                t.localPosition = CS.UnityEngine.Vector3(offset.x, offset.y, offset.z)
            end)
            if not ok then
                log.error('HelpPanel 设置 localPosition 失败(page=%s): %s', tostring(pageId), tostring(err))
            end
        end
    end
end

return HelpPanel
