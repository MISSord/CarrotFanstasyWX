using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    public class BusinessProvision
    {
        private Dictionary<string, BaseServer> businessDic = new Dictionary<string, BaseServer>();
        private List<BaseServer> businessList = new List<BaseServer>();

        public EventDispatcher eventDispatcher { get; private set; }
        private static BusinessProvision business;

        public bool IsGameQuit = false;

        public static BusinessProvision Instance
        {
            get
            {
                if (business == null)
                {
                    business = new BusinessProvision();
                    business.eventDispatcher = new EventDispatcher();
                }
                return business;
            }
        }

        public void Init()
        {
            this.businessDic.Add(BusinessType.UIServer, UIServer.Instance);
            this.businessDic.Add(BusinessType.AccountServer, AccountServer.Instance);
            this.businessDic.Add(BusinessType.SettingServer, SettingServer.Instance);

            //this.businessDic.Add(BusinessType.RoomServer, RoomServer.Instance);
            //this.businessDic.Add(BusinessType.BattleParamServer, BattleParamServer.Instance);

            this.eventDispatcher.AddListener(CommonEventType.GAME_QUIT, this.Dispose);
        }

        public void LoadBusiness()
        {
            foreach (KeyValuePair<String, BaseServer> info in businessDic)
            {
                info.Value.LoadModule();
                info.Value.AddSocketListener();
                this.businessList.Add(info.Value);
            }
        }

        public void ReloadBusiness()
        {
            foreach (KeyValuePair<string, BaseServer> info in businessDic)
            {
                info.Value.ReloadModule();
            }
        }

        public void Dispose()
        {
            for (int i = this.businessList.Count - 1; i >= 0; i--)
            {
                this.businessList[i].RemoveSocketListener();
                this.businessList[i].Dispose();
            }
            this.businessDic.Clear();
            this.businessList.Clear();

            ServerProvision.connectionServer.Dispose();
            ServerProvision.sceneServer.Dispose();

            this.IsGameQuit = true;
        }
    }
}
