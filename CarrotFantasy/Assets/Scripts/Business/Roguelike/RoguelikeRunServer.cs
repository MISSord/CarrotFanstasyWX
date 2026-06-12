using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 肉鸽 Run 层：肉鸽金币、背包、商店购买。生命周期跨越多次战斗与大地图节点。
    /// </summary>
    public class RoguelikeRunServer : BaseServer<RoguelikeRunServer>
    {
        public const int DefaultBattleVictoryGold = 50;
        public const int DefaultStartingGold = 100;

        public EventDispatcher eventDispatcher { get; private set; }
        public RoguelikeRunState ActiveRun { get; private set; }

        public bool IsRunActive
        {
            get { return this.ActiveRun != null && this.ActiveRun.isActive; }
        }

        public int PendingBattlePointId { get; private set; }
        public int PendingEncounterId { get; private set; }

        protected override void OnSingletonInit()
        {
            this.eventDispatcher = new EventDispatcher();
            RoguelikeItemConfigReader.Instance.Init();
            RoguelikeShopConfigReader.Instance.Init();
        }

        public override void LoadModule()
        {
            base.LoadModule();
            BusinessProvision.Instance.eventDispatcher.AddListener(CommonEventType.ENTER_ROGUELIKE_MAP, this.EnterRoguelikeMap);
        }

        void EnterRoguelikeMap()
        {
            ServerProvision.sceneServer.LoadScene(BaseSceneType.RoguelikeMapScene, null);
        }

        public override void Dispose()
        {
            if (BusinessProvision.Instance != null)
            {
                BusinessProvision.Instance.eventDispatcher.RemoveListener(CommonEventType.ENTER_ROGUELIKE_MAP, this.EnterRoguelikeMap);
            }
            this.ActiveRun = null;
            base.Dispose();
        }

        public void StartRun(int mapId, HexWorldProgress progress = null)
        {
            this.ActiveRun = new RoguelikeRunState
            {
                mapId = mapId,
                roguelikeGold = DefaultStartingGold,
                isActive = true,
            };
            if (progress != null)
            {
                this.ActiveRun.mapProgress = CloneProgress(progress);
            }
            else
            {
                this.ActiveRun.mapProgress.mapId = mapId;
            }

            this.PendingBattlePointId = 0;
            this.PendingEncounterId = 0;
            this.eventDispatcher.DispatchEvent(RoguelikeEvent.RUN_STARTED);
            this.DispatchGoldChanged(0);
        }

        public void EndRun(RoguelikeRunEndReason reason)
        {
            if (!this.IsRunActive)
            {
                return;
            }

            this.ActiveRun.isActive = false;
            this.eventDispatcher.DispatchEvent<RoguelikeRunEndReason>(RoguelikeEvent.RUN_ENDED, reason);
            this.ActiveRun = null;
        }

        public int RoguelikeGold
        {
            get { return this.IsRunActive ? this.ActiveRun.roguelikeGold : 0; }
        }

        public IReadOnlyList<int> OwnedItemIds
        {
            get
            {
                if (!this.IsRunActive)
                {
                    return Array.Empty<int>();
                }
                return this.ActiveRun.ownedItemIds;
            }
        }

        public void SetPendingBattle(int pointId, int encounterId)
        {
            this.PendingBattlePointId = pointId;
            this.PendingEncounterId = encounterId;
        }

        public void ClearPendingBattle()
        {
            this.PendingBattlePointId = 0;
            this.PendingEncounterId = 0;
        }

        public void SyncMapProgress(HexWorldProgress progress)
        {
            if (!this.IsRunActive || progress == null)
            {
                return;
            }
            this.ActiveRun.mapProgress = CloneProgress(progress);
        }

        public HexWorldProgress GetMapProgress()
        {
            if (!this.IsRunActive)
            {
                return null;
            }
            return CloneProgress(this.ActiveRun.mapProgress);
        }

        public bool AddRoguelikeGold(int amount, RoguelikeGoldSource source)
        {
            if (!this.IsRunActive || amount <= 0)
            {
                return false;
            }
            this.ActiveRun.roguelikeGold += amount;
            this.DispatchGoldChanged(amount);
            Debug.Log("[RoguelikeRun] Gold +" + amount + " (" + source + "), total=" + this.ActiveRun.roguelikeGold);
            return true;
        }

        public bool TrySpendRoguelikeGold(int amount)
        {
            if (!this.IsRunActive || amount <= 0 || this.ActiveRun.roguelikeGold < amount)
            {
                return false;
            }
            this.ActiveRun.roguelikeGold -= amount;
            this.DispatchGoldChanged(-amount);
            return true;
        }

        public bool TryAddItem(int itemId)
        {
            if (!this.IsRunActive || itemId <= 0)
            {
                return false;
            }
            if (!RoguelikeItemConfigReader.Instance.TryGet(itemId, out _))
            {
                return false;
            }
            if (this.ActiveRun.ownedItemIds.Contains(itemId))
            {
                return false;
            }
            this.ActiveRun.ownedItemIds.Add(itemId);
            this.eventDispatcher.DispatchEvent<int>(RoguelikeEvent.INVENTORY_CHANGED, itemId);
            return true;
        }

        public bool OwnsItem(int itemId)
        {
            return this.IsRunActive && this.ActiveRun.ownedItemIds.Contains(itemId);
        }

        public void SetActiveShopPoint(int shopPointId)
        {
            if (!this.IsRunActive)
            {
                return;
            }
            this.ActiveRun.activeShopPointId = shopPointId;
        }

        public void ClearActiveShop()
        {
            if (!this.IsRunActive)
            {
                return;
            }
            this.ActiveRun.activeShopPointId = 0;
        }

        public List<RoguelikeShopOffer> GetShopOffers(int shopPointId)
        {
            List<RoguelikeShopOffer> list = new List<RoguelikeShopOffer>();
            if (!this.IsRunActive)
            {
                return list;
            }

            int[] itemIds = RoguelikeShopConfigReader.Instance.GetItemIdsForShop(shopPointId);
            for (int i = 0; i < itemIds.Length; i++)
            {
                int itemId = itemIds[i];
                if (!RoguelikeItemConfigReader.Instance.TryGet(itemId, out RoguelikeItemDef def))
                {
                    continue;
                }
                list.Add(new RoguelikeShopOffer
                {
                    offerId = shopPointId * 1000 + itemId,
                    itemId = itemId,
                    price = def.price,
                    displayName = def.displayName,
                    soldOut = this.OwnsItem(itemId),
                });
            }
            return list;
        }

        public bool TryPurchase(int offerId)
        {
            if (!this.IsRunActive)
            {
                return false;
            }

            int itemId = offerId % 1000;
            if (!RoguelikeItemConfigReader.Instance.TryGet(itemId, out RoguelikeItemDef def))
            {
                return false;
            }
            if (this.OwnsItem(itemId))
            {
                return false;
            }
            if (!this.TrySpendRoguelikeGold(def.price))
            {
                return false;
            }
            if (!this.TryAddItem(itemId))
            {
                this.AddRoguelikeGold(def.price, RoguelikeGoldSource.Debug);
                return false;
            }

            this.eventDispatcher.DispatchEvent<int>(RoguelikeEvent.ITEM_PURCHASED, itemId);
            Debug.Log("[RoguelikeRun] Purchased item " + itemId + " (" + def.displayName + ")");
            return true;
        }

        /// <summary>汇总背包对单局战斗的加成（由 <see cref="BattleLauncher"/> 写入开战参数并由 <see cref="BattleGlobalBuffComponent"/> 应用）。</summary>
        public void CollectBattleModifiers(out int startCoinBonus, out int towerDamagePercentBonus)
        {
            startCoinBonus = 0;
            towerDamagePercentBonus = 0;
            if (!this.IsRunActive)
            {
                return;
            }

            for (int i = 0; i < this.ActiveRun.ownedItemIds.Count; i++)
            {
                if (!RoguelikeItemConfigReader.Instance.TryGet(this.ActiveRun.ownedItemIds[i], out RoguelikeItemDef def))
                {
                    continue;
                }
                startCoinBonus += def.startBattleCoinBonus;
                towerDamagePercentBonus += def.towerDamagePercentBonus;
            }
        }

        public void OnBattleVictory()
        {
            this.AddRoguelikeGold(DefaultBattleVictoryGold, RoguelikeGoldSource.BattleVictory);
        }

        void DispatchGoldChanged(int delta)
        {
            this.eventDispatcher.DispatchEvent<int>(RoguelikeEvent.GOLD_CHANGED, delta);
        }

        static HexWorldProgress CloneProgress(HexWorldProgress src)
        {
            if (src == null)
            {
                return new HexWorldProgress();
            }
            HexWorldProgress dst = new HexWorldProgress
            {
                mapId = src.mapId,
                currentPointId = src.currentPointId,
            };
            if (src.blockedPointIds != null)
            {
                dst.blockedPointIds = new List<int>(src.blockedPointIds);
            }
            if (src.consumedEnterPointIds != null)
            {
                dst.consumedEnterPointIds = new List<int>(src.consumedEnterPointIds);
            }
            if (src.leaveHandledPointIds != null)
            {
                dst.leaveHandledPointIds = new List<int>(src.leaveHandledPointIds);
            }
            return dst;
        }

    }
}
