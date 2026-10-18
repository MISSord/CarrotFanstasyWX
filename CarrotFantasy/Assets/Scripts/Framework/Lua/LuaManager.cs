using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using XLua;

namespace CarrotFantasy
{
    /// <summary>
    /// Lua 环境管理。
    /// <para>
    /// 职责：LuaEnv 生命周期、从 AssetBundle 预加载 Lua 脚本、为 require 提供自定义 loader、GM 执行入口。
    /// </para>
    /// <para>
    /// 这里直接用 <see cref="AssetBundleManager"/> 而不是 <c>TextLoader</c>：脚本在初始化时已全量预载进
    /// 内存字典（xLua 的 loader 必须是同步的），不需要再经过一层 loader 缓存与引用计数。
    /// </para>
    /// </summary>
    public static class LuaManager
    {
        /// <summary>Lua 脚本所在的 AB 包名（由 PrefabABPathPostprocessor 按 Assets/Game/LuaScripts 规则分配）。</summary>
        public const string ScriptBundleName = "lua_scripts";

        /// <summary>脚本索引资产名，内容形如 {"modules":[{"module":"gm.demo","asset":"gm_demo"}]}。</summary>
        public const string IndexAssetName = "lua_index";

        /// <summary>单个脚本加载超时。AB 异常时不能让启动流程无限等待。</summary>
        private const float LoadTimeoutSeconds = 5f;

        /// <summary>模块名（gm.demo）→ Lua 源码。</summary>
        private static readonly Dictionary<string, string> ModuleScripts =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>资产名（gm_demo）→ 模块名（gm.demo），用于直接按资产名 require。</summary>
        private static readonly Dictionary<string, string> AssetToModule =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static LuaEnv s_env;
        private static bool s_initializing;
        private static string s_error;

        /// <summary>初始化时传入的宿主，用于 GM 触发脚本重载（需要跑协程）。</summary>
        private static MonoBehaviour s_host;

        public static bool IsReady
        {
            get { return s_env != null; }
        }

        public static string LastError
        {
            get { return s_error; }
        }

        public static int ModuleCount
        {
            get { return ModuleScripts.Count; }
        }

        [Serializable]
        private class LuaIndex
        {
            public List<LuaModuleEntry> modules;
        }

        [Serializable]
        private class LuaModuleEntry
        {
            public string module;
            public string asset;
        }

        /// <summary>
        /// 初始化 Lua 环境：读索引 → 预载脚本 → 建 LuaEnv。
        /// 失败不会中断启动流程（Lua 只影响 GM / 热更逻辑，不应阻断进游戏）。
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

            string indexJson = null;
            yield return LoadTextRoutine(IndexAssetName, text => indexJson = text);

            if (string.IsNullOrEmpty(indexJson))
            {
                s_error = "索引 " + ScriptBundleName + "/" + IndexAssetName + " 未加载到";
                Debug.LogWarning("[LuaManager] " + s_error + "，Lua 热更不可用");
                s_initializing = false;
                yield break;
            }

            LuaIndex index = null;
            try
            {
                index = JsonUtility.FromJson<LuaIndex>(indexJson);
            }
            catch (Exception e)
            {
                Debug.LogError("[LuaManager] 解析 " + IndexAssetName + " 失败: " + e.Message);
            }

            if (index != null && index.modules != null)
            {
                for (int i = 0; i < index.modules.Count; i++)
                {
                    LuaModuleEntry entry = index.modules[i];
                    if (entry == null || string.IsNullOrEmpty(entry.module) || string.IsNullOrEmpty(entry.asset))
                    {
                        continue;
                    }

                    string text = null;
                    yield return LoadTextRoutine(entry.asset, loaded => text = loaded);

                    if (string.IsNullOrEmpty(text))
                    {
                        Debug.LogWarning("[LuaManager] 模块加载失败: " + entry.module + " (asset=" + entry.asset + ")");
                        continue;
                    }

                    ModuleScripts[entry.module] = text;
                    AssetToModule[entry.asset] = entry.module;
                }
            }

            try
            {
                s_env = new LuaEnv();
                s_env.AddLoader(CustomLoader);

                // 预热内置库，尽早暴露 xLua/Resources 缺失这类问题。
                s_env.DoString("local ok, err = pcall(require, 'xlua.util') if not ok then error(err) end", "lua_bootstrap");

                Debug.Log("[LuaManager] Lua 环境就绪，已预载模块 " + ModuleScripts.Count + " 个，包 " + ScriptBundleName);
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
            // 这样 /gm lua require('gm.demo').ping() 也能看到返回值，不必逼调用方记得写 return。
            LuaFunction fn = null;
            try
            {
                fn = s_env.LoadString<LuaFunction>("return " + chunk, "gm");
            }
            catch (Exception)
            {
                // 编译失败说明它是语句（如 a = 1），走下面的 DoString。
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
                    // 已编译通过，这里是运行时异常，不能再回退重跑，否则副作用会执行两次。
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
        /// 重新从 AB 拉取全部 Lua 脚本，并清掉 package.loaded，
        /// 从而做到「改脚本 → 重打 lua_scripts → /gm lua reload 即可生效」，不用重启进程。
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

            string[] modules = new string[ModuleScripts.Count];
            ModuleScripts.Keys.CopyTo(modules, 0);

            Dictionary<string, string> moduleToAsset = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in AssetToModule)
            {
                moduleToAsset[pair.Value] = pair.Key;
            }

            int reloaded = 0;
            for (int i = 0; i < modules.Length; i++)
            {
                string asset;
                if (!moduleToAsset.TryGetValue(modules[i], out asset))
                {
                    continue;
                }

                string text = null;
                yield return LoadTextRoutine(asset, loaded => text = loaded);
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                ModuleScripts[modules[i]] = text;
                reloaded++;
            }

            // 清 package.loaded，下次 require 才会重新走自定义 loader 取到新源码。
            try
            {
                StringBuilder sb = new StringBuilder("local loaded = package.loaded; ");
                for (int i = 0; i < modules.Length; i++)
                {
                    sb.Append("loaded['").Append(modules[i]).Append("'] = nil; ");
                }

                s_env.DoString(sb.ToString(), "lua_reload");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LuaManager] 清理 package.loaded 失败: " + e.Message);
            }

