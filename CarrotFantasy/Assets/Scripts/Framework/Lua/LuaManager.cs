using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using XLua;

namespace CarrotFantasy
{
    /// <summary>
    /// Lua 环境管理。
    /// <para>
    /// 两种运行模式，模块命名空间不同但各自自洽：
    /// </para>
    /// <para>
    /// <b>① 编辑器 Development / Debug（直读工程源码）</b><br/>
    ///   模块名 = 相对路径（<c>framework.class</c>），文件 = <c>Assets/Game/LuaScripts/framework/class.lua</c>。
    ///   源码里的 require 保持原样，改完 .lua 立刻生效，不需要打包。
    /// </para>
    /// <para>
    /// <b>② 打包后 Production（读解包目录）</b><br/>
    ///   打包时已把 require 扁平化（<c>framework.view_manager</c> → <c>view_manager</c>），
    ///   运行时先从 AB 取 <c>lua_bundle</c>，按 buildId 解包成散文件到 persistentDataPath，
    ///   之后 <c>require</c> 只读单个几 KB 的文件，避免全量加载的 IO 尖峰与常驻内存。
    /// </para>
    /// </summary>
    public static class LuaManager
    {
        /// <summary>Lua Bundle 所在 AB 包名。</summary>
        public const string ScriptBundleName = LuaBundleReader.BundleName;

        /// <summary>包内唯一资产名。</summary>
        public const string BundleAssetName = LuaBundleReader.AssetName;

        /// <summary>编辑器直读模式下的源码目录（相对 Assets）。</summary>
        public const string ProjectSourceRelative = "Game/LuaScripts";

        /// <summary>加载超时，避免 AB 异常时把启动流程卡死。</summary>
        private const float LoadTimeoutSeconds = 5f;

        /// <summary>加载全局模块并 New Ctrl 单例；热更重载后需再次执行。</summary>
        private const string BootstrapChunk =
            "local ok, err = pcall(require, 'framework.module_controller') if not ok then error(err) end";

        /// <summary>当前可用的模块名集合（仅名字，不存源码）。</summary>
        private static readonly HashSet<string> Modules = new HashSet<string>(StringComparer.Ordinal);

        private static readonly List<string> ModuleList = new List<string>();

        private static LuaEnv s_env;
        private static bool s_initializing;
        private static string s_error;
        private static bool s_useProjectSource;
        private static string s_projectSourceDir;

        /// <summary>初始化时传入的宿主，用于 GM 触发脚本重载（需要跑协程）。</summary>
        private static MonoBehaviour s_host;

        public static bool IsReady
        {
            get { return s_env != null; }
        }

        /// <summary>当前 LuaEnv（未初始化时为 null）。仅供框架层（LuaView 等）使用。</summary>
        public static LuaEnv Env
        {
            get { return s_env; }
        }

        /// <summary>新建一个 Lua 表，由 C# 填好数据后传给 Lua（节点表、flush info 等）。</summary>
        public static LuaTable NewTable()
        {
            return s_env != null ? s_env.NewTable() : null;
        }

