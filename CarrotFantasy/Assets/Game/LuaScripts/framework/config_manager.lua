--[[
    framework/config_manager.lua —— Luban 按需加载（Ctrl 单例，全局 ConfigManager）

    启动只 New 管理器，不读表数据。传入表名后再 require 对应 gen/luban_data。
    schema.lua 仅在第一次取表/枚举时加载（只有结构，没有行数据）。

    用法：
        local items = ConfigManager:Get('TbItem')
        local row = items:Get(1)
        ConfigManager:Get('TbRoguelikeLevel'):Get(1, 2)
        ConfigManager:Get('TbMapLevel'):Get(1, 2)
        ConfigManager:Get('TbMapBigLevel'):Get(1)
        ConfigManager:GetEnum('BuffType').Stun
        ConfigManager:Unload('TbItem')   -- 释放单表，下次 Get 再加载
]]

local TableUtil = require 'framework.config_tables'

local SCHEMA_MODULE = 'gen.luban.schema'

local ConfigManager = class()

function ConfigManager:__init()
    self._schema = nil
    self._defs_by_name = nil
    self._defs_by_file = nil
    self._cache = {}
    self._modules = {}
end

function ConfigManager:__fini()
    self:UnloadAll()
    self._schema = nil
    self._defs_by_name = nil
    self._defs_by_file = nil
end

function ConfigManager:_ensure_schema()
    if self._schema then
        return self._schema
    end

    local ok, schema = pcall(require, SCHEMA_MODULE)
    if not ok then
        error('ConfigManager: 缺少 ' .. SCHEMA_MODULE .. '，请先运行 ConfigTools/gen_code_json.bat')
    end

    self._schema = schema
    self._defs_by_name = {}
    self._defs_by_file = {}

    local tables = schema.tables or {}
    for i = 1, #tables do
        local def = tables[i]
        self._defs_by_name[def.name] = def
        local file = TableUtil.StripLua(def.file)
        if file then
            self._defs_by_file[file] = def
            self._defs_by_file[string.lower(file)] = def
        end
    end

    return schema
end

function ConfigManager:_find_def(name)
    self:_ensure_schema()
    if type(name) ~= 'string' or name == '' then
        error('ConfigManager: 配置名不能为空')
    end

    local def = self._defs_by_name[name]
        or self._defs_by_file[name]
        or self._defs_by_file[string.lower(name)]
        or self._defs_by_file[TableUtil.StripLua(name)]

    if not def then
        error('ConfigManager: 未知配置 ' .. name)
    end
    return def
end

--- 只加载这一张表；已加载则直接返回缓存。
function ConfigManager:Load(name)
    local def = self:_find_def(name)
    local cached = self._cache[def.name]
    if cached ~= nil then
        return cached
    end

    local raw, mod = TableUtil.LoadData(def.file)
    local wrapped = TableUtil.WrapTable(def, raw)
    self._cache[def.name] = wrapped
    self._modules[def.name] = mod
    return wrapped
end

--- 按需加载并返回表。
function ConfigManager:Get(name)
    return self:Load(name)
end

function ConfigManager:Has(name)
    self:_ensure_schema()
    local def = self._defs_by_name[name]
        or self._defs_by_file[name]
        or self._defs_by_file[string.lower(tostring(name or ''))]
        or self._defs_by_file[TableUtil.StripLua(name)]
    if def == nil then
        return false
    end
    return self._cache[def.name] ~= nil
end

function ConfigManager:GetEnums()
    return self:_ensure_schema().enums or {}
end

function ConfigManager:GetEnum(name)
    return self:GetEnums()[name]
end

function ConfigManager:Unload(name)
    if self._defs_by_name == nil then
        return
    end

    local def = self._defs_by_name[name]
        or self._defs_by_file[name]
        or self._defs_by_file[string.lower(tostring(name or ''))]
        or self._defs_by_file[TableUtil.StripLua(name)]
    if def == nil then
        return
    end

    local mod = self._modules[def.name]
    if mod and package.loaded then
        package.loaded[mod] = nil
    end
    self._cache[def.name] = nil
    self._modules[def.name] = nil
end

function ConfigManager:UnloadAll()
    if package.loaded then
        for _, mod in pairs(self._modules) do
            package.loaded[mod] = nil
        end
        package.loaded[SCHEMA_MODULE] = nil
    end
    self._cache = {}
    self._modules = {}
    self._schema = nil
    self._defs_by_name = nil
    self._defs_by_file = nil
end

return ConfigManager
