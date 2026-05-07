using System;
using Google.Protobuf;
using UnityEngine;

namespace CarrotFantasy
{
    public class RoomServer : BaseServer<RoomServer>
    {
        public EventDispatcher eventDispatcher;

        public Gamer partner;
        public Gamer myself;
        public bool isMatching { get; set; }
        private int localIndex = -1;

        protected override void OnSingletonInit()
        {
            eventDispatcher = new EventDispatcher();
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

        private void NotifyStartMatch(IMessage message)
        {

        }

        private void NotifyGamerEnterRoom(IMessage message)
        {
            Debug.Log("有人加入房间");
        }

        private void NotifyGamerExitRoom(IMessage message)
        {
            Debug.Log("有人离开房间");
        }

        private void NotifyGamerReadyFight(IMessage message)
        {
        }

        public void SendStartMatch()
        {
            //ServerProvision.connectionServer.Send(new C2G_StartMatch_Req());
            //this.myself = new Gamer(AccountServer.Instance.userId);
            //this.myself.isReady = false;
        }

        public void SendReadyFight()
        {
            //ServerProvision.connectionServer.Send(new Actor_GamerReady_Landlords());
        }

        public void SendCanelMatch()
        {
            //ServerProvision.connectionServer.Send(new C2G_ReturnLobby_Ntt());
        }

        public void CanelMatch()
        {
            this.SendCanelMatch();
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
