--[[
    framework/logger.lua —— 统一日志出口

    设计要点：
      1. 走 C# 的 Debug，才能在 Unity Console 里看到并用 Unity 的过滤/堆栈；
      2. 没有 Unity 环境时（纯 lua 工具链）退回 print，方便单元测试；
      3. 格式化用 pcall 包一层，避免调用方写错 %s 导致日志本身抛异常。
]]

local M = {}

local LEVELS = { debug = 1, info = 2, warn = 3, error = 4 }
M.LEVELS = LEVELS
M.level = LEVELS.debug

local function safeFormat(fmt, ...)
    if select('#', ...) == 0 then
        return tostring(fmt)
    end
    local ok, s = pcall(string.format, fmt, ...)
    return ok and s or tostring(fmt)
end

local function hasUnity()
    return CS ~= nil and CS.UnityEngine ~= nil and CS.UnityEngine.Debug ~= nil
end

local function output(level, fmt, ...)
    if LEVELS[level] < M.level then
        return
    end

    local line = string.format('[Lua][%s] %s', level:upper(), safeFormat(fmt, ...))
    if not hasUnity() then
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

function M.debug(fmt, ...) output('debug', fmt, ...) end
function M.info(fmt, ...) output('info', fmt, ...) end
function M.warn(fmt, ...) output('warn', fmt, ...) end
function M.error(fmt, ...) output('error', fmt, ...) end

--- 设置日志级别：'debug' | 'info' | 'warn' | 'error'
function M.setLevel(name)
    local lv = LEVELS[name]
    if lv then
        M.level = lv
    end
end

return M
