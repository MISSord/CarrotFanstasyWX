--[[
    framework/module_controller.lua —— 全局类装载与 Ctrl 单例入口（LuaManager require）

    约定：framework / services 下的类在**自己文件里直接声明为全局**
    （class、log、Event、BaseView、ViewManager、ConfigManager、MapCtrl），
    这里只按依赖顺序 require 一遍，不再做 `_G[item.global] = mod` 这类映射。

    加新类：把模块路径写进 FILE_LIST（顺序即依赖顺序）。
    加 Ctrl：FILE_LIST 里先 require，再写进 CTRL_LIST，NewCtrls 按表顺序 New 一次。
]]

local FILE_LIST = {
    'framework.class',
    'framework.logger',
    'framework.event',
    'framework.view_base',
    'framework.view_manager',
    'framework.config_manager',
    'services.map_service',
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
    -- 类在自己的文件里已是全局，这里只需按顺序把文件执行一遍
    for i = 1, #FILE_LIST do
        require(FILE_LIST[i])
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
