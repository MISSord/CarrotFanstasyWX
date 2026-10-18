--[[
    framework/event.lua —— 事件总线（带 owner 归属，为热更卸载服务）

    为什么需要 owner：
      Lua 热更时如果只清 package.loaded，旧模块注册的回调仍被 C# 侧或事件总线持有，
      会同时响应 → 重复扣血、日志翻倍、内存泄漏。
      注册时带上 owner（通常是模块表或 view 实例），热更前调用 event:unbindAll(owner)
      一次性摘干净。

    用法：
        local Event = require 'framework.event'
        local bus = Event.new()

        bus:on('battle_win', function(levelId) ... end, self)   -- self 作为 owner
        bus:emit('battle_win', 3)
        bus:unbindAll(self)                                     -- 热更/销毁时
]]

local log = require 'framework.logger'

local unpack = table.unpack or unpack

local Event = {}
Event.__index = Event

function Event.new()
    return setmetatable({
        handlers = {},   -- name -> { {fn=..., owner=...}, ... }
        owners = {},     -- owner -> { {name=..., fn=...}, ... }
    }, Event)
end

function Event:on(name, fn, owner)
    assert(type(name) == 'string', 'event name must be string')
    assert(type(fn) == 'function', 'event handler must be function')

    local list = self.handlers[name]
    if not list then
        list = {}
        self.handlers[name] = list
    end

    list[#list + 1] = { fn = fn, owner = owner }

    if owner ~= nil then
        local owned = self.owners[owner]
        if not owned then
            owned = {}
            self.owners[owner] = owned
        end
        owned[#owned + 1] = { name = name, fn = fn }
    end

    return fn
end

function Event:once(name, fn, owner)
    local wrapper
    wrapper = function(...)
        self:off(name, wrapper)
        return fn(...)
    end
    return self:on(name, wrapper, owner)
end

function Event:off(name, fn)
    local list = self.handlers[name]
    if not list then
        return
    end

    for i = #list, 1, -1 do
        if list[i].fn == fn then
            table.remove(list, i)
        end
    end

    if #list == 0 then
        self.handlers[name] = nil
    end
end

function Event:offAll(name)
    self.handlers[name] = nil
end

--- 解绑某个 owner 注册过的所有回调（热更/销毁的关键入口）
function Event:unbindAll(owner)
    if owner == nil then
        return 0
    end

    local owned = self.owners[owner]
    if not owned then
        return 0
    end

    local count = #owned
    for i = 1, count do
        self:off(owned[i].name, owned[i].fn)
    end

    self.owners[owner] = nil
    return count
end

function Event:emit(name, ...)
    local list = self.handlers[name]
    if not list or #list == 0 then
        return
    end

    -- 快照：回调里可能 off/on，直接遍历原表会错乱
    local snapshot = { unpack(list) }
    for i = 1, #snapshot do
        local handler = snapshot[i]
        local ok, err = pcall(handler.fn, ...)
        if not ok then
            log.error('event "%s" handler error: %s', name, tostring(err))
        end
    end
end

function Event:clear()
    self.handlers = {}
    self.owners = {}
end

return Event
