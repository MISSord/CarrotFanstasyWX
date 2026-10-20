--[[
    panels/help_panel.lua —— HelpPanel 样例（全局 class / BaseView / log）
]]

local HelpPanel = class(BaseView)

local BUNDLE = 'ui/view/helppanel_prefab'
local ASSET = 'HelpPanel'

local _create = HelpPanel.New

function HelpPanel.New()
    return _create({
        name = 'HelpPanel',
        layer = 1,
        bundle = BUNDLE,
        asset = ASSET,
    })
end

local PAGE_HELP = 1
local PAGE_MONSTER = 2
local PAGE_TOWER = 3

function HelpPanel:__init(_cfg)
    self.showId = PAGE_HELP
    self.nodeHelp = nil
    self.nodeMonster = nil
    self.nodeTower = nil
    self._clicks = {}
end

function HelpPanel:OnLoad()
    self.nodeHelp = self:Node('HelpPage')
    self.nodeMonster = self:Node('MonsterPage')
    self.nodeTower = self:Node('TowerPage')

    self._clicks[#self._clicks + 1] = self:AddClick('Btn_Return', function()
        self:Close()
    end)
    self._clicks[#self._clicks + 1] = self:AddClick('Btn_Help', function()
        self:_show_page(PAGE_HELP)
    end)
    self._clicks[#self._clicks + 1] = self:AddClick('Btn_Monster', function()
        self:_show_page(PAGE_MONSTER)
    end)
    self._clicks[#self._clicks + 1] = self:AddClick('Btn_Tower', function()
        self:_show_page(PAGE_TOWER)
    end)

    self.showId = PAGE_HELP
    self:_update_node_position()

    log.Debug('HelpPanel 已加载')
end

function HelpPanel:OnOpen(index)
    self.showId = PAGE_HELP
    self:_update_node_position()
end

function HelpPanel:OnClose()
    log.Debug('HelpPanel 关闭')
end

function HelpPanel:OnUnload()
    for i = 1, #self._clicks do
        local handle = self._clicks[i]
        if handle ~= nil and self.bridge ~= nil then
            self.bridge:RemoveButtonClick(handle)
        end
    end
    self._clicks = {}
    log.Debug('HelpPanel 已卸载')
end

function HelpPanel:_show_page(pageId)
    if self.showId == pageId then
        return
    end
    self.showId = pageId
    self:_update_node_position()
end

local SHOW_OFFSET = { x = 0, y = 0, z = 0 }
local FADE_OFFSET = { x = 0, y = 3000, z = 0 }

function HelpPanel:_update_node_position()
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
                log.Error('HelpPanel 设置 localPosition 失败(page=%s): %s', tostring(pageId), tostring(err))
            end
        end
    end
end

return HelpPanel
