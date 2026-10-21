--[[
    framework/lua_view_host.lua —— C# LuaView 的调用入口

    本文件直接定义全局 LuaViewHost（普通表，不是 class）：C# 侧通过 LuaManager
    调用这里的函数，避免 C# 直接索引实例方法（依赖 metatable，容易踩坑）。

        LuaViewHost.EnsureView(moduleName, cfg) require 模块并 New(cfg) 出面板实例
                                                （cfg 由 C# 传入：name/layer/bundle/asset）
        LuaViewHost.Invoke(view, method, ...)   调用面板实例方法，失败只记日志不抛给 C#
]]

LuaViewHost = {}

function LuaViewHost.EnsureView(moduleName, cfg)
    if type(moduleName) ~= 'string' or moduleName == '' then
        log.Error('[LuaViewHost] 模块名为空')
        return nil
    end

    local ok, mod = pcall(require, moduleName)
    if not ok then
        log.Error('[LuaViewHost] require 失败: %s (%s)', tostring(moduleName), tostring(mod))
        return nil
    end

    if type(mod) ~= 'table' or type(mod.New) ~= 'function' then
        log.Error('[LuaViewHost] 模块不是可 New 的类: %s', tostring(moduleName))
        return nil
    end

    -- 子类不覆盖 New：构造配置（name/layer/bundle/asset）由 C# 通过 cfg 传入，
    -- BaseView.__init(cfg) 消费；子类只在 __init 里初始化自己的字段。
    local created, view = pcall(mod.New, cfg)
    if not created then
        log.Error('[LuaViewHost] New 失败: %s (%s)', tostring(moduleName), tostring(view))
        return nil
    end

    return view
end

function LuaViewHost.Invoke(view, method, ...)
    if view == nil then
        log.Warn('[LuaViewHost] view 为空，忽略调用: %s', tostring(method))
        return nil
    end

    local fn = view[method]
    if type(fn) ~= 'function' then
        log.Warn('[LuaViewHost] 方法不存在: %s', tostring(method))
        return nil
    end

    local ok, ret = pcall(fn, view, ...)
    if not ok then
        log.Error('[LuaViewHost] %s 调用失败: %s', tostring(method), tostring(ret))
        return nil
    end

    return ret
end

return LuaViewHost
