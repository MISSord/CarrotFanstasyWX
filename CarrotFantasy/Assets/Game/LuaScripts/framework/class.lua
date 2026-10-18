--[[
    framework/class.lua —— 极简 OO（单继承）

    用法：
        local class = require 'framework.class'
        local BaseView = require 'framework.view_base'
        local HelpPanel = class('HelpPanel', BaseView)

        function HelpPanel:ctor()
            BaseView.ctor(self, { name = 'HelpPanel' })
        end

    约定：
      - 子类构造里必须显式调用 `父类.ctor(self, ...)`；
      - 子类同名方法自动覆盖父类；
      - 需要调父类实现时用 `BaseView.xxx(self, ...)`。
]]

local function class(name, super)
    local cls = {
        __cname = name,   -- 类名，便于日志与调试
        __super = super,  -- 父类，便于 isA 判断
    }

    cls.__index = cls

    if super then
        setmetatable(cls, { __index = super })
    end

    function cls.new(...)
        local obj = setmetatable({}, cls)
        if obj.ctor then
            obj:ctor(...)
        end
        return obj
    end

    -- 类型判断：obj:isA(SomeClass)
    function cls.isA(target, targetClass)
        local c = getmetatable(target)
        while c do
            if c == targetClass then
                return true
            end
            c = c.__super
        end
        return false
    end

    return cls
end

return class
