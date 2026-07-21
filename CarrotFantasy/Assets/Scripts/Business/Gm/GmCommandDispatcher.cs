using System;
using System.Collections.Generic;
using System.Text;

namespace CarrotFantasy
{
    /// <summary>GM 命令执行上下文。</summary>
    public sealed class GmCommandContext
    {
        public long UserId = StandaloneBackendMock.DefaultUserId;
    }

    /// <summary>GM 命令执行结果。</summary>
    public readonly struct GmCommandResult
    {
        public readonly bool Success;
        public readonly string Message;

        public GmCommandResult(bool success, string message)
        {
            this.Success = success;
            this.Message = message ?? string.Empty;
        }

        public static GmCommandResult Ok(string message)
        {
            return new GmCommandResult(true, message);
        }

        public static GmCommandResult Fail(string message)
        {
            return new GmCommandResult(false, message);
        }
    }

    /// <summary>
    /// 解析并执行 <c>/gm 指令 参数...</c> 文本命令。
    /// </summary>
    public static class GmCommandDispatcher
    {
        public const string RequiredPrefix = "/gm";

        delegate GmCommandResult CommandHandler(GmCommandContext ctx, string[] args);

        static readonly Dictionary<string, CommandHandler> Handlers = BuildHandlers();

        public static GmCommandResult Execute(string input, GmCommandContext ctx)
        {
            if (ctx == null)
            {
                return GmCommandResult.Fail("GM 上下文为空。");
            }

            if (!TryParse(input, out string command, out string[] args, out string parseError))
            {
                return GmCommandResult.Fail(parseError);
            }

            if (!Handlers.TryGetValue(command, out CommandHandler handler))
            {
                return GmCommandResult.Fail(
                    string.Format("未知指令 \"{0}\"。输入 /gm help 查看列表。", command));
            }

            try
            {
                return handler(ctx, args);
            }
            catch (Exception ex)
            {
                return GmCommandResult.Fail("执行异常: " + ex.Message);
            }
        }

        public static string GetHelpText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("格式: /gm <指令> [参数...]");
            sb.AppendLine();
            sb.AppendLine("changeData <大关0起> <小关> [萝卜0-3] [全清0|1]  解锁到指定关（大关从0起，例: /gm changeData 0 1）");
            sb.AppendLine("unlockTo <大关1起> <小关> [萝卜] [全清0|1]       同上，大关从1起");
            sb.AppendLine("setCell <大关> <小关> <萝卜> <全清> <解锁0|1>    修改单关");
            sb.AppendLine("unlockAll [萝卜] [全清0|1]                      解锁全部");
            sb.AppendLine("reset                                           重置初始档");
            sb.AppendLine("load                                            从 PlayerPrefs 加载");
            sb.AppendLine("delete                                          删除本地存档");
            sb.AppendLine("setUser <userId>                                设置存档 UserId");
            sb.AppendLine("preview                                         输出当前快照");
            sb.AppendLine("startBattle <大关> <小关>                       进战斗（需 Play）");
            sb.AppendLine("openMap [大关] [小关]                           打开选关（需 Play）");
            sb.AppendLine("startRoguelike <大关> <小关>                   进肉鸽小关地图（需 Play）");
            sb.AppendLine("openRoguelikeMap [大关] [小关]                 打开肉鸽选关（需 Play）");
            sb.AppendLine("help [指令名]                                   显示帮助");
            return sb.ToString();
        }

        public static bool TryParse(string input, out string command, out string[] args, out string error)
        {
            command = string.Empty;
            args = Array.Empty<string>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "输入为空。";
                return false;
            }

            string text = input.Trim();
            if (!text.StartsWith(RequiredPrefix, StringComparison.OrdinalIgnoreCase))
            {
                error = "必须以 " + RequiredPrefix + " 开头。";
                return false;
            }