            Debug.Log("[LuaManager] Lua 脚本已重新加载 " + reloaded + "/" + modules.Length + " 个");
            if (onDone != null)
            {
                onDone(reloaded);
            }
        }

        /// <summary>供 GM 触发脚本重载：重新从 AB 拉源码并清 package.loaded。</summary>
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
            sb.Append("Lua 就绪，包 ").Append(ScriptBundleName).Append("，模块 ").Append(ModuleScripts.Count).Append(" 个：");

            bool first = true;
            foreach (string module in ModuleScripts.Keys)
            {
                if (!first)
                {
                    sb.Append(", ");
                }

                sb.Append(module);
                first = false;
            }

            return sb.ToString();
        }

        /// <summary>xLua 自定义 loader：require 时从预载的脚本字典取源码。</summary>
        private static byte[] CustomLoader(ref string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                return null;
            }

            string key = NormalizeModuleName(filepath);

            string script;
            if (!ModuleScripts.TryGetValue(key, out script))
            {
                string module;
                if (AssetToModule.TryGetValue(key, out module))
                {
                    ModuleScripts.TryGetValue(module, out script);
                }
            }

            if (string.IsNullOrEmpty(script))
            {
                return null;
            }

            return Encoding.UTF8.GetBytes(script);
        }

        /// <summary>把 require 传入的名字统一成索引里的模块名写法（gm/demo → gm.demo）。</summary>
        private static string NormalizeModuleName(string filepath)
        {
            return filepath.Replace('\\', '/').Replace('/', '.');
        }

        /// <summary>
        /// 加载一个脚本资产，带一次兜底重试。
        /// <para>
        /// Unity 只剥掉最后一层扩展名：<c>gm_demo.lua.txt</c> 的资源名是 <c>gm_demo.lua</c>。
        /// 索引里按 <c>gm_demo.lua</c> 写；万一某个 Unity 版本只取到 <c>gm_demo</c>，这里再试一次，
        /// 免得因为文件名细节让整条 Lua 链路失效。
        /// </para>
        /// </summary>
        private static IEnumerator LoadTextRoutine(string assetName, Action<string> onLoaded)
        {
            string text = null;
            yield return LoadTextOnceRoutine(assetName, loaded => text = loaded);

            if (string.IsNullOrEmpty(text)
                && assetName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)
                && assetName.Length > 4)
            {
                yield return LoadTextOnceRoutine(assetName.Substring(0, assetName.Length - 4), loaded => text = loaded);
            }

            if (onLoaded != null)
            {
                onLoaded(text);
            }
        }

        /// <summary>
        /// 通过 AssetBundleManager 异步加载一个文本资产。
        /// Development / Debug 模式下管理器会走 AssetDatabase，同样走这个入口。
        /// </summary>
        private static IEnumerator LoadTextOnceRoutine(string assetName, Action<string> onLoaded)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                if (onLoaded != null)
                {
                    onLoaded(null);
                }

                yield break;
            }

            AssetBundleManager manager = AssetBundleManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[LuaManager] AssetBundleManager 未初始化，跳过 " + assetName);
                if (onLoaded != null)
                {
                    onLoaded(null);
                }

                yield break;
            }

            TextAsset result = null;
            try
            {
                manager.LoadAsset(
                    ScriptBundleName,
                    assetName,
                    typeof(TextAsset),
                    obj => result = obj as TextAsset);
            }
            catch (Exception e)
            {
                Debug.LogError("[LuaManager] 请求加载 " + assetName + " 失败: " + e.Message);
                if (onLoaded != null)
                {
                    onLoaded(null);
                }

                yield break;
            }

            float elapsed = 0f;
            while (result == null && elapsed < LoadTimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (result == null)
            {
                Debug.LogWarning(
                    "[LuaManager] 加载超时 " + LoadTimeoutSeconds + "s: " + ScriptBundleName + "/" + assetName
                    + "（检查该资产是否已分配到 AB 包 " + ScriptBundleName + "）");
                if (onLoaded != null)
                {
                    onLoaded(null);
                }

                yield break;
            }

            if (onLoaded != null)
            {
                onLoaded(result.text);
            }
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
