--[[
    framework/module_controller.lua —— 全局模块与 Ctrl 单例入口（LuaManager require）

    加全局类：写进 FILE_LIST（path + global）。
    加 Ctrl：FILE_LIST 里先 require，再写进 CTRL_LIST，NewCtrls 按该表顺序 New 一次。
]]

local FILE_LIST = {
    { path = 'framework.class',        global = 'class' },
    { path = 'framework.logger',       global = 'log' },
    { path = 'framework.event',        global = 'Event' },
    { path = 'framework.view_base',    global = 'BaseView' },
    { path = 'framework.view_manager', global = 'ViewManager' },
    { path = 'services.map_service',   global = 'MapCtrl' },
    { path = 'framework.config_manager', global = 'ConfigManager' },
}

-- 顺序即依赖顺序：先 ViewManager，再业务 Ctrl
local CTRL_LIST = {
    'ViewManager',
    'MapCtrl',
    'ConfigManager',
}

local ModuleController = {}

local function _is_class(t)
    return type(t) == 'table' and rawget(t, '__index') == t and type(t.New) == 'function'
end

local function _destroy_ctrls()
    for i = #CTRL_LIST, 1, -1 do
        local name = CTRL_LIST[i]
        local obj = rawget(_G, name)
        if type(obj) == 'table' and not _is_class(obj) and type(obj.Destroy) == 'function' then
            obj:Destroy()
        end
    end
end

function ModuleController.LoadFiles()
    for i = 1, #FILE_LIST do
        local item = FILE_LIST[i]
        local mod = require(item.path)
        if item.global then
            _G[item.global] = mod
        end
    end
end

function ModuleController.NewCtrls()
    for i = 1, #CTRL_LIST do
        local name = CTRL_LIST[i]
        local cls = _G[name]
        if type(cls) ~= 'table' or type(cls.New) ~= 'function' then
            error('ModuleController.NewCtrls: ' .. tostring(name) .. ' 不是可 New 的类，先 LoadFiles')
        end

        if not rawget(cls, '__singleton_bound') then
            local orig_new = cls.New
            function cls.New(...)
                if cls.__inst then
                    return cls.__inst
                end
                cls.__inst = orig_new(...)
                return cls.__inst
            end
            cls.__singleton_bound = true
        end

        _G[name] = cls.New()
    end
end

_destroy_ctrls()
ModuleController.LoadFiles()
ModuleController.NewCtrls()

_G.ModuleController = ModuleController
return ModuleController