            string rest = text.Substring(RequiredPrefix.Length).Trim();
            if (rest.Length == 0)
            {
                error = "缺少指令名，例如: /gm changeData 0 1";
                return false;
            }

            List<string> tokens = SplitTokens(rest);
            if (tokens.Count == 0)
            {
                error = "缺少指令名。";
                return false;
            }

            command = tokens[0].ToLowerInvariant();
            if (tokens.Count == 1)
            {
                args = Array.Empty<string>();
            }
            else
            {
                args = tokens.GetRange(1, tokens.Count - 1).ToArray();
            }

            return true;
        }

        static Dictionary<string, CommandHandler> BuildHandlers()
        {
            return new Dictionary<string, CommandHandler>(StringComparer.OrdinalIgnoreCase)
            {
                { "help", HandleHelp },
                { "changedata", HandleChangeData },
                { "unlockto", HandleUnlockTo },
                { "setcell", HandleSetCell },
                { "unlockall", HandleUnlockAll },
                { "reset", HandleReset },
                { "load", HandleLoad },
                { "delete", HandleDelete },
                { "setuser", HandleSetUser },
                { "preview", HandlePreview },
                { "startbattle", HandleStartBattle },
                { "openmap", HandleOpenMap },
                { "startroguelike", HandleStartRoguelike },
                { "openroguelikemap", HandleOpenRoguelikeMap },
            };
        }

        static GmCommandResult HandleHelp(GmCommandContext ctx, string[] args)
        {
            if (args.Length == 0)
            {
                return GmCommandResult.Ok(GetHelpText());
            }

            string key = args[0].ToLowerInvariant();
            if (!Handlers.ContainsKey(key))
            {
                return GmCommandResult.Fail("未找到指令: " + args[0]);
            }

            return GmCommandResult.Ok(GetHelpText());
        }

        /// <summary>大关参数从 0 起（0=第1章），小关从 1 起。</summary>
        static GmCommandResult HandleChangeData(GmCommandContext ctx, string[] args)
        {
            if (args.Length < 2)
            {
                return GmCommandResult.Fail("用法: /gm changeData <大关0起> <小关> [萝卜] [全清0|1]");
            }

            if (!TryParseInt(args[0], out int chapter0))
            {
                return GmCommandResult.Fail("大关参数无效: " + args[0]);
            }

            if (!TryParseInt(args[1], out int level))
            {
                return GmCommandResult.Fail("小关参数无效: " + args[1]);
            }

            int big = chapter0 + 1;
            byte carrot = MapInfoType.CARROT_STATE_GOLD;
            byte allClear = MapInfoType.ALL_CLEAR;
            if (args.Length >= 3 && !TryParseByte(args[2], out carrot))
            {
                return GmCommandResult.Fail("萝卜参数无效: " + args[2]);
            }

            if (args.Length >= 4 && !TryParseAllClear(args[3], out allClear))
            {
                return GmCommandResult.Fail("全清参数无效: " + args[3]);
            }

            return ApplyUnlockTo(ctx, big, level, carrot, allClear, "changeData");
        }

        static GmCommandResult HandleUnlockTo(GmCommandContext ctx, string[] args)
        {
            if (args.Length < 2)
            {
                return GmCommandResult.Fail("用法: /gm unlockTo <大关1起> <小关> [萝卜] [全清0|1]");
            }

            if (!TryParseInt(args[0], out int big))
            {
                return GmCommandResult.Fail("大关参数无效: " + args[0]);
            }

            if (!TryParseInt(args[1], out int level))
            {
                return GmCommandResult.Fail("小关参数无效: " + args[1]);
            }

            byte carrot = MapInfoType.CARROT_STATE_GOLD;
            byte allClear = MapInfoType.ALL_CLEAR;
            if (args.Length >= 3 && !TryParseByte(args[2], out carrot))
            {
                return GmCommandResult.Fail("萝卜参数无效: " + args[2]);
            }

            if (args.Length >= 4 && !TryParseAllClear(args[3], out allClear))
            {
                return GmCommandResult.Fail("全清参数无效: " + args[3]);
            }

            return ApplyUnlockTo(ctx, big, level, carrot, allClear, "unlockTo");
        }

        static GmCommandResult ApplyUnlockTo(
            GmCommandContext ctx,
            int big,
            int level,
            byte carrot,
            byte allClear,
            string cmdName)
        {
            string snapshot = GmMapProgressService.BuildUnlockUpToSnapshot(big, level, carrot, allClear);
            GmMapProgressService.ApplySnapshot(snapshot, ctx.UserId);
            return GmCommandResult.Ok(
                string.Format(
                    "[{0}] 已解锁到 {1}-{2}（已完成关萝卜={3} 全清={4}）。",
                    cmdName,
                    big,
                    level,
                    carrot,
                    allClear == MapInfoType.ALL_CLEAR ? 1 : 0));
        }

        static GmCommandResult HandleSetCell(GmCommandContext ctx, string[] args)
        {
            if (args.Length < 5)
            {
                return GmCommandResult.Fail("用法: /gm setCell <大关> <小关> <萝卜> <全清> <解锁0|1>");
            }

            if (!TryParseInt(args[0], out int big)
                || !TryParseInt(args[1], out int level)
                || !TryParseByte(args[2], out byte carrot)
                || !TryParseAllClear(args[3], out byte allClear)
                || !TryParseUnlock(args[4], out byte unlocked))
            {
                return GmCommandResult.Fail("参数格式错误，请检查大关/小关/萝卜/全清/解锁。");
            }

            string baseSnapshot = GmMapProgressService.GetLiveSnapshotOrPersisted(ctx.UserId);
            string snapshot = GmMapProgressService.BuildSingleCellSnapshot(
                baseSnapshot,
                big,
                level,
                carrot,
                allClear,
                unlocked);
            GmMapProgressService.ApplySnapshot(snapshot, ctx.UserId);
            return GmCommandResult.Ok(
                string.Format(
                    "已写入 {0}-{1}: 萝卜={2} 全清={3} 解锁={4}",
                    big,
                    level,
                    carrot,
                    allClear,
                    unlocked));
        }

        static GmCommandResult HandleUnlockAll(GmCommandContext ctx, string[] args)
        {
            byte carrot = MapInfoType.CARROT_STATE_GOLD;
            byte allClear = MapInfoType.ALL_CLEAR;
            if (args.Length >= 1 && !TryParseByte(args[0], out carrot))
            {
                return GmCommandResult.Fail("萝卜参数无效: " + args[0]);
            }

            if (args.Length >= 2 && !TryParseAllClear(args[1], out allClear))
            {
                return GmCommandResult.Fail("全清参数无效: " + args[1]);
            }

            string snapshot = GmMapProgressService.BuildUnlockAllSnapshot(carrot, allClear);
            GmMapProgressService.ApplySnapshot(snapshot, ctx.UserId);
            return GmCommandResult.Ok("已解锁全部关卡。");
        }

        static GmCommandResult HandleReset(GmCommandContext ctx, string[] args)
        {
            if (args.Length > 0)
            {
                return GmCommandResult.Fail("reset 不需要参数。");
            }

            string snapshot = GmMapProgressService.BuildResetSnapshot();
            GmMapProgressService.ApplySnapshot(snapshot, ctx.UserId);
            return GmCommandResult.Ok("已重置为初始地图进度。");
        }

        static GmCommandResult HandleLoad(GmCommandContext ctx, string[] args)
        {
            if (args.Length > 0)
            {
                return GmCommandResult.Fail("load 不需要参数。");
            }

            string loaded = GmMapProgressService.LoadPersistedSnapshot(ctx.UserId);
            if (string.IsNullOrEmpty(loaded))
            {
                return GmCommandResult.Fail("未找到 UserId=" + ctx.UserId + " 的本地存档。");
            }

            GmMapProgressService.ApplySnapshot(loaded, ctx.UserId);
            return GmCommandResult.Ok("已从 PlayerPrefs 加载并应用。");
        }

        static GmCommandResult HandleDelete(GmCommandContext ctx, string[] args)
        {
            if (args.Length > 0)
            {
                return GmCommandResult.Fail("delete 不需要参数。");
            }

            GmMapProgressService.DeletePersistedSnapshot(ctx.UserId);
            return GmCommandResult.Ok("已删除 UserId=" + ctx.UserId + " 的本地存档。");
        }

        static GmCommandResult HandleSetUser(GmCommandContext ctx, string[] args)
        {
            if (args.Length < 1)
            {
                return GmCommandResult.Fail("用法: /gm setUser <userId>");
            }

            if (!long.TryParse(args[0], out long userId) || userId <= 0)
            {
                return GmCommandResult.Fail("userId 无效: " + args[0]);
            }

            ctx.UserId = userId;
            return GmCommandResult.Ok("已设置 UserId=" + userId);
        }

        static GmCommandResult HandlePreview(GmCommandContext ctx, string[] args)
        {
            string snapshot = GmMapProgressService.GetLiveSnapshotOrPersisted(ctx.UserId);
            return GmCommandResult.Ok(snapshot);
        }

        static GmCommandResult HandleStartBattle(GmCommandContext ctx, string[] args)
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return GmCommandResult.Fail("startBattle 需要在 Play 模式下执行。");
            }

            if (args.Length < 2)
            {
                return GmCommandResult.Fail("用法: /gm startBattle <大关> <小关>");
            }

            if (!TryParseInt(args[0], out int big) || !TryParseInt(args[1], out int level))
            {
                return GmCommandResult.Fail("大关或小关参数无效。");
            }

            string snapshot = GmMapProgressService.BuildUnlockUpToSnapshot(big, level);
            GmMapProgressService.ApplySnapshot(snapshot, ctx.UserId);
            BattleLauncher.StartClassicLevel(big, level);
            return GmCommandResult.Ok(string.Format("已解锁并进入战斗 {0}-{1}。", big, level));
        }

        static GmCommandResult HandleOpenMap(GmCommandContext ctx, string[] args)
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return GmCommandResult.Fail("openMap 需要在 Play 模式下执行。");
            }

            int big = 1;
            int level = 1;
            if (args.Length >= 1 && !TryParseInt(args[0], out big))
            {
                return GmCommandResult.Fail("大关参数无效: " + args[0]);
            }

            if (args.Length >= 2 && !TryParseInt(args[1], out level))
            {
                return GmCommandResult.Fail("小关参数无效: " + args[1]);
            }

            if (ViewManager.Instance == null)
            {
                return GmCommandResult.Fail("ViewManager 未初始化。");
            }

            ViewManager.Instance.OpenView<MapBigLevelPanel>();
            if (!ViewManager.Instance.viewTypeDic.TryGetValue(typeof(MapNormalLevelPanel), out BaseView levelView))
            {
                return GmCommandResult.Fail("MapNormalLevelPanel 未注册。");
            }

            var panel = (MapNormalLevelPanel)levelView;
            panel.OpenForBigLevel(big, level);
            ViewManager.Instance.OpenView<MapNormalLevelPanel>();
            return GmCommandResult.Ok(string.Format("已打开选关界面，选中 {0}-{1}。", big, level));
        }

        static GmCommandResult HandleStartRoguelike(GmCommandContext ctx, string[] args)
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return GmCommandResult.Fail("startRoguelike 需要在 Play 模式下执行。");
            }

            if (args.Length < 2)
            {
                return GmCommandResult.Fail("用法: /gm startRoguelike <大关> <小关>");
            }

            if (!TryParseInt(args[0], out int big) || !TryParseInt(args[1], out int level))
            {
                return GmCommandResult.Fail("大关或小关参数无效。");
            }

            if (RoguelikeMapServer.Instance == null)
            {
                return GmCommandResult.Fail("RoguelikeMapServer 未初始化。");
            }

            if (RoguelikeLevelConfigReader.Instance.Get(big, level) == null)
            {
                return GmCommandResult.Fail(string.Format("无肉鸽关卡配置 {0}-{1}。", big, level));
            }

            RoguelikeMapServer.Instance.mapModel.ForceUnlockLevel(big, level);
            if (!RoguelikeMapServer.Instance.EnterLevel(big, level))
            {
                return GmCommandResult.Fail(string.Format("进入肉鸽关卡 {0}-{1} 失败。", big, level));
            }

            RoguelikeLevelDef def = RoguelikeMapServer.Instance.GetLevelDef(big, level);
            return GmCommandResult.Ok(string.Format(
                "已进入肉鸽 {0}-{1}（{2}），shopPool={3}，startingGold={4}。",
                big,
                level,
                def != null ? def.displayName : "?",
                def != null ? def.shopPoolId : 0,
                def != null ? def.startingGold : 0));
        }

        static GmCommandResult HandleOpenRoguelikeMap(GmCommandContext ctx, string[] args)
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return GmCommandResult.Fail("openRoguelikeMap 需要在 Play 模式下执行。");
            }

            int big = 1;
            int level = 1;
            if (args.Length >= 1 && !TryParseInt(args[0], out big))
            {
                return GmCommandResult.Fail("大关参数无效: " + args[0]);
            }

            if (args.Length >= 2 && !TryParseInt(args[1], out level))
            {
                return GmCommandResult.Fail("小关参数无效: " + args[1]);
            }

            if (ViewManager.Instance == null)
            {
                return GmCommandResult.Fail("ViewManager 未初始化。");
            }

            ViewManager.Instance.OpenView<RoguelikeBigLevelPanel>();
            if (!ViewManager.Instance.viewTypeDic.TryGetValue(typeof(RoguelikeNormalLevelPanel), out BaseView levelView))
            {
                return GmCommandResult.Fail("RoguelikeNormalLevelPanel 未注册。");
            }

            var panel = (RoguelikeNormalLevelPanel)levelView;
            panel.OpenForBigLevel(big, level);
            ViewManager.Instance.OpenView<RoguelikeNormalLevelPanel>();
            return GmCommandResult.Ok(string.Format("已打开肉鸽选关界面，选中 {0}-{1}。", big, level));
        }

        static List<string> SplitTokens(string text)
        {
            var tokens = new List<string>();
            int i = 0;
            while (i < text.Length)
            {
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                if (i >= text.Length)
                {
                    break;
                }

                int start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                tokens.Add(text.Substring(start, i - start));
            }

            return tokens;
        }

        static bool TryParseInt(string text, out int value)
        {
            return int.TryParse(text, out value);
        }

        static bool TryParseByte(string text, out byte value)
        {
            if (!int.TryParse(text, out int n) || n < 0 || n > 255)
            {
                value = 0;
                return false;
            }

            value = (byte)n;
            return true;
        }

        static bool TryParseAllClear(string text, out byte value)
        {
            if (!TryParseByte(text, out byte n))
            {
                value = MapInfoType.NOT_ALL_CLEAR;
                return false;
            }

            value = n == 1 ? MapInfoType.ALL_CLEAR : MapInfoType.NOT_ALL_CLEAR;
            return true;
        }

        static bool TryParseUnlock(string text, out byte value)
        {
            if (!TryParseByte(text, out byte n))
            {
                value = MapInfoType.LOCK_LEVEL;
                return false;
            }

            value = n == 1 ? MapInfoType.UNLOCK_LEVEL : MapInfoType.LOCK_LEVEL;
            return true;
        }
    }
}
