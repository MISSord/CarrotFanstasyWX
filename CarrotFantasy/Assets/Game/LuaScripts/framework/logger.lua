--[[
    framework/logger.lua —— 统一日志出口（本文件直接定义全局 log）
]]

log = {}

local LEVELS = { debug = 1, info = 2, warn = 3, error = 4 }
log.LEVELS = LEVELS
log.level = LEVELS.debug

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
    if LEVELS[level] < log.level then
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

function log.Debug(fmt, ...) _output('debug', fmt, ...) end
function log.Info(fmt, ...) _output('info', fmt, ...) end
function log.Warn(fmt, ...) _output('warn', fmt, ...) end
function log.Error(fmt, ...) _output('error', fmt, ...) end

function log.SetLevel(name)
    local lv = LEVELS[name]
    if lv then
        log.level = lv
    end
end

return log
