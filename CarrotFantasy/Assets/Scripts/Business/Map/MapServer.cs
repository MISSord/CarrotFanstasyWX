using System;


namespace CarrotFantasy
{
    public class MapServer : BaseServer
    {
        private static MapServer mapServer;
        public String account;
        public MapModel mapModel;
        public EventDispatcher eventDispatcher;

        public SingleMapInfo unSaveMapInfo;

        public int curBigLevel { get; private set; }
        public int curLevel { get; private set; }

        public static MapServer Instance
        {
            get
            {
                if (mapServer == null)
                {
                    mapServer = new MapServer();
                    mapServer.eventDispatcher = new EventDispatcher();
                    mapServer.mapModel = new MapModel(mapServer.eventDispatcher);
                }
                return mapServer;
            }
        }

        public override void LoadModule()
        {
            base.LoadModule();
            this.AddListener();

            this.curBigLevel = 0;
            this.curLevel = 0;
        }

        private void AddListener()
        {
            AccountServer.Instance.eventDispatcher.AddListener(AccountServer.LOGIN_SUCCESS, this.sendGetUserInfo);
        }

        private void RemoveListener()
        {
            AccountServer.Instance.eventDispatcher.RemoveListener(AccountServer.LOGIN_SUCCESS, this.sendGetUserInfo);
        }

        public override void AddSocketListener()
        {
            //ServerProvision.connectionServer.AddListener(HotfixOpcode.A1001_GetUserInfo_G2C, this.notifyUserInfo);
            //ServerProvision.connectionServer.AddListener(HotfixOpcode.A1002_SetSingleMapInfo_G2C, this.notifySetSingleMapInfo);
            //ServerProvision.connectionServer.AddListener(HotfixOpcode.A1003_GameMapInfo_G2C, this.notifyGameMapInfo);
        }

        public override void RemoveSocketListener()
        {
            //ServerProvision.connectionServer.RemoveListener(HotfixOpcode.A1001_GetUserInfo_G2C, this.notifyUserInfo);
            //ServerProvision.connectionServer.RemoveListener(HotfixOpcode.A1002_SetSingleMapInfo_G2C, this.notifySetSingleMapInfo);
            //ServerProvision.connectionServer.RemoveListener(HotfixOpcode.A1003_GameMapInfo_G2C, this.notifyGameMapInfo);
        }

        //private void notifyUserInfo(IMessage message)
        //{
        //    A1001_GetUserInfo_G2C msg = (A1001_GetUserInfo_G2C)message;
        //    if (msg.Error == ErrorCode.ERR_Success)
        //    {
        //        this.mapModel.parseMapInfo(msg.MapInfo);
        //    }
        //}

        //private void notifySetSingleMapInfo(IMessage message)
        //{
        //    A1002_SetSingleMapInfo_G2C msg = (A1002_SetSingleMapInfo_G2C)message;
        //    if (msg.Error == ErrorCode.ERR_Success)
        //    {
        //        this.mapModel.updateSingleMapInfo(this.unSaveMapInfo);
        //        this.mapModel.updateSingleMapInfoUnLockState(msg.BigLevelId, msg.LevelId, msg.UnLocked);
        //        this.unSaveMapInfo = null;
        //    }
        //}

        //private void notifyGameMapInfo(IMessage message)
        //{
        //    A1003_GameMapInfo_G2C msg = (A1003_GameMapInfo_G2C)message;
        //    if (msg.Error == ErrorCode.ERR_Success)
        //    {
        //        //this.eventDispatcher.DispatchEvent(MapEventType.CAN_START_GAME);
        //        BusinessProvision.Instance.eventDispatcher.DispatchEvent(CommonEventType.READY_START_PVE_GAME);
        //        ServerProvision.sceneServer.loadScene(BaseSceneType.BattleScene, null);
        //        UIServer.Instance.ShowLoadingPanel();
        //    }
        //    else
        //    {
        //        //this.eventDispatcher.DispatchEvent(MapEventType.CANT_START_GAME);
        //    }
        //}


        public override void Dispose()
        {
            this.mapModel.Dispose();
            this.RemoveListener();
        }

        public void sendGetUserInfo()
        {
            //ServerProvision.connectionServer.Send(new A1001_GetUserInfo_C2G());
        }

        public void sendSetSingleMapInfo(SingleMapInfo unSaveMapInfo)
        {
            //A1002_SetSingleMapInfo_C2G msg = new A1002_SetSingleMapInfo_C2G();
            //this.unSaveMapInfo = unSaveMapInfo;
            //msg.BigLevelId = unSaveMapInfo.bigLevelId;
            //msg.LevelId = unSaveMapInfo.levelId;
            //msg.CarrotState = unSaveMapInfo.carrotState;
            //msg.IsAllClear = unSaveMapInfo.isAllClear;
            //msg.UnLocked = unSaveMapInfo.unLocked;
            //ServerProvision.connectionServer.Send(msg);
        }

        public void sendGameMapInfo(int bigLevel, int level)
        {
            //A1003_GameMapInfo_C2G msg = new A1003_GameMapInfo_C2G();
            //msg.BigLevelId = bigLevel;
            //msg.LevelId = level;

            //this.curBigLevel = bigLevel;
            //this.curLevel = level;

            //ServerProvision.connectionServer.Send(msg);
        }
    }
}
