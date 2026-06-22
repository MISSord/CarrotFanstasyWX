using System;
using CfNet;
using Google.Protobuf;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 单机模式下模拟服务端下发的登录与地图数据（默认初始存档，与 <see cref="MapInfoHelper.GetInitMapInfo"/> 一致）。
    /// </summary>
    public static class StandaloneBackendMock
    {
        public const long DefaultUserId = 1;
        public const string DefaultAccount = "standalone";

        private static long sessionUserId;
        private static string mapSnapshot = string.Empty;
        private static bool hasSession;

        public static bool HasSession => hasSession && StandaloneGameConfig.EnableStandaloneMode;

        public static string GetDefaultMapSnapshot()
        {
            return MapInfoHelper.GetInitMapInfo();
        }

        /// <summary>GM / 调试：覆盖内存快照并同步 MapServer（Play 模式）。</summary>
        public static void ApplyMapSnapshotForGm(string snapshot, long userId)
        {
            if (!StandaloneGameConfig.EnableStandaloneMode || string.IsNullOrEmpty(snapshot))
            {
                return;
            }

            if (userId > 0)
            {
                sessionUserId = userId;
                hasSession = true;
            }

            mapSnapshot = snapshot;
            StandaloneMapPersistence.Save(sessionUserId > 0 ? sessionUserId : DefaultUserId, snapshot);
        }

        /// <summary>业务模块加载完成后调用：写入默认账号、地图并触发与线上一致的登录成功事件。</summary>
        public static void BootstrapDefaultSession()
        {
            if (!StandaloneGameConfig.EnableStandaloneMode)
            {
                return;
            }

            ApplySession(DefaultAccount, DefaultUserId, GetDefaultMapSnapshot());
            Debug.Log("[Standalone] 已启用单机模式，使用默认地图存档。");
        }

        /// <summary>模拟登录请求（登录面板在单机模式下走此入口）。</summary>
        public static void SimulateLogin(string account, string password)
        {
            if (!StandaloneGameConfig.EnableStandaloneMode)
            {
                return;
            }

            if (string.IsNullOrEmpty(account))
            {
                UIServer.Instance.ShowTip("账号不能为空");
                return;
            }

            long userId = DeriveUserId(account);
            ApplySession(account, userId, GetDefaultMapSnapshot());
            UIServer.Instance.ShowTip("单机模式：已使用本地默认存档");
        }

        /// <summary>
        /// 拦截客户端发出的 Protobuf 请求并本地应答。返回 true 表示已处理，不再走网络。
        /// </summary>
        public static bool TryHandleClientRequest<T>(ushort opcode, T message) where T : Google.Protobuf.IMessage
        {
            if (!StandaloneGameConfig.EnableStandaloneMode || !hasSession)
            {
                return false;
            }

            ConnectionServer cs = ServerProvision.connectionServer;
            if (cs == null)
            {
                return false;
            }

            switch (opcode)
            {
                case SimpleBinaryOpcodes.LoginRequest:
                    return HandleLoginRequest(cs, message as LoginRequest);
                case SimpleBinaryOpcodes.GetUserMapRequest:
                    return HandleGetUserMapRequest(cs, message as GetUserMapRequest);
                case SimpleBinaryOpcodes.SetSingleMapRequest:
                    return HandleSetSingleMapRequest(cs, message as SetSingleMapRequest);
                default:
                    return false;
            }
        }

        private static void ApplySession(string account, long userId, string snapshot)
        {
            sessionUserId = userId;
            string persisted = StandaloneMapPersistence.TryLoad(userId);
            if (!string.IsNullOrEmpty(persisted))
            {
                mapSnapshot = persisted;
            }
            else
            {
                mapSnapshot = string.IsNullOrEmpty(snapshot) ? GetDefaultMapSnapshot() : snapshot;
            }

            hasSession = true;

            AccountServer.Instance.ApplyStandaloneSession(account, userId);
            MapServer.Instance.ApplyMapSnapshot(mapSnapshot);
            AccountServer.Instance.eventDispatcher.DispatchEvent(AccountServer.LOGIN_SUCCESS);
        }

        private static long DeriveUserId(string account)
        {
            if (string.Equals(account, DefaultAccount, StringComparison.OrdinalIgnoreCase))
            {
                return DefaultUserId;
            }

            unchecked
            {
                long id = 0;
                for (int i = 0; i < account.Length; i++)
                {
                    id = id * 31 + account[i];
                }

                if (id <= 0)
                {
                    id = DefaultUserId;
                }

                return id;
            }
        }

        private static bool HandleLoginRequest(ConnectionServer cs, LoginRequest req)
        {
            if (req == null)
            {
                return false;
            }

            long userId = DeriveUserId(req.Account ?? string.Empty);
            sessionUserId = userId;
            hasSession = true;
            string persisted = StandaloneMapPersistence.TryLoad(userId);
            mapSnapshot = !string.IsNullOrEmpty(persisted) ? persisted : GetDefaultMapSnapshot();

            DispatchLoginSuccess(cs, userId);
            DispatchMapSnapshot(cs);
            return true;
        }

        private static bool HandleGetUserMapRequest(ConnectionServer cs, GetUserMapRequest req)
        {
            if (req == null || req.UserId != sessionUserId)
            {
                DispatchGetUserMap(cs, 403, string.Empty, "用户不匹配");
                return true;
            }

            DispatchGetUserMap(cs, 0, mapSnapshot, "ok");
            return true;
        }

        private static bool HandleSetSingleMapRequest(ConnectionServer cs, SetSingleMapRequest req)
        {
            if (req == null)
            {
                return false;
            }

            if (req.UserId != sessionUserId)
            {
                DispatchSetSingleMap(cs, 403, 0, 0, 0, "用户不匹配");
                return true;
            }

            if (req.BigLevelId < 1 || req.BigLevelId > 3 || req.LevelId < 1 || req.LevelId > 5)
            {
                DispatchSetSingleMap(cs, 400, 0, 0, 0, "关卡参数无效");
                return true;
            }

            try
            {
                (int nextBig, int nextSmall) = ApplyVictoryToSnapshot(
                    req.BigLevelId,
                    req.LevelId,
                    req.CarrotState,
                    req.IsAllClear);
                int unlockFlag = nextBig == 0 ? 0 : 1;
                DispatchSetSingleMap(cs, 0, nextBig, nextSmall, unlockFlag, "保存成功");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Standalone] 保存地图失败: " + ex.Message);
                DispatchSetSingleMap(cs, 500, 0, 0, 0, "本地保存失败");
            }

            return true;
        }

        private static (int nextBig, int nextSmall) ApplyVictoryToSnapshot(
            int bigLevelId,
            int levelId,
            int carrotState,
            int isAllClear)
        {
            var progress = new SingleMapInfo
            {
                bigLevelId = (byte)bigLevelId,
                levelId = (byte)levelId,
                carrotState = (byte)carrotState,
                isAllClear = (byte)isAllClear,
                unLocked = MapInfoType.UNLOCK_LEVEL,
            };

            mapSnapshot = MapInfoHelper.GetNewMapInfo(mapSnapshot, progress);
            (int nb, int nl) = NextLevel(bigLevelId, levelId);
            if (nb != 0)
            {
                var unlock = new SingleMapInfo
                {
                    bigLevelId = (byte)nb,
                    levelId = (byte)nl,
                    carrotState = 0,
                    isAllClear = MapInfoType.NOT_ALL_CLEAR,
                    unLocked = MapInfoType.UNLOCK_LEVEL,
                };
                mapSnapshot = MapInfoHelper.GetNewMapInfo(mapSnapshot, unlock);
            }

            StandaloneMapPersistence.Save(sessionUserId, mapSnapshot);

            return (nb, nl);
        }

        private static (int big, int level) NextLevel(int big, int level)
        {
            if (level < 5)
            {
                return (big, level + 1);
            }

            if (big < 3)
            {
                return (big + 1, 1);
            }

            return (0, 0);
        }

        private static void DispatchLoginSuccess(ConnectionServer cs, long userId)
        {
            var resp = new LoginResponse
            {
                Result = 0,
                UserId = userId,
                Message = "单机模式登录成功",
            };
            cs.DispatchPacket(ConnectionBinaryFrame.Encode(SimpleBinaryOpcodes.LoginResponse, resp.ToByteArray()));
        }

        private static void DispatchMapSnapshot(ConnectionServer cs)
        {
            DispatchGetUserMap(cs, 0, mapSnapshot, "standalone_push");
        }

        private static void DispatchGetUserMap(ConnectionServer cs, int result, string snapshot, string message)
        {
            var resp = new GetUserMapResponse
            {
                Result = result,
                MapSnapshot = snapshot ?? string.Empty,
                Message = message ?? string.Empty,
            };
            cs.DispatchPacket(ConnectionBinaryFrame.Encode(SimpleBinaryOpcodes.GetUserMapResponse, resp.ToByteArray()));
        }

        private static void DispatchSetSingleMap(
            ConnectionServer cs,
            int result,
            int bigLevelId,
            int levelId,
            int unlocked,
            string message)
        {
            var resp = new SetSingleMapResponse
            {
                Result = result,
                BigLevelId = bigLevelId,
                LevelId = levelId,
                Unlocked = unlocked,
                Message = message ?? string.Empty,
            };
            cs.DispatchPacket(ConnectionBinaryFrame.Encode(SimpleBinaryOpcodes.SetSingleMapResponse, resp.ToByteArray()));
        }
    }
}
