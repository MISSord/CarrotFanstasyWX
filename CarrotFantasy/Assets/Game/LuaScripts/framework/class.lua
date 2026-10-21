--[[
    单继承 OOP：只认 __init / __fini；父类先构造、子类先析构。
    本文件直接定义全局 class()；module_controller 只负责按顺序 require。

    相对网上常见写法（lua-users SimpleLuaClasses / PIL 16.1）的取舍：
      · 用 __index 链继承，不 pairs 拷贝父类字段（父类后改的方法子类立刻可见）
      · setmetatable(cls, super)，类表本身当实例的 metatable，不再多包一层 { __index = super }
      · 隐藏 Object 基类，Destroy 只定义一次；New 必须绑在每个类上，否则 Child.New 会落到父类闭包
      · 不提供 isA / 多继承 / __call(Dog())；对外构造入口只有 Class.New(...)
]]

local Object = {}
Object.__index = Object

local function _init_object(cls, obj, ...)
    if cls.__super then
        _init_object(cls.__super, obj, ...)
    end
    local init = rawget(cls, '__init')
    if init then
        init(obj, ...)
    end
end

--- 子类 __fini 失败仍继续父类，避免只清一半；返回最先发生的错误
local function _fini_object(cls, obj)
    local fini = rawget(cls, '__fini')
    local err
    if fini then
        local ok, e = pcall(fini, obj)
        if not ok then
            err = e
        end
    end
    if cls.__super then
        err = err or _fini_object(cls.__super, obj)
    end
    return err
end

function Object:Destroy()
    if rawget(self, '__destroyed') then
        return
    end
    rawset(self, '__destroyed', true)

    local cls = getmetatable(self)
    if type(cls) ~= 'table' then
        return
    end

    local err = _fini_object(cls, self)
    if err then
        error('[class] __fini failed: ' .. tostring(err), 0)
    end
end

function class(super)
    if super ~= nil and type(super) ~= 'table' then
        error('class: super must be a class table', 2)
    end
    super = super or Object

    local cls = { __super = super }
    cls.__index = cls
    setmetatable(cls, super)

    -- 必须写在 cls 自己身上：点号调用 New 时拿不到 class，只能靠闭包抓住 cls
    function cls.New(...)
        local obj = setmetatable({}, cls)
        _init_object(cls, obj, ...)
        return obj
    end

    return cls
end

return class