-- gm_demo.lua
-- ============================================================
-- GM 演示模块：用来证明「改 Lua → 重打 lua_scripts 包 → 热更生效」
-- 这个文件会被打进 AB 包 lua_scripts，运行时由 LuaManager 预载。
--
-- 游戏内 GM 面板/控制台输入：
--   /gm lua require('gm.demo').ping()
--   /gm lua require('gm.demo').hello('小明')
--   /gm lua require('gm.demo').coins(500)
--   /gm lua reload            <-- 重新从 AB 拉脚本（改完脚本不用重启）
-- ============================================================

local M = {}

-- 改这个数字 + 重新打包 lua_scripts，再 /gm lua reload，就能看到版本号变了，
-- 这一条就是「代码热更生效」的最小证据。
local VERSION = 1

function M.version()
    return VERSION
end

function M.ping()
    return 'pong, gm.demo v' .. VERSION
end

function M.hello(name)
    return '来自 Lua 的问候：' .. tostring(name or 'GM') .. '（gm.demo v' .. VERSION .. '）'
end

-- 演示：Lua 侧拼业务文案 / 数值处理，C# 侧只负责调用。
function M.coins(n)
    n = tonumber(n) or 100
    if n < 0 then
        return '金币数不能为负：' .. n
    end
    return '（模拟）已发放金币 ' .. n
end

-- 演示 Lua 能反过来调用 C#：打印一条日志。
function M.log(msg)
    CS.UnityEngine.Debug.Log('[Lua] ' .. tostring(msg))
    return '已输出到 Console'
end

return M
