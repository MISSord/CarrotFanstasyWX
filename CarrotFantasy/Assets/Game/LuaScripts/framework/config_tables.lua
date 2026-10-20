--[[
    Luban 表包装（map / list / one）。由 ConfigManager 按需调用，不要在启动时 require 全部数据。
]]

local DATA_PREFIX = 'gen.luban_data.'

local function _split_index(index)
    local fields = {}
    if type(index) ~= 'string' or index == '' then
        return fields
    end
    for part in string.gmatch(index, '[^+]+') do
        fields[#fields + 1] = part
    end
    return fields
end

local function _compose_key(fields, row)
    if #fields <= 1 then
        return row[fields[1]]
    end
    local parts = {}
    for i = 1, #fields do
        parts[i] = tostring(row[fields[i]])
    end
    return table.concat(parts, '\0')
end

local function _compose_key_from_args(fields, ...)
    local n = select('#', ...)
    if n <= 1 and #fields <= 1 then
        return (...)
    end
    local args = { ... }
    if #fields <= 1 then
        return args[1]
    end
    local parts = {}
    for i = 1, #fields do
        parts[i] = tostring(args[i])
    end
    return table.concat(parts, '\0')
end

local function _strip_lua(file)
    local name = file
    if type(name) == 'string' and name:sub(-4) == '.lua' then
        return name:sub(1, -5)
    end
    return name
end

local function LoadData(file)
    local name = _strip_lua(file)
    local mod = DATA_PREFIX .. name
    local ok, data = pcall(require, mod)
    if ok then
        return data, mod
    end

    ok, data = pcall(require, name)
    if ok then
        return data, name
    end

    error('配置数据加载失败: ' .. tostring(mod) .. ' / ' .. tostring(name))
end

local function WrapTable(def, raw)
    if def.mode == 'one' then
        return raw
    end

    local fields = _split_index(def.index)
    local list
    local map

    if def.mode == 'list' then
        list = raw
        map = {}
        for i = 1, #list do
            local row = list[i]
            map[_compose_key(fields, row)] = row
        end
    else
        map = raw
        list = {}
        local minK, maxK, count, allInt = nil, nil, 0, true
        for k, _ in pairs(raw) do
            count = count + 1
            if type(k) ~= 'number' or k ~= math.floor(k) then
                allInt = false
                break
            end
            if minK == nil or k < minK then
                minK = k
            end
            if maxK == nil or k > maxK then
                maxK = k
            end
        end
        if allInt and minK ~= nil and maxK - minK + 1 == count then
            for k = minK, maxK do
                list[#list + 1] = raw[k]
            end
        else
            for _, row in pairs(raw) do
                list[#list + 1] = row
            end
        end
    end

    local wrapper = {
        DataMap = map,
        DataList = list,
    }

    function wrapper:GetOrDefault(...)
        return map[_compose_key_from_args(fields, ...)]
    end

    function wrapper:Get(...)
        local v = map[_compose_key_from_args(fields, ...)]
        if v == nil and def.mode == 'map' then
            error(string.format('%s.Get missing key', def.name))
        end
        return v
    end

    if def.mode == 'map' then
        setmetatable(wrapper, { __index = map })
    end

    return wrapper
end

return {
    LoadData = LoadData,
    WrapTable = WrapTable,
    StripLua = _strip_lua,
}
