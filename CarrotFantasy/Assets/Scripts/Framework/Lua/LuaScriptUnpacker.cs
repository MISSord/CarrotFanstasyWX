using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 把 Lua Bundle 解包成**一堆扁平的 .lua 文件**落到本地，供 require 按需读取。
    /// <para>
    /// 为什么要落成散文件：xLua 的 loader 必须是同步的，如果只保留一个 bundle，
    /// 就只能在启动时全量读入内存（IO 尖峰 + 常驻内存）。
    /// 落成散文件后，<c>require('view_manager')</c> 只读 <c>current/view_manager.lua</c> 一个文件。
    /// </para>
    /// <para>
    /// 目录结构（persistentDataPath 下）：
    /// <code>
    /// LuaScripts/
    ///   current/        ← 当前生效版本（全部 .lua 平铺 + .build_id）
    ///   incoming.tmp/   ← 解包中的临时目录
    ///   previous.tmp/   ← 切换瞬间的旧版本备份
    /// </code>
    /// </para>
    /// 切换策略：解包到 incoming → 校验条数 → current 改名 previous → incoming 改名 current
    /// → 删 previous。任何一步失败都尝试回滚到旧版本。
    /// </summary>
    public static class LuaScriptUnpacker
    {
        public const string RootFolderName = "LuaScripts";
        private const string BuildIdFileName = ".build_id";

        /// <summary>每帧最多写多少字节，避免写盘造成单帧卡顿（按字节而非文件数分帧）。</summary>
        private const int BytesPerFrame = 256 * 1024;

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public static string RootDir
        {
            get { return Path.Combine(Application.persistentDataPath, RootFolderName); }
        }

        public static string CurrentDir
        {
            get { return Path.Combine(RootDir, "current"); }
        }

        private static string IncomingDir
        {
            get { return Path.Combine(RootDir, "incoming.tmp"); }
        }

        private static string BackupDir
        {
            get { return Path.Combine(RootDir, "previous.tmp"); }
        }

        private static string BuildIdFilePath
        {
            get { return Path.Combine(CurrentDir, BuildIdFileName); }
        }

        /// <summary>本地是否已有可用版本。</summary>
        public static bool IsReady { get; private set; }

        /// <summary>最近一次失败原因。</summary>
        public static string LastError { get; private set; }

        /// <summary>本地已安装的 buildId（没有则返回 null）。</summary>
        public static string ReadInstalledBuildId()
        {
            try
            {
                if (!File.Exists(BuildIdFilePath))
                {
                    return null;
                }

                return File.ReadAllText(BuildIdFilePath, Utf8NoBom).Trim();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>模块文件路径（扁平名，不含 .lua）。</summary>
        public static string ResolveModuleFile(string flatName)
        {
            return Path.Combine(CurrentDir, flatName + ".lua");
        }

        /// <summary>
        /// 确保本地脚本与给定 buildId 一致。一致则直接返回 true（跳过解包）。
        /// 全程分帧，调用方用 <c>yield return</c> 驱动即可。
        /// </summary>
        public static IEnumerator EnsureUnpackedRoutine(byte[] bundleBytes, string buildId, Action<bool> onDone)
        {
            IsReady = false;
            LastError = null;

            if (bundleBytes == null || bundleBytes.Length == 0)
            {
                LastError = "bundle 数据为空";
                Finish(onDone, false);
                yield break;
            }

            if (string.IsNullOrEmpty(buildId))
            {
                LastError = "buildId 为空";
                Finish(onDone, false);
                yield break;
            }

            // 已经是这个版本 → 跳过
            string installed = ReadInstalledBuildId();
            if (installed == buildId && Directory.Exists(CurrentDir))
            {
                IsReady = true;
                Debug.Log("[LuaUnpack] 本地已是 buildId=" + buildId + "，跳过解包");
                Finish(onDone, true);
                yield break;
            }

            // 清理上次残留
            try
            {
                DeleteDirectory(BackupDir);
                DeleteDirectory(IncomingDir);
                Directory.CreateDirectory(IncomingDir);
            }
            catch (Exception e)
            {
                LastError = "准备解包目录失败: " + e.Message;
                Debug.LogError("[LuaUnpack] " + LastError);
                Finish(onDone, false);
                yield break;
            }

            int expected;
            string headerBuildId;
            try
            {
                LuaBundleReader.ReadHeader(bundleBytes, out headerBuildId, out expected);
            }
            catch (Exception e)
            {
                LastError = "读取包头失败: " + e.Message;
                Debug.LogError("[LuaUnpack] " + LastError);
                DeleteDirectoryQuietly(IncomingDir);
                Finish(onDone, false);
                yield break;
            }

            if (!string.Equals(headerBuildId, buildId, StringComparison.OrdinalIgnoreCase))
            {
                LastError = "包头 buildId(" + headerBuildId + ") 与期望(" + buildId + ") 不一致";
                Debug.LogError("[LuaUnpack] " + LastError);
                DeleteDirectoryQuietly(IncomingDir);
                Finish(onDone, false);
                yield break;
            }

            // 流式解包 + 分帧写盘
            int written = 0;
            int bytesThisFrame = 0;
            Exception writeError = null;
            bool parseFailed = false;

            using (IEnumerator<KeyValuePair<string, string>> enumerator =
                LuaBundleReader.EnumerateLazy(bundleBytes).GetEnumerator())
            {
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = enumerator.MoveNext();
                    }
                    catch (Exception e)
                    {
                        writeError = e;
                        parseFailed = true;
                        break;
                    }

                    if (!moved)
                    {
                        break;
                    }

                    KeyValuePair<string, string> entry = enumerator.Current;

                    try
                    {
                        string filePath = Path.Combine(IncomingDir, entry.Key + ".lua");
                        File.WriteAllText(filePath, entry.Value, Utf8NoBom);
                        written++;
                        bytesThisFrame += entry.Value.Length * 3;
                    }
                    catch (Exception e)
                    {
                        writeError = e;
                        break;
                    }

                    if (bytesThisFrame >= BytesPerFrame)
                    {
                        bytesThisFrame = 0;
                        yield return null;
                    }
                }
            }

            if (writeError != null)
            {
                LastError = (parseFailed ? "解析 bundle 失败: " : "写入脚本失败: ") + writeError.Message;
                Debug.LogError("[LuaUnpack] " + LastError);
                DeleteDirectoryQuietly(IncomingDir);
                Finish(onDone, false);
                yield break;
            }

            if (written != expected)
            {
                LastError = "解包条数不一致：期望 " + expected + "，实际 " + written;
                Debug.LogError("[LuaUnpack] " + LastError);
                DeleteDirectoryQuietly(IncomingDir);
                Finish(onDone, false);
                yield break;
            }

            // 写入版本标记
            try
            {
                File.WriteAllText(Path.Combine(IncomingDir, BuildIdFileName), buildId, Utf8NoBom);
            }
            catch (Exception e)
            {
                LastError = "写入 buildId 失败: " + e.Message;
                Debug.LogError("[LuaUnpack] " + LastError);
                DeleteDirectoryQuietly(IncomingDir);
                Finish(onDone, false);
                yield break;
            }

            // 原子切换
            bool swapped = false;
            try
            {
                if (Directory.Exists(CurrentDir))
                {
                    Directory.Move(CurrentDir, BackupDir);
                }

                Directory.Move(IncomingDir, CurrentDir);
                swapped = true;
            }
            catch (Exception e)
            {
                LastError = "切换版本目录失败: " + e.Message;
                Debug.LogError("[LuaUnpack] " + LastError);

                // 回滚：把旧版本挪回去
                try
                {
                    if (!Directory.Exists(CurrentDir) && Directory.Exists(BackupDir))
                    {
                        Directory.Move(BackupDir, CurrentDir);
                    }
                }
                catch (Exception rollbackError)
                {
                    Debug.LogError("[LuaUnpack] 回滚失败: " + rollbackError.Message);
                }
            }

            DeleteDirectoryQuietly(BackupDir);
            DeleteDirectoryQuietly(IncomingDir);

            if (!swapped)
            {
                // 切换失败但本地可能仍有旧版本可用
                if (Directory.Exists(CurrentDir))
                {
                    IsReady = true;
                    Finish(onDone, true);
                    yield break;
                }

                Finish(onDone, false);
                yield break;
            }

            IsReady = true;
            Debug.Log("[LuaUnpack] 解包完成：buildId=" + buildId + "，脚本 " + written + " 个 → " + CurrentDir);
            Finish(onDone, true);
        }

        private static void Finish(Action<bool> onDone, bool success)
        {
            if (onDone != null)
            {
                onDone(success);
            }
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static void DeleteDirectoryQuietly(string path)
        {
            try
            {
                DeleteDirectory(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LuaUnpack] 清理目录失败 " + path + " : " + e.Message);
            }
        }
    }
}