        /// <summary>require 一个 Lua 面板模块并 New 出实例（模块需返回可 New 的类）。</summary>
        public static LuaTable EnsureLuaView(string moduleName)
        {
            if (s_env == null || string.IsNullOrEmpty(moduleName))
            {
                return null;
            }

            try
            {
                object[] results = s_env.DoString(
                    "return LuaViewHost.EnsureView('" + moduleName + "')",
                    "lua_view_ensure");
                if (results != null && results.Length > 0)
                {
                    return results[0] as LuaTable;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[LuaManager] EnsureLuaView 失败: " + moduleName + " / " + e.Message);
            }

            return null;
        }

        /// <summary>调用 Lua 视图实例上的方法；方法不存在只告警，异常在 Lua 侧 pcall 兜住。</summary>
        public static void CallLuaView(LuaTable view, string method, params object[] args)
        {
            if (s_env == null || view == null || string.IsNullOrEmpty(method))
            {
                return;
            }

            object[] callArgs = new object[(args != null ? args.Length : 0) + 2];
            callArgs[0] = view;
            callArgs[1] = method;
            if (args != null)
            {
                Array.Copy(args, 0, callArgs, 2, args.Length);
            }

            InvokeLuaFunction("LuaViewHost", "Invoke", callArgs);
        }

        /// <summary>调用全局表里的普通函数（不自动传 self）。</summary>
        public static object CallLuaFunc(string tableName, string funcName, params object[] args)
        {
            return InvokeLuaFunction(tableName, funcName, args);
        }

        private static object InvokeLuaFunction(string tableName, string funcName, object[] args)
        {
            if (s_env == null || string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(funcName))
            {
                return null;
            }

            LuaTable table = s_env.Global.Get<LuaTable>(tableName);
            if (table == null)
            {
                Debug.LogWarning("[LuaManager] 全局表不存在: " + tableName);
                return null;
            }

            try
            {
                LuaFunction fn = table.Get<LuaFunction>(funcName);
                if (fn == null)
                {
                    Debug.LogWarning("[LuaManager] 函数不存在: " + tableName + "." + funcName);
                    return null;
                }

                try
                {
                    return fn.Call(args ?? new object[0]);
                }
                finally
                {
                    fn.Dispose();
                }
            }
            finally
            {
                table.Dispose();
            }
        }

        public static string LastError
        {
            get { return s_error; }
        }

        public static int ModuleCount
        {
            get { return ModuleList.Count; }
        }

        /// <summary>编辑器 Development / Debug 模式直接读工程源码。</summary>
        public static bool UseProjectSource
        {
            get { return s_useProjectSource; }
        }

        /// <summary>
        /// 初始化 Lua 环境。失败不会中断启动流程（Lua 只影响 GM / 热更逻辑，不应阻断进游戏）。
        /// </summary>
        public static IEnumerator InitializeRoutine(MonoBehaviour host)
        {
            if (s_env != null || s_initializing)
            {
                yield break;
            }

            s_host = host;
            s_initializing = true;
            s_error = null;
            s_useProjectSource = DetectProjectSourceMode();

            yield return LoadModulesRoutine();

            if (ModuleList.Count == 0)
            {
                s_error = string.IsNullOrEmpty(s_error) ? "未加载到任何 Lua 模块" : s_error;
                Debug.LogWarning("[LuaManager] Lua 不可用：" + s_error);
                s_initializing = false;
                yield break;
            }

            try
            {
                s_env = new LuaEnv();
                s_env.AddLoader(CustomLoader);

                // 预热内置库，尽早暴露 xLua Resources 缺失这类问题。
                s_env.DoString("local ok, err = pcall(require, 'xlua.util') if not ok then error(err) end", "lua_bootstrap");
                s_env.DoString(BootstrapChunk, "lua_module_controller");

                Debug.Log("[LuaManager] Lua 环境就绪，模块 " + ModuleList.Count + " 个，模式="
                          + (s_useProjectSource ? "编辑器直读源码" : "解包散文件"));
            }
            catch (Exception e)
            {
                s_error = e.Message;
                Debug.LogError("[LuaManager] 创建 LuaEnv 失败: " + e);
                s_env = null;
            }
            finally
            {
                s_initializing = false;
            }
        }

        /// <summary>每帧调用，回收 LuaTable / LuaFunction 等 LuaBase 对象。</summary>
        public static void Tick()
        {
            if (s_env != null)
            {
                s_env.Tick();
            }
        }

        public static void Shutdown()
        {
            if (s_env == null)
            {
                return;
            }

            try
            {
                s_env.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LuaManager] Dispose 异常: " + e.Message);
            }

            s_env = null;
        }

        /// <summary>执行一段 Lua 代码，用于 GM 调试。</summary>
        public static LuaExecResult Execute(string chunk)
        {
            if (s_env == null)
            {
                string reason = string.IsNullOrEmpty(s_error) ? "尚未初始化" : s_error;
                return LuaExecResult.Fail("Lua 环境未就绪：" + reason);
            }

            if (string.IsNullOrEmpty(chunk))
            {
                return LuaExecResult.Fail("用法: /gm lua <lua 代码>");
            }

            // 先按「表达式」编译，等价于 lua REPL 的 return <line> 尝试。
            LuaFunction fn = null;
            try
            {
                fn = s_env.LoadString<LuaFunction>("return " + chunk, "gm");
            }
            catch (Exception)
            {
                fn = null;
            }

            if (fn != null)
            {
                try
                {
                    return FormatReturnValues(fn.Call());
                }
                catch (Exception e)
                {
                    return LuaExecResult.Fail(e.Message);
                }
                finally
                {
                    fn.Dispose();
                }
            }

            try
            {
                return FormatReturnValues(s_env.DoString(chunk, "gm"));
            }
            catch (Exception e)
            {
                return LuaExecResult.Fail(e.Message);
            }
        }

        private static LuaExecResult FormatReturnValues(object[] ret)
        {
            if (ret == null || ret.Length == 0)
            {
                return LuaExecResult.Ok("(执行成功，无返回值)");
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < ret.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(ret[i] == null ? "nil" : ret[i].ToString());
            }

            return LuaExecResult.Ok(sb.ToString());
        }

