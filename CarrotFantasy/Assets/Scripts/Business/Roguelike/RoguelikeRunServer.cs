using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 肉鸽 Run 层：肉鸽金币、背包、商店购买。生命周期跨越多次战斗与大地图节点。
    /// 选关/解锁由 <see cref="RoguelikeMapServer"/> 负责。
    /// </summary>
    public class RoguelikeRunServer : BaseServer<RoguelikeRunServer>
    {
        public const int DefaultBattleVictoryGold = 50;
        public const int DefaultStartingGold = 100;

        public EventDispatcher eventDispatcher { get; private set; }
        public RoguelikeRunState ActiveRun { get; private set; }

        readonly RoguelikeBattleModifiers battleModsCache = new RoguelikeBattleModifiers();

        public bool IsRunActive
        {
            get { return this.ActiveRun != null && this.ActiveRun.isActive; }
        }

        public int PendingBattlePointId { get; private set; }
        public int PendingEncounterId { get; private set; }

        protected override void OnSingletonInit()
        {
            this.eventDispatcher = new EventDispatcher();
            RoguelikeEffectConfigReader.Instance.Init();
            RoguelikeItemConfigReader.Instance.Init();
            RoguelikeShopConfigReader.Instance.Init();
        }

        public override void LoadModule()
        {
            base.LoadModule();
        }

        public override void Dispose()
        {
            this.ActiveRun = null;
            base.Dispose();
        }

        /// <summary>用选关快照开一局；旧的仅 mapId 入口请走 <see cref="StartRunFromMapIdFallback"/>。</summary>
        public void StartRun(RoguelikeRunStartParams startParams)
        {
            if (startParams == null)
            {
                Debug.LogError("[RoguelikeRunServer] StartRun params is null.");
                return;
            }

            int gold = startParams.startingGold > 0 ? startParams.startingGold : DefaultStartingGold;
            this.ActiveRun = new RoguelikeRunState
            {
                bigLevelId = startParams.bigLevelId,
                levelId = startParams.levelId,
                mapId = startParams.mapId,
                hexMapAssetId = startParams.hexMapAssetId,
                shopPoolId = startParams.shopPoolId,
                encounterTableId = startParams.encounterTableId,
                randomEventPoolId = startParams.randomEventPoolId,
                runSeed = startParams.runSeed,
                roguelikeGold = gold,
                isActive = true,
            };

            if (startParams.startingEffectIds != null)
            {
                for (int i = 0; i < startParams.startingEffectIds.Length; i++)
                {
                    int effectId = startParams.startingEffectIds[i];
                    if (effectId > 0 && !this.ActiveRun.startingEffectIds.Contains(effectId))
                    {
                        this.ActiveRun.startingEffectIds.Add(effectId);
                    }
                }
            }

            int bonusGold = RoguelikeEffectCompiler.SumStartingRoguelikeGold(this.ActiveRun.startingEffectIds);
            if (bonusGold > 0)
            {
                this.ActiveRun.roguelikeGold += bonusGold;
            }

            if (startParams.mapProgress != null)
            {
                this.ActiveRun.mapProgress = CloneProgress(startParams.mapProgress);
            }
            else
            {
                this.ActiveRun.mapProgress.mapId = startParams.mapId;
            }

            this.PendingBattlePointId = 0;
            this.PendingEncounterId = 0;
            this.eventDispatcher.DispatchEvent(RoguelikeEvent.RUN_STARTED);
            this.DispatchGoldChanged(0);
            Debug.Log(
                "[RoguelikeRun] StartRun " + startParams.bigLevelId + "-" + startParams.levelId +
                " shopPool=" + startParams.shopPoolId +
                " gold=" + this.ActiveRun.roguelikeGold +
                " effects=" + this.ActiveRun.startingEffectIds.Count);
        }

        /// <summary>兼容旧调用：仅有 mapId 时按默认金币开局（无章节配置）。</summary>
        public void StartRunFromMapIdFallback(int mapId, HexWorldProgress progress = null)
        {
            this.StartRun(new RoguelikeRunStartParams
            {
                bigLevelId = 0,
                levelId = 0,
                mapId = mapId,
                shopPoolId = 0,
                startingGold = DefaultStartingGold,
                startingEffectIds = Array.Empty<int>(),
                mapProgress = progress,
            });
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

        public IReadOnlyList<int> StartingEffectIds
        {
            get
            {
                if (!this.IsRunActive)
                {
                    return Array.Empty<int>();
                }
                return this.ActiveRun.startingEffectIds;
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

        public int CountOwnedItem(int itemId)
        {
            if (!this.IsRunActive || itemId <= 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < this.ActiveRun.ownedItemIds.Count; i++)
            {
                if (this.ActiveRun.ownedItemIds[i] == itemId)
                {
                    count++;
                }
            }
            return count;
        }

        public bool OwnsItem(int itemId)
        {
            return this.CountOwnedItem(itemId) > 0;
        }

        public bool IsItemSoldOut(int itemId)
        {
            if (!RoguelikeItemConfigReader.Instance.TryGet(itemId, out RoguelikeItemDef def))
            {
                return true;
            }
            return this.CountOwnedItem(itemId) >= def.maxOwn;
        }

        public bool TryAddItem(int itemId)
        {
            if (!this.IsRunActive || itemId <= 0)
            {
                return false;
            }
            if (!RoguelikeItemConfigReader.Instance.TryGet(itemId, out RoguelikeItemDef def))
            {
                return false;
            }
            if (this.CountOwnedItem(itemId) >= def.maxOwn)
            {
                return false;
            }
            this.ActiveRun.ownedItemIds.Add(itemId);
            this.eventDispatcher.DispatchEvent<int>(RoguelikeEvent.INVENTORY_CHANGED, itemId);
            return true;
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

            int seed = this.ActiveRun.runSeed ^ (shopPointId * 397);
            int[] itemIds = RoguelikeShopConfigReader.Instance.ResolveShelfItemIds(
                this.ActiveRun.shopPoolId,
                seed);
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
                    soldOut = this.IsItemSoldOut(itemId),
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
            if (this.IsItemSoldOut(itemId))
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

        /// <summary>汇总开局效果 + 背包道具效果，写入开战加成。</summary>
        public void CollectBattleModifiers(RoguelikeBattleModifiers mods)
        {
            if (mods == null)
            {
                return;
            }

            mods.Clear();
            if (!this.IsRunActive)
            {
                return;
            }

            RoguelikeEffectCompiler.CompileEffectIds(this.ActiveRun.startingEffectIds, mods);
            RoguelikeEffectCompiler.CompileItemIds(this.ActiveRun.ownedItemIds, mods);
        }

        /// <summary>兼容旧签名：只返回金币/塔伤；全局 Buff 请用 <see cref="CollectBattleModifiers(RoguelikeBattleModifiers)"/>。</summary>
        public void CollectBattleModifiers(out int startCoinBonus, out int towerDamagePercentBonus)
        {
            this.CollectBattleModifiers(this.battleModsCache);
            startCoinBonus = this.battleModsCache.StartCoinBonus;
            towerDamagePercentBonus = this.battleModsCache.TowerDamagePercentBonus;
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
