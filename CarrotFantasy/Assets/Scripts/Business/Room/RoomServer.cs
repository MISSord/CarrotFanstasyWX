using System;
using ETModel;
using UnityEngine;

namespace CarrotFantasy 
{
    public class RoomServer : BaseServer
    {
        private static RoomServer roomServer;
        public EventDispatcher eventDispatcher;

        public Gamer partner;
        public Gamer myself;
        public bool isMatching { get; set; }
        private int localIndex = -1;

        public static RoomServer Instance
        {
            get
            {
                if (roomServer == null)
                {
                    roomServer = new RoomServer();
                    roomServer.eventDispatcher = new EventDispatcher();
                }
                return roomServer;
            }
        }

        public override void LoadModule()
        {
            base.LoadModule();
        }

        public override void ReloadModule()
        {
            base.ReloadModule();
        }

        public override void AddSocketListener()
        {
            //ServerProvision.connectionServer.AddListener(HotfixOpcode.G2C_StartMatch_Back, this.notifyStartMatch);
            //ServerProvision.connectionServer.AddListener(HotfixOpcode.Actor_GamerEnterRoom_Ntt, this.notifyGamerEnterRoom);
            //ServerProvision.connectionServer.AddListener(HotfixOpcode.Actor_GamerExitRoom_Ntt, this.notifyGamerExitRoom);
            //ServerProvision.connectionServer.AddListener(HotfixOpcode.Actor_GamerReady_Landlords, this.notifyGamerReadyFight);
        }

        public override void RemoveSocketListener()
        {
            //ServerProvision.connectionServer.RemoveListener(HotfixOpcode.G2C_StartMatch_Back, this.notifyStartMatch);
            //ServerProvision.connectionServer.RemoveListener(HotfixOpcode.Actor_GamerEnterRoom_Ntt, this.notifyGamerEnterRoom);
            //ServerProvision.connectionServer.RemoveListener(HotfixOpcode.Actor_GamerExitRoom_Ntt, this.notifyGamerExitRoom);
            //ServerProvision.connectionServer.RemoveListener(HotfixOpcode.Actor_GamerReady_Landlords, this.notifyGamerReadyFight);
        }

        private void notifyStartMatch(IMessage message)
        {

        }

        private void notifyGamerEnterRoom(IMessage message) //就两个位置，
        {
            Debug.Log("有人加入房间");
            Actor_GamerEnterRoom_Ntt msg = (Actor_GamerEnterRoom_Ntt)message;
            if(this.isMatching == true)
            {
                this.isMatching = false;
            }
            if (msg.Gamers.count >= 3)
            {
                Debug.Log("队伍人数不对");
                return;
            }
            for (int i = 0; i < msg.Gamers.count - 1; i++)
            {
                GamerInfo gamerInfo = msg.Gamers[i];
                if (gamerInfo.UserID == 0) continue;
                if (gamerInfo.UserID == AccountServer.Instance.userId) continue;
                this.partner = new Gamer(gamerInfo.UserID);
            }
            this.eventDispatcher.DispatchEvent(RoomEventType.USER_INFO_CHANGE);
        }

        private void notifyGamerExitRoom(IMessage message)
        {
            Debug.Log("有人离开房间");
            Actor_GamerExitRoom_Ntt msg = (Actor_GamerExitRoom_Ntt)message;
            if (partner.UserID == msg.UserID) partner = null;
            if (myself.UserID == msg.UserID) myself = null;
            this.eventDispatcher.DispatchEvent(RoomEventType.USER_INFO_CHANGE);
        }

        private void notifyGamerReadyFight(IMessage message)
        {
            Actor_GamerReady_Landlords msg = (Actor_GamerReady_Landlords)message;
            bool isChange = false;
            if(partner != null)
            {
                if (msg.UserID == partner.UserID)
                {
                    partner.isReady = true;
                    isChange = true;
                }
            }
            else if (msg.UserID == myself.UserID)
            {
                myself.isReady = true;
                isChange = true;
            }
            if(isChange == true)
            {
                this.eventDispatcher.DispatchEvent(RoomEventType.USER_INFO_CHANGE);
            }else
            {
                if (partner == null) return;
                Debug.Log(String.Format("发送的账号有问题，我{0}，同伴{1}，发送的{2}", myself.UserID, partner.UserID, msg.UserID));
            }
            
        }

        public void sendStartMatch()
        {
            //ServerProvision.connectionServer.Send(new C2G_StartMatch_Req());
            //this.myself = new Gamer(AccountServer.Instance.userId);
            //this.myself.isReady = false;
        }

        public void sendReadyFight()
        {
            //ServerProvision.connectionServer.Send(new Actor_GamerReady_Landlords());
        }

        public void sendCanelMatch()
        {
            //ServerProvision.connectionServer.Send(new C2G_ReturnLobby_Ntt());
        }

        public void canelMatch()
        {
            this.sendCanelMatch();
            this.isMatching = false;
            this.myself = null;
            this.partner = null;
        }

        public override void Dispose()
        {
            base.Dispose();
        }

    }
}