        /// <summary>
        /// 重新加载脚本：重新扫描源码 / 重新取 bundle 并按需解包，然后清 package.loaded。
        /// </summary>
        public static IEnumerator ReloadScriptsRoutine(Action<int> onDone)
        {
            if (s_env == null)
            {
                if (onDone != null)
                {
                    onDone(0);
                }

                yield break;
            }

            string[] previous = ModuleList.ToArray();

            yield return LoadModulesRoutine();

            try
            {
                StringBuilder sb = new StringBuilder("local loaded = package.loaded; ");
                for (int i = 0; i < previous.Length; i++)
                {
                    sb.Append("loaded['").Append(previous[i]).Append("'] = nil; ");
                }

                s_env.DoString(sb.ToString(), "lua_reload");
                s_env.DoString(BootstrapChunk, "lua_reload_module_controller");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LuaManager] 清理 package.loaded / module_controller 失败: " + e.Message);
            }

            int count = ModuleList.Count;
            Debug.Log("[LuaManager] Lua 脚本已重新加载，当前模块 " + count + " 个");
            if (onDone != null)
            {
                onDone(count);
            }
        }

        /// <summary>供 GM 触发脚本重载。</summary>
        public static bool RequestReload(Action<int> onDone = null)
        {
            if (s_env == null || s_host == null)
            {
                return false;
            }

            s_host.StartCoroutine(ReloadScriptsRoutine(onDone));
            return true;
        }

