--[[
    framework/logger.lua —— 统一日志出口（全局 log，由 module_controller 注入）
]]

local M = {}

local LEVELS = { debug = 1, info = 2, warn = 3, error = 4 }
M.LEVELS = LEVELS
M.level = LEVELS.debug

local function _safe_format(fmt, ...)
    if select('#', ...) == 0 then
        return tostring(fmt)
    end
    local ok, s = pcall(string.format, fmt, ...)
    return ok and s or tostring(fmt)
end

local function _has_unity()
    return CS ~= nil and CS.UnityEngine ~= nil and CS.UnityEngine.Debug ~= nil
end

local function _output(level, fmt, ...)
    if LEVELS[level] < M.level then
        return
    end

    local line = string.format('[Lua][%s] %s', level:upper(), _safe_format(fmt, ...))
    if not _has_unity() then
        print(line)
        return
    end

    if level == 'error' then
        CS.UnityEngine.Debug.LogError(line)
    elseif level == 'warn' then
        CS.UnityEngine.Debug.LogWarning(line)
    else
        CS.UnityEngine.Debug.Log(line)
    end
end

function M.Debug(fmt, ...) _output('debug', fmt, ...) end
function M.Info(fmt, ...) _output('info', fmt, ...) end
function M.Warn(fmt, ...) _output('warn', fmt, ...) end
function M.Error(fmt, ...) _output('error', fmt, ...) end

function M.SetLevel(name)
    local lv = LEVELS[name]
    if lv then
        M.level = lv
    end
end

return M
