using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 肉鸽选关层（对齐 <see cref="MapServer"/>）：大关/小关解锁、小关配置、发起进图。
    /// 局内态仍由 <see cref="RoguelikeRunServer"/> 管理。
    /// </summary>
    public class RoguelikeMapServer : BaseServer<RoguelikeMapServer>
    {
        public RoguelikeMapModel mapModel { get; private set; }
        public EventDispatcher eventDispatcher { get; private set; }

        public int LastEnteredBigLevelId { get; private set; }
        public int LastEnteredLevelId { get; private set; }

        RoguelikeBigLevelPanel bigLevelPanel;
        RoguelikeNormalLevelPanel normalLevelPanel;

        protected override void OnSingletonInit()
        {
            this.eventDispatcher = new EventDispatcher();
            this.mapModel = new RoguelikeMapModel(this.eventDispatcher);
            RoguelikeEffectConfigReader.Instance.Init();
            RoguelikeLevelConfigReader.Instance.Init();
            RoguelikeShopConfigReader.Instance.Init();
        }

        public override void LoadModule()
        {
            base.LoadModule();
            this.mapModel.ApplyDefaultProgress();
            this.AddAccountListener();
            this.AddRunListener();
            BusinessProvision.Instance.eventDispatcher.AddListener(
                CommonEventType.ENTER_ROGUELIKE_MAP,
                this.OnEnterRoguelikeMapEvent);

            this.bigLevelPanel = new RoguelikeBigLevelPanel();
            this.bigLevelPanel.RegisterData();
            this.normalLevelPanel = new RoguelikeNormalLevelPanel();
            this.normalLevelPanel.RegisterData();
        }

        public override void Dispose()
        {
            this.RemoveAccountListener();
            this.RemoveRunListener();
            if (BusinessProvision.Instance != null)
            {
                BusinessProvision.Instance.eventDispatcher.RemoveListener(
                    CommonEventType.ENTER_ROGUELIKE_MAP,
                    this.OnEnterRoguelikeMapEvent);
            }
            if (this.mapModel != null)
            {
                this.mapModel.Dispose();
            }
            base.Dispose();
        }

        void AddAccountListener()
        {
            AccountServer.Instance.eventDispatcher.AddListener(
                AccountServer.LOGIN_SUCCESS,
                this.OnLoginBootstrap);
        }

        void RemoveAccountListener()
        {
            if (AccountServer.Instance != null)
            {
                AccountServer.Instance.eventDispatcher.RemoveListener(
                    AccountServer.LOGIN_SUCCESS,
                    this.OnLoginBootstrap);
            }
        }

        void AddRunListener()
        {
            RoguelikeRunServer.Instance.eventDispatcher.AddListener<RoguelikeRunEndReason>(
                RoguelikeEvent.RUN_ENDED,
                this.OnRunEnded);
        }

        void RemoveRunListener()
        {
            if (RoguelikeRunServer.Instance != null)
            {
                RoguelikeRunServer.Instance.eventDispatcher.RemoveListener<RoguelikeRunEndReason>(
                    RoguelikeEvent.RUN_ENDED,
                    this.OnRunEnded);
            }
        }

        void OnLoginBootstrap()
        {
            // Phase 1：无独立肉鸽存档协议，登录后铺默认进度。
            this.mapModel.ApplyDefaultProgress();
        }

        /// <summary>无参数进图事件：进入最近选关，或默认 1-1。</summary>
        void OnEnterRoguelikeMapEvent()
        {
            int big = this.LastEnteredBigLevelId > 0 ? this.LastEnteredBigLevelId : 1;
            int level = this.LastEnteredLevelId > 0 ? this.LastEnteredLevelId : 1;
            this.EnterLevel(big, level);
        }

        public void RememberLastEntered(int bigLevelId, int levelId)
        {
            if (bigLevelId <= 0 || levelId <= 0)
            {
                return;
            }
            this.LastEnteredBigLevelId = bigLevelId;
            this.LastEnteredLevelId = levelId;
        }

        public bool CanEnterLevel(int bigLevelId, int levelId)
        {
            if (!RoguelikeLevelConfigReader.Instance.TryGet(bigLevelId, levelId, out _))
            {
                return false;
            }
            return this.mapModel.IsLevelUnlocked(bigLevelId, levelId);
        }

        public RoguelikeLevelDef GetLevelDef(int bigLevelId, int levelId)
        {
            return RoguelikeLevelConfigReader.Instance.Get(bigLevelId, levelId);
        }

        /// <summary>
        /// 选关确认进图：校验解锁 → 开 Run → 进肉鸽场景。
        /// </summary>
        public bool EnterLevel(int bigLevelId, int levelId)
        {
            if (!this.CanEnterLevel(bigLevelId, levelId))
            {
                this.eventDispatcher.DispatchEvent(RoguelikeMapEventType.CANT_ENTER_LEVEL);
                UIServer.Instance?.ShowTip("关卡尚未解锁");
                Debug.LogWarning("[RoguelikeMapServer] Cannot enter " + bigLevelId + "-" + levelId);
                return false;
            }

            RoguelikeLevelDef def = this.GetLevelDef(bigLevelId, levelId);
            if (def == null)
            {
                this.eventDispatcher.DispatchEvent(RoguelikeMapEventType.CANT_ENTER_LEVEL);
                return false;
            }

            if (ServerProvision.sceneServer != null && ServerProvision.sceneServer.IsLoading)
            {
                UIServer.Instance?.ShowTip("场景加载中，请稍候");
                return false;
            }

            if (RoguelikeRunServer.Instance.IsRunActive)
            {
                RoguelikeRunServer.Instance.EndRun(RoguelikeRunEndReason.Abandoned);
            }

            RoguelikeRunStartParams startParams = RoguelikeRunStartParams.FromLevelDef(def);
            startParams.mapId = def.hexMapAssetId != null ? def.hexMapAssetId.GetHashCode() : 0;

            this.RememberLastEntered(bigLevelId, levelId);
            RoguelikeRunServer.Instance.StartRun(startParams);
            this.eventDispatcher.DispatchEvent(RoguelikeMapEventType.CAN_ENTER_LEVEL);

            ServerProvision.sceneServer.LoadScene(BaseSceneType.RoguelikeMapScene, null);
            return true;
        }

        /// <summary>
        /// 编辑器直接进 Scene.unity 时的兜底：若尚无 Run，按 1-1（或最近选关）开局，不重复 LoadScene。
        /// </summary>
        public void EnsureRunForDirectSceneEntry(int fallbackMapId)
        {
            if (RoguelikeRunServer.Instance.IsRunActive)
            {
                return;
            }

            int big = this.LastEnteredBigLevelId > 0 ? this.LastEnteredBigLevelId : 1;
            int level = this.LastEnteredLevelId > 0 ? this.LastEnteredLevelId : 1;

            RoguelikeLevelDef def = this.GetLevelDef(big, level);
            if (def == null)
            {
                def = this.GetLevelDef(1, 1);
            }
            if (def == null)
            {
                Debug.LogError("[RoguelikeMapServer] No level def for direct entry.");
                return;
            }

            // 直接进场景时不做解锁校验，方便调试。
            RoguelikeRunStartParams startParams = RoguelikeRunStartParams.FromLevelDef(def);
            startParams.mapId = fallbackMapId != 0
                ? fallbackMapId
                : (def.hexMapAssetId != null ? def.hexMapAssetId.GetHashCode() : 0);
            this.RememberLastEntered(def.bigLevelId, def.levelId);
            RoguelikeRunServer.Instance.StartRun(startParams);
        }

        void OnRunEnded(RoguelikeRunEndReason reason)
        {
            if (reason != RoguelikeRunEndReason.Victory)
            {
                return;
            }

            int big = this.LastEnteredBigLevelId;
            int level = this.LastEnteredLevelId;
            if (big <= 0 || level <= 0)
            {
                return;
            }

            this.mapModel.MarkClearedAndUnlockNext(big, level);
            Debug.Log("[RoguelikeMapServer] Cleared " + big + "-" + level + ", unlocked next if any.");
        }
    }
}
