--[[
    framework/event.lua —— 事件总线（本文件直接定义全局 Event）

    用法：
        local bus = Event.New()
        bus:On('battle_win', function(levelId) ... end, self)
        bus:Emit('battle_win', 3)
        bus:UnbindAll(self)
]]

local unpack = table.unpack or unpack

Event = {}
Event.__index = Event

function Event.New()
    return setmetatable({
        handlers = {},
        owners = {},
    }, Event)
end

function Event:On(name, fn, owner)
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

function Event:Once(name, fn, owner)
    local wrapper
    wrapper = function(...)
        self:Off(name, wrapper)
        return fn(...)
    end
    return self:On(name, wrapper, owner)
end

function Event:Off(name, fn)
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

function Event:OffAll(name)
    self.handlers[name] = nil
end

function Event:UnbindAll(owner)
    if owner == nil then
        return 0
    end

    local owned = self.owners[owner]
    if not owned then
        return 0
    end

    local count = #owned
    for i = 1, count do
        self:Off(owned[i].name, owned[i].fn)
    end

    self.owners[owner] = nil
    return count
end

function Event:Emit(name, ...)
    local list = self.handlers[name]
    if not list or #list == 0 then
        return
    end

    local snapshot = { unpack(list) }
    for i = 1, #snapshot do
        local handler = snapshot[i]
        local ok, err = pcall(handler.fn, ...)
        if not ok then
            log.Error('event "%s" handler error: %s', name, tostring(err))
        end
    end
end

function Event:Clear()
    self.handlers = {}
    self.owners = {}
end

return Event
