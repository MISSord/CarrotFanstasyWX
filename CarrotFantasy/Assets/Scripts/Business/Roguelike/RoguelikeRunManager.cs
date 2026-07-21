using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 肉鸽 Run 流程编排：大地图 ↔ 战斗场景，转发 Hex 事件到 <see cref="RoguelikeRunServer"/>。
    /// </summary>
    public class RoguelikeRunManager : MonoBehaviour
    {
        public static RoguelikeRunManager Instance { get; private set; }

        [SerializeField] int defaultBigLevel = 1;
        [SerializeField] int defaultLevel = 1;
        [SerializeField] float returnToMapDelaySeconds = 0.5f;

        HexWorldMapController mapController;
        HexWorldMapRuntime mapRuntime;
        bool contextBound;
        int pendingNotifyPointId;
        bool pendingNotifyVictory;
        int returnToMapSchId;

        public bool IsBoundToMap
        {
            get { return this.mapController != null && this.mapRuntime != null; }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                this.UnbindContext();
                Instance = null;
            }
            this.CancelReturnSchedule();
        }

        /// <summary>由大地图场景或 <see cref="HexWorldMapController"/> 调用。</summary>
        public void BindHexMap(HexWorldMapController controller)
        {
            if (controller == null)
            {
                return;
            }

            this.UnbindContext();
            this.mapController = controller;
            this.mapRuntime = controller.Runtime;

            if (controller.MapAsset == null || this.mapRuntime == null)
            {
                Debug.LogError("[RoguelikeRunManager] mapAsset or runtime is null.");
                return;
            }

            int mapId = controller.MapAsset.name.GetHashCode();
            if (!RoguelikeRunServer.Instance.IsRunActive)
            {
                // 选关进图会先 StartRun；直接进 Scene 时由 MapServer 按默认/最近关兜底。
                if (RoguelikeMapServer.Instance != null)
                {
                    RoguelikeMapServer.Instance.EnsureRunForDirectSceneEntry(mapId);
                }
                else
                {
                    RoguelikeRunServer.Instance.StartRunFromMapIdFallback(mapId, this.mapRuntime.ExportProgress());
                }
            }

            this.BindContext(this.mapRuntime.Context);
            this.FlushPendingMapNotifications();
        }

        public static RoguelikeRunManager EnsureOn(HexWorldMapController controller)
        {
            if (controller == null)
            {
                return Instance;
            }
            if (Instance == null)
            {
                GameObject go = new GameObject("RoguelikeRunManager");
                Instance = go.AddComponent<RoguelikeRunManager>();
            }
            Instance.BindHexMap(controller);
            return Instance;
        }

        void BindContext(HexMapContext context)
        {
            if (context == null || this.contextBound)
            {
                return;
            }
            context.OnBattleRequested += this.HandleBattleRequested;
            context.OnShopRequested += this.HandleShopRequested;
            context.OnRandomEventRequested += this.HandleRandomEventRequested;
            this.contextBound = true;
        }

        void UnbindContext()
        {
            if (!this.contextBound || this.mapRuntime == null || this.mapRuntime.Context == null)
            {
                this.contextBound = false;
                return;
            }
            HexMapContext context = this.mapRuntime.Context;
            context.OnBattleRequested -= this.HandleBattleRequested;
            context.OnShopRequested -= this.HandleShopRequested;
            context.OnRandomEventRequested -= this.HandleRandomEventRequested;
            this.contextBound = false;
        }

        void HandleBattleRequested(int pointId, int encounterId)
        {
            RoguelikeRunServer.Instance.SetPendingBattle(pointId, encounterId);
            if (this.mapRuntime != null)
            {
                RoguelikeRunServer.Instance.SyncMapProgress(this.mapRuntime.ExportProgress());
            }

            BattleLauncher.StartRoguelikeEncounter(
                encounterId,
                this.defaultBigLevel,
                this.defaultLevel
            );
        }

        void HandleShopRequested(int pointId)
        {
            RoguelikeRunServer.Instance.SetActiveShopPoint(pointId);
            Debug.Log(
                "[RoguelikeRun] Shop at point " + pointId +
                ", gold=" + RoguelikeRunServer.Instance.RoguelikeGold +
                ". Keys: 1/2/3=buy offer, C=close."
            );
        }

        void HandleRandomEventRequested(int pointId, int randomEventId)
        {
            RoguelikeRunServer.Instance.AddRoguelikeGold(25, RoguelikeGoldSource.RandomEvent);
            Debug.Log(
                "[RoguelikeRun] Random event " + randomEventId + " at " + pointId +
                ", +25 gold. Press C to continue."
            );
        }

        public void CloseRandomEvent()
        {
            this.SyncMapProgressToRun();
            if (this.mapRuntime != null && this.mapRuntime.Context != null)
            {
                this.mapRuntime.Context.NotifyRandomEventClosed();
            }
        }

        void SyncMapProgressToRun()
        {
            if (this.mapRuntime != null && RoguelikeRunServer.Instance.IsRunActive)
            {
                RoguelikeRunServer.Instance.SyncMapProgress(this.mapRuntime.ExportProgress());
            }
        }

        public void HandlePveMatchSettled(PveMatchSettlement settlement)
        {
            if (settlement == null)
            {
                return;
            }

            this.pendingNotifyPointId = RoguelikeRunServer.Instance.PendingBattlePointId;
            this.pendingNotifyVictory = settlement.IsVictory;

            if (settlement.IsVictory)
            {
                RoguelikeRunServer.Instance.OnBattleVictory();
            }

            RoguelikeRunServer.Instance.ClearPendingBattle();
            this.ScheduleReturnToMap();
        }

        void ScheduleReturnToMap()
        {
            this.CancelReturnSchedule();
            if (this.returnToMapDelaySeconds <= 0f)
            {
                this.ReturnToRoguelikeMap();
                return;
            }
            this.returnToMapSchId = Sche.DelayExeOnceTimes(this.ReturnToRoguelikeMap, this.returnToMapDelaySeconds);
        }

        void CancelReturnSchedule()
        {
            if (this.returnToMapSchId != 0)
            {
                Sche.SilenceSingleSche(this.returnToMapSchId);
                this.returnToMapSchId = 0;
            }
        }

        void ReturnToRoguelikeMap()
        {
            this.returnToMapSchId = 0;
            ServerProvision.sceneServer.LoadScene(BaseSceneType.RoguelikeMapScene, null);
        }

        void FlushPendingMapNotifications()
        {
            if (this.mapRuntime == null || this.mapRuntime.Context == null)
            {
                return;
            }
            if (this.pendingNotifyPointId <= 0)
            {
                return;
            }

            if (this.pendingNotifyVictory)
            {
                this.mapRuntime.Context.NotifyBattleWon(this.pendingNotifyPointId);
            }
            else
            {
                this.mapRuntime.Context.NotifyBattleLost(this.pendingNotifyPointId);
            }

            this.pendingNotifyPointId = 0;
            this.pendingNotifyVictory = false;
        }

        /// <summary>商店调试：购买第 index 个商品（0-based）。</summary>
        public bool TryPurchaseShopOfferIndex(int index)
        {
            if (!RoguelikeRunServer.Instance.IsRunActive)
            {
                return false;
            }
            int shopPointId = RoguelikeRunServer.Instance.ActiveRun.activeShopPointId;
            List<RoguelikeShopOffer> offers = RoguelikeRunServer.Instance.GetShopOffers(shopPointId);
            if (index < 0 || index >= offers.Count)
            {
                return false;
            }
            return RoguelikeRunServer.Instance.TryPurchase(offers[index].offerId);
        }

        public void CloseShop()
        {
            RoguelikeRunServer.Instance.ClearActiveShop();
            this.SyncMapProgressToRun();
            if (this.mapRuntime != null && this.mapRuntime.Context != null)
            {
                this.mapRuntime.Context.NotifyShopClosed();
            }
        }

        void OnGUI()
        {
            if (!RoguelikeRunServer.Instance.IsRunActive || !this.IsBoundToMap)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(10f, Screen.height - 120f, 320f, 110f), GUI.skin.box);
            RoguelikeRunState run = RoguelikeRunServer.Instance.ActiveRun;
            if (run != null)
            {
                GUILayout.Label("Level: " + run.bigLevelId + "-" + run.levelId + " pool=" + run.shopPoolId);
            }
            GUILayout.Label("Roguelike Gold: " + RoguelikeRunServer.Instance.RoguelikeGold);
            GUILayout.Label("Inventory: " + string.Join(", ", RoguelikeRunServer.Instance.OwnedItemIds));
            GUILayout.Label("StartEffects: " + string.Join(", ", RoguelikeRunServer.Instance.StartingEffectIds));
            GUILayout.Label("Shop: 1/2/3 buy, C close | Random: C close");
            GUILayout.EndArea();
        }

        void Update()
        {
            if (!RoguelikeRunServer.Instance.IsRunActive || this.mapRuntime == null)
            {
                return;
            }
            if (!this.mapRuntime.MovementLocked)
            {
                return;
            }

            int shopPointId = RoguelikeRunServer.Instance.ActiveRun.activeShopPointId;
            if (shopPointId != 0)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    this.TryPurchaseShopOfferIndex(0);
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    this.TryPurchaseShopOfferIndex(1);
                }
                else if (Input.GetKeyDown(KeyCode.Alpha3))
                {
                    this.TryPurchaseShopOfferIndex(2);
                }
                else if (Input.GetKeyDown(KeyCode.C))
                {
                    this.CloseShop();
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                this.CloseRandomEvent();
            }
        }
    }
}
