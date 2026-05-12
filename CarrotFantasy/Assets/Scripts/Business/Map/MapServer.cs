using System;
using CfNet;
using UnityEngine;

namespace CarrotFantasy
{
    public class MapServer : BaseServer<MapServer>
    {
        public String account;
        public MapModel mapModel;
        public EventDispatcher eventDispatcher;

        public SingleMapInfo unSaveMapInfo;

        private MapBigLevelPanel mapBigLevelPanel;
        private MapNormalLevelPanel mapNormalLevelPanel;

        public int curBigLevel { get; private set; }
        public int curLevel { get; private set; }

        protected override void OnSingletonInit()
        {
            eventDispatcher = new EventDispatcher();
            mapModel = new MapModel(eventDispatcher);
        }

        public override void LoadModule()
        {
            base.LoadModule();
            this.AddAccountListener();

            this.curBigLevel = 0;
            this.curLevel = 0;

            mapBigLevelPanel = new MapBigLevelPanel();
            mapBigLevelPanel.RegisterData();

            mapNormalLevelPanel = new MapNormalLevelPanel();
            mapNormalLevelPanel.RegisterData();
        }

        private void AddAccountListener()
        {
            AccountServer.Instance.eventDispatcher.AddListener(AccountServer.LOGIN_SUCCESS, this.OnLoginSuccessBootstrapMap);
        }

        private void RemoveAccountListener()
        {
            AccountServer.Instance.eventDispatcher.RemoveListener(AccountServer.LOGIN_SUCCESS, this.OnLoginSuccessBootstrapMap);
        }

        public override void AddSocketListener()
        {
            ConnectionServer cs = ServerProvision.connectionServer;
            if (cs != null)
            {
                cs.AddProtobufListener(SimpleBinaryOpcodes.GetUserMapResponse, GetUserMapResponse.Parser, this.OnGetUserMapResponseProto);
                cs.AddProtobufListener(SimpleBinaryOpcodes.SetSingleMapResponse, SetSingleMapResponse.Parser, this.OnSetSingleMapResponseProto);
            }
        }

        public override void RemoveSocketListener()
        {
            ConnectionServer cs = ServerProvision.connectionServer;
            if (cs != null)
            {
                cs.RemoveProtobufListener(SimpleBinaryOpcodes.GetUserMapResponse);
                cs.RemoveProtobufListener(SimpleBinaryOpcodes.SetSingleMapResponse);
            }
        }

        public override void Dispose()
        {
            this.RemoveAccountListener();
            this.mapModel.Dispose();
        }

        /// <summary>应用地图快照（服务端 GetUserMap 或本地默认）。</summary>
        public void ApplyMapSnapshot(string mapSnapshot)
        {
            if (string.IsNullOrEmpty(mapSnapshot))
            {
                mapSnapshot = MapInfoHelper.GetInitMapInfo();
            }

            this.mapModel.ParseMapInfo(mapSnapshot);
        }

        /// <summary>
        /// 登录成功后的地图：未连接时用本地初始图；已连接时先铺默认档，再由服务端登录后主动推送的 203 覆盖。
        /// 仍可通过客户端发 202 主动拉图（与推送负载一致）。
        /// </summary>
        private void OnLoginSuccessBootstrapMap()
        {
            this.ApplyMapSnapshot(MapInfoHelper.GetInitMapInfo());
        }

        private void OnGetUserMapResponseProto(GetUserMapResponse msg)
        {
            if (msg.Result != 0)
            {
                Debug.LogWarning(string.Format("拉取地图失败: {0}", msg.Message));
                this.ApplyMapSnapshot(MapInfoHelper.GetInitMapInfo());
                return;
            }

            this.ApplyMapSnapshot(msg.MapSnapshot);
        }

        /// <summary>
        /// 联网：仅上报，等 <see cref="OnSetSingleMapResponseProto"/> 成功后再写本关数据并按服务端下发的解锁字段开下一关。
        /// 离线：本地立即写本关 + 客户端推算下一关解锁。
        /// </summary>
        public void SendSetSingleMapInfo(SingleMapInfo unSaveMapInfo)
        {
            this.unSaveMapInfo = unSaveMapInfo;

            ConnectionServer cs = ServerProvision.connectionServer;
            if (cs == null || !cs.IsTransportConnected || AccountServer.Instance.userId == 0)
            {
                this.ApplyVictoryOffline(unSaveMapInfo);
                this.unSaveMapInfo = null;
                return;
            }

            try
            {
                var req = new SetSingleMapRequest
                {
                    UserId = AccountServer.Instance.userId,
                    BigLevelId = unSaveMapInfo.bigLevelId,
                    LevelId = unSaveMapInfo.levelId,
                    CarrotState = unSaveMapInfo.carrotState,
                    IsAllClear = unSaveMapInfo.isAllClear,
                };
                cs.SendProtobuf(SimpleBinaryOpcodes.SetSingleMapRequest, req);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(string.Format("SendSetSingleMapInfo: {0}", ex.Message));
                UIServer.Instance.ShowTip("同步失败，请检查网络后重试");
                this.unSaveMapInfo = null;
            }
        }

        public void SendGameMapInfo(int bigLevel, int level)
        {
            this.curBigLevel = bigLevel;
            this.curLevel = level;
        }

        private void OnSetSingleMapResponseProto(SetSingleMapResponse msg)
        {
            SingleMapInfo pending = this.unSaveMapInfo;
            if (pending == null)
            {
                return;
            }

            if (msg.Result != 0)
            {
                Debug.LogWarning(string.Format("保存地图失败: {0}", msg.Message));
                UIServer.Instance.ShowTip(string.IsNullOrEmpty(msg.Message) ? "存档同步失败" : msg.Message);
                this.unSaveMapInfo = null;
                return;
            }

            // 服务端已持久化上一关数据后再应用本关结算，避免与服务器不一致
            this.mapModel.UpdateSingleMapInfo(pending);

            // 下一关解锁仅以服务端响应为准（含「无下一关」时 BigLevelId/LevelId 为 0）
            if (msg.BigLevelId != 0 && msg.LevelId != 0 && msg.Unlocked != 0)
            {
                this.mapModel.UpdateSingleMapInfoUnLockState(msg.BigLevelId, msg.LevelId, (byte)msg.Unlocked);
            }

            this.unSaveMapInfo = null;
        }

        /// <summary>无网络或未登录：本机完整结算（含下一关解锁推算）。</summary>
        private void ApplyVictoryOffline(SingleMapInfo progress)
        {
            if (progress == null)
            {
                return;
            }

            this.mapModel.UpdateSingleMapInfo(progress);
            (int nb, int nl) = NextLevel(progress.bigLevelId, progress.levelId);
            if (nb != 0)
            {
                this.mapModel.UpdateSingleMapInfoUnLockState(nb, nl, MapInfoType.UNLOCK_LEVEL);
            }
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
    }
}
