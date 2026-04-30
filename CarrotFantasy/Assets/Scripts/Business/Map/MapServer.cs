using System;


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
            this.AddListener();

            this.curBigLevel = 0;
            this.curLevel = 0;

            mapBigLevelPanel = new MapBigLevelPanel();
            mapBigLevelPanel.RegisterData();

            mapNormalLevelPanel = new MapNormalLevelPanel();
            mapNormalLevelPanel.RegisterData();
        }

        private void AddListener()
        {
            AccountServer.Instance.eventDispatcher.AddListener(AccountServer.LOGIN_SUCCESS, this.SendGetUserInfo);
        }

        private void RemoveListener()
        {
            AccountServer.Instance.eventDispatcher.RemoveListener(AccountServer.LOGIN_SUCCESS, this.SendGetUserInfo);
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

        public void SendGetUserInfo()
        {
            //ServerProvision.connectionServer.Send(new A1001_GetUserInfo_C2G());
        }

        public void SendSetSingleMapInfo(SingleMapInfo unSaveMapInfo)
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

        public void SendGameMapInfo(int bigLevel, int level)
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
