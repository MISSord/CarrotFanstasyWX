--[[
    仅供 LuaLS / Cursor 解析全局符号与 F12 跳转；运行时由 module_controller 写入 _G。
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

---@class ModuleController
---@field LoadFiles fun()
---@field NewCtrls fun()
ModuleController = {}
