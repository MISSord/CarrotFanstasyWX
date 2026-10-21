--[[
    仅供 LuaLS / Cursor 解析全局符号与 F12 跳转。
    framework / services 下的模块会在自己文件里直接声明全局
    （class / log / Event / BaseView / ViewManager / ConfigManager / MapCtrl），
    本文件只给编辑器提供类型入口，运行时不会被 require。
    勿 require 本文件；工作区通过 .luarc.json 的 workspace.library 加载。
]]

---@meta

class = require 'framework.class'
log = require 'framework.logger'
Event = require 'framework.event'
BaseView = require 'framework.view_base'
ViewManager = require 'framework.view_manager'
MapCtrl = require 'services.map_service'
ConfigManager = require 'framework.config_manager'

---@class LuaViewHost
---@field EnsureView fun(moduleName: string): table
---@field Invoke fun(view: table, method: string, ...): any
LuaViewHost = {}

---@class ModuleController
---@field LoadFiles fun()
---@field NewCtrls fun()
ModuleController = {}