        /// <summary>供 GM 显示当前状态。</summary>
        public static string Describe()
        {
            if (s_env == null)
            {
                return "Lua 未就绪" + (string.IsNullOrEmpty(s_error) ? string.Empty : "：" + s_error);
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(s_useProjectSource ? "Lua 就绪（编辑器直读源码）" : "Lua 就绪（解包散文件）");
            sb.Append("，模块 ").Append(ModuleList.Count).Append(" 个：");

            for (int i = 0; i < ModuleList.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(ModuleList[i]);
            }

            return sb.ToString();
        }

        // ---------------------------------------------------------------- 加载

        private static IEnumerator LoadModulesRoutine()
        {
            Modules.Clear();
            ModuleList.Clear();

            if (s_useProjectSource)
            {
                LoadModuleListFromProjectSource();
                yield break;
            }

            yield return LoadFromBundleRoutine();
        }

        /// <summary>编辑器直读：扫描 Assets/Game/LuaScripts/**/*.lua，模块名用路径形式。</summary>
        private static void LoadModuleListFromProjectSource()
        {
            s_projectSourceDir = Path.Combine(Application.dataPath, ProjectSourceRelative);

            if (!Directory.Exists(s_projectSourceDir))
            {
                s_error = "源码目录不存在: " + s_projectSourceDir;
                Debug.LogWarning("[LuaManager] " + s_error);
                return;
            }

            string[] files = Directory.GetFiles(s_projectSourceDir, "*.lua", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);

            for (int i = 0; i < files.Length; i++)
            {
                string rel = files[i].Substring(s_projectSourceDir.Length).TrimStart('\\', '/');
                if (rel.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                {
                    rel = rel.Substring(0, rel.Length - 4);
                }

                AddModule(LuaBundleReader.ToModuleName(rel));
            }

            Debug.Log("[LuaManager] 扫描工程源码 " + ModuleList.Count + " 个模块: " + s_projectSourceDir);
        }

        /// <summary>打包后：取 AB 里的 bundle → 按 buildId 解包 → 从解包目录读模块名（扁平）。</summary>
        private static IEnumerator LoadFromBundleRoutine()
        {
            AssetBundleManager manager = AssetBundleManager.Instance;
            if (manager == null)
            {
                s_error = "AssetBundleManager 未初始化";
                Debug.LogWarning("[LuaManager] " + s_error);
                yield break;
            }

            TextAsset bundleAsset = null;
            try
            {
                manager.LoadAsset(
                    ScriptBundleName,
                    BundleAssetName,
                    typeof(TextAsset),
                    obj => bundleAsset = obj as TextAsset);
            }
            catch (Exception e)
            {
                s_error = "请求加载 Lua Bundle 失败: " + e.Message;
                Debug.LogError("[LuaManager] " + s_error);
                yield break;
            }

            float elapsed = 0f;
            while (bundleAsset == null && elapsed < LoadTimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (bundleAsset == null)
            {
                s_error = "加载超时 " + LoadTimeoutSeconds + "s: "
                          + ScriptBundleName + "/" + BundleAssetName
                          + "（检查是否已执行 Tools/Lua/打包 Lua Bundle）";
                Debug.LogWarning("[LuaManager] " + s_error);
                yield break;
            }

            byte[] bundleBytes = bundleAsset.bytes;

            string buildId;
            int expectedCount;
            try
            {
                LuaBundleReader.ReadHeader(bundleBytes, out buildId, out expectedCount);
            }
            catch (Exception e)
            {
                s_error = "读取 Lua Bundle 包头失败: " + e.Message;
                Debug.LogError("[LuaManager] " + s_error);
                yield break;
            }

            bool unpacked = false;
            yield return LuaScriptUnpacker.EnsureUnpackedRoutine(bundleBytes, buildId, ok => unpacked = ok);

            // 解包后立即释放 bundle 引用，避免整份字节常驻内存。
            bundleAsset = null;
            bundleBytes = null;

            if (!unpacked)
            {
                s_error = "解包失败: " + LuaScriptUnpacker.LastError;
                Debug.LogWarning("[LuaManager] " + s_error);
                yield break;
            }

            LoadModuleListFromDirectory(LuaScriptUnpacker.CurrentDir, flat: true);
            Debug.Log("[LuaManager] 解包目录 " + ModuleList.Count + " 个模块，期望 " + expectedCount + " 个");
        }

        /// <summary>从解包目录读模块名（扁平名 = 文件名去掉 .lua）。</summary>
        private static void LoadModuleListFromDirectory(string dir, bool flat)
        {
            if (!Directory.Exists(dir))
            {
                s_error = "目录不存在: " + dir;
                Debug.LogWarning("[LuaManager] " + s_error);
                return;
            }

            string[] files = Directory.GetFiles(dir, "*.lua", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);

            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(files[i]);
                AddModule(flat ? name : LuaBundleReader.ToModuleName(name));
            }
        }

        private static void AddModule(string name)
        {
            if (string.IsNullOrEmpty(name) || !Modules.Add(name))
            {
                return;
            }

            ModuleList.Add(name);
        }

        // ---------------------------------------------------------------- loader

        /// <summary>
        /// xLua 自定义 loader：按模式从工程源码或解包目录读取**单个文件**。
        /// 每次 require 只产生一次小文件 IO。
        /// </summary>
        private static byte[] CustomLoader(ref string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                return null;
            }

            string moduleName = LuaBundleReader.ToModuleName(filepath);

            // xLua 自带模块（xlua.util / perf.* / tdr.*）以 TextAsset 形式放在 Assets/XLua/Resources 下，
            // 由 xLua 内置 loader 处理。这里必须静默放行，否则会被拼成 LuaScripts/xlua/util.lua
            // 并打出误导性的「找不到模块文件」。
            if (IsXluaInternalModule(moduleName))
            {
                return null;
            }

            string path;
            if (s_useProjectSource)
            {
                if (string.IsNullOrEmpty(s_projectSourceDir))
                {
                    return null;
                }

                path = Path.Combine(s_projectSourceDir, moduleName.Replace('.', Path.DirectorySeparatorChar) + ".lua");
            }
            else
            {
                // 解包后模块名是扁平文件名；require('framework.class') 与 require('class') 都应能解析。
                string flatName = LuaBundleReader.ToFlatName(moduleName);
                path = LuaScriptUnpacker.ResolveModuleFile(flatName);
            }

            if (!File.Exists(path))
            {
                Debug.LogError("[LuaManager] 找不到模块文件: " + path + "（module=" + moduleName + "）");
                return null;
            }

            try
            {
                return File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                Debug.LogError("[LuaManager] 读取模块失败 " + path + " : " + e.Message);
                return null;
            }
        }

        /// <summary>是否为 xLua 运行时自带的模块（由内置 loader 从 Resources 读取）。</summary>
        private static bool IsXluaInternalModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return false;
            }

            return moduleName == "xlua"
                || moduleName.StartsWith("xlua.", StringComparison.Ordinal)
                || moduleName.StartsWith("perf.", StringComparison.Ordinal)
                || moduleName.StartsWith("tdr.", StringComparison.Ordinal);
        }

        // ---------------------------------------------------------------- 模式判定

        private static bool DetectProjectSourceMode()
        {
#if UNITY_EDITOR
            // Development = 0, DebugMode = 4：与 AssetBundleManager 的 LoadMode 语义一致
            int mode = UnityEditor.EditorPrefs.GetInt("GameLoadMode", 0);
            return mode == 0 || mode == 4;
#else
            return false;
#endif
        }
    }

    /// <summary>Lua 执行结果，供 GM 层直接展示。</summary>
    public readonly struct LuaExecResult
    {
        public readonly bool Success;
        public readonly string Message;

        private LuaExecResult(bool success, string message)
        {
            this.Success = success;
            this.Message = message;
        }

        public static LuaExecResult Ok(string message)
        {
            return new LuaExecResult(true, message);
        }

        public static LuaExecResult Fail(string message)
        {
            return new LuaExecResult(false, message);
        }
    }
}
