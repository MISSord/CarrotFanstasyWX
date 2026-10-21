using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// Lua 脚本预解包：<c>lua_scripts</c> 一落盘就立即解包，不等整个更新流程结束。
    /// <para>
    /// 背景：<c>pack_lua_scripts_001</c> 只有 17 KB、<c>lua_scripts</c> 只有 20 KB，
    /// 但它排在 44 个 Pack / 32 MB 的下载队列里。如果等到 <c>GameMain</c> 才解包，
    /// 玩家必须等「全部资源下完 + 启动面板关闭」才看到 Lua 就绪。
    /// 这里把 Lua 就绪时间从「全部下载完」提前到「Lua 包下完」。
    /// </para>
    /// <para>
    /// 与 <see cref="LuaManager"/> 的约定：
    /// </para>
    /// <list type="bullet">
    ///   <item>预解包成功 → <see cref="IsReady"/> 为 true，LuaManager 启动时直接扫模块表，
    ///         不再重复加载 AB / 解包；</item>
    ///   <item>预解包失败 → 只告警，LuaManager 走原路径重试（不影响启动）；</item>
    ///   <item>预解包进行中 → LuaManager 会等它结束，避免两边同时解包同一目录。</item>
    /// </list>
    /// <para>
    /// 为什么直接 <c>AssetBundle.LoadFromFile</c> 而不用 <see cref="AssetBundleManager"/>：
    /// 预解包发生在下载阶段，此时 <c>AssetBundleManager</c> 还没 <c>SetAssetBundleItem</c>
    /// （新清单在 Finalizing 才生效），按名字取不到新下载的包；直接按落盘路径加载最稳。
    /// </para>
    /// </summary>
    public sealed class LuaBundlePrefetcher : MonoBehaviour
    {
        private const string LogTag = "[LuaPrefetch]";

        /// <summary>预解包是否已完成且成功。</summary>
        public static bool IsReady { get; private set; }

        /// <summary>预解包是否正在进行。</summary>
        public static bool IsRunning { get; private set; }

        /// <summary>失败原因（成功时为空）。</summary>
        public static string LastError { get; private set; }

        private static LuaBundlePrefetcher s_runner;
        private static bool s_finished;

        /// <summary>
        /// 某个 AB 落盘后由下载器调用（主线程）。
        /// 只有 <c>lua_scripts</c> 会触发，且整局只触发一次。
        /// </summary>
        public static void OnBundlePersisted(string bundleName)
        {
            if (s_finished || IsRunning)
            {
                return;
            }

            if (!string.Equals(bundleName, LuaBundleReader.BundleName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!EnsureRunner())
            {
                return;
            }

            IsRunning = true;
            try
            {
                s_runner.StartCoroutine(s_runner.UnpackRoutine());
            }
            catch (Exception e)
            {
                LastError = "启动预解包协程失败: " + e.Message;
                Finish(false);
            }
        }

        /// <summary>测试/重载用：复位状态（不会删除已解包目录）。</summary>
        public static void ResetState()
        {
            IsReady = false;
            IsRunning = false;
            LastError = null;
            s_finished = false;
        }

        private static bool EnsureRunner()
        {
            if (s_runner != null)
            {
                return true;
            }

            try
            {
                GameObject go = new GameObject("[LuaBundlePrefetcher]");
                DontDestroyOnLoad(go);
                s_runner = go.AddComponent<LuaBundlePrefetcher>();
                return s_runner != null;
            }
            catch (Exception e)
            {
                LastError = "创建预解包宿主失败: " + e.Message;
                Debug.LogWarning(LogTag + " " + LastError);
                s_finished = true;
                return false;
            }
        }

        private IEnumerator UnpackRoutine()
        {
            string bundlePath = AssetBundlePathHelper.GetLocalLZ4Path(LuaBundleReader.BundleName);
            byte[] bytes = null;
            string buildId = null;
            int expectedCount = 0;

            // 1) 读 AB（同步 LoadFromFile + 一次资产读取，20 KB 级别，不值得分帧）
            AssetBundle bundle = null;
            try
            {
                if (!File.Exists(bundlePath))
                {
                    LastError = "落盘文件不存在: " + bundlePath;
                }
                else
                {
                    bundle = AssetBundle.LoadFromFile(bundlePath);
                    if (bundle == null)
                    {
                        LastError = "AssetBundle.LoadFromFile 返回 null: " + bundlePath;
                    }
                    else
                    {
                        TextAsset asset = bundle.LoadAsset<TextAsset>(LuaBundleReader.AssetName);
                        if (asset == null)
                        {
                            LastError = "包内缺少资产: " + LuaBundleReader.AssetName;
                        }
                        else
                        {
                            bytes = asset.bytes;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LastError = "读取 Lua AB 失败: " + e.Message;
            }
            finally
            {
                // bytes 已经是拷贝，立刻释放 AB，避免和启动时的加载互相占引用。
                if (bundle != null)
                {
                    try
                    {
                        bundle.Unload(false);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning(LogTag + " 卸载 AB 失败: " + e.Message);
                    }
                }
            }

            if (bytes == null || bytes.Length == 0)
            {
                Finish(false);
                yield break;
            }

            // 2) 读包头（buildId + 脚本条数）
            try
            {
                LuaBundleReader.ReadHeader(bytes, out buildId, out expectedCount);
            }
            catch (Exception e)
            {
                LastError = "读取包头失败: " + e.Message;
                Finish(false);
                yield break;
            }

            // 3) 解包（内部：buildId 命中直接跳过 / 否则 incoming → 校验 → 原子切换）
            bool unpacked = false;
            yield return LuaScriptUnpacker.EnsureUnpackedRoutine(
                bytes,
                buildId,
                ok => unpacked = ok);

            bytes = null;

            if (!unpacked)
            {
                LastError = string.IsNullOrEmpty(LuaScriptUnpacker.LastError)
                    ? "解包失败"
                    : LuaScriptUnpacker.LastError;
            }

            Finish(unpacked, buildId, expectedCount);
        }

        private static void Finish(bool ok, string buildId = null, int expectedCount = 0)
        {
            IsReady = ok;
            IsRunning = false;
            s_finished = true;

            if (ok)
            {
                Debug.Log(string.Format(
                    "{0} Lua 脚本已提前解包就绪：buildId={1}，{2} 个脚本 → {3}",
                    LogTag,
                    buildId,
                    expectedCount,
                    LuaScriptUnpacker.CurrentDir));
            }
            else
            {
                Debug.LogWarning(LogTag + " 预解包失败（启动时会重试）: " + LastError);
            }

            // 宿主只用于跑这一条协程，结束即销毁。
            if (s_runner != null)
            {
                try
                {
                    // 静态方法里不能用实例语法调 Destroy，这里显式走 UnityEngine.Object。
                    UnityEngine.Object.Destroy(s_runner.gameObject);
                }
                catch
                {
                    // 退出流程里可能已经销毁，忽略。
                }

                s_runner = null;
            }
        }
    }
}
