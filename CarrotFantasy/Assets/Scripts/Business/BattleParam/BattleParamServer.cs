using LitJson;
using System.IO;
using UnityEngine;

namespace CarrotFantasy
{
    //战斗参数服务，记录当前游戏进度
    public class BattleParamServer : BaseServer<BattleParamServer>
    {
        private NormalModelPanel normalModepanel;
        private MenuView menuView;
        private GameWinView gameWinView;
        private GameOverView gameOverView;

        public SingleMapInfo curSingleMapInfo;
        public Stage curStage;
        public LevelInfo info;

        public int curBigLevel;
        public int curLevel;

        public bool isPVE = false;

        /// <summary>
        /// 为 true 时 <see cref="BattleManager"/> 创建 <see cref="SurvivalPveBattle"/>（生存模式）；
        /// 为 false 时创建经典 <see cref="PveBattle"/>。仅当 <see cref="isPVE"/> 为 true 时生效。在进入战斗前由入口设置。
        /// </summary>
        public bool useSurvivalPveBattleMode = false;

        /// <summary>
        /// 为 true 时进入 <see cref="HitTestBenchmarkBattle"/>（碰撞性能对比），不再走 Pve / Survival Pve。
        /// </summary>
        public bool useHitTestBenchmarkBattle = false;

        /// <summary>基准战斗内碰撞实现：true=网格版 <see cref="BattleSimpleHitTestComponent"/>，false=暴力版 <see cref="BattleBruteForceHitTestComponent"/>。</summary>
        public bool hitTestBenchmarkUseSpatialGrid = true;

        /// <summary>基准战斗 Console 输出间隔（逻辑帧数）。</summary>
        public int hitTestBenchmarkLogIntervalFrames = 300;

        /// <summary>测试怪/弹刷出间隔（逻辑帧）。</summary>
        public int testUnitsSpawnIntervalFrames = 15;

        public int testMonstersPerBatch = 4;

        public int testBulletsPerBatch = 8;

        /// <summary>基准/测试战斗地图列数（格子宽）。</summary>
        public int hitTestMapXColumn = 24;

        /// <summary>基准/测试战斗地图行数（格子高）。</summary>
        public int hitTestMapYRow = 16;

        public override void LoadModule()
        {
            base.LoadModule();
            this.InitBattleViews();
            this.AddListener();
        }

        private void InitBattleViews()
        {
            normalModepanel = new NormalModelPanel();
            normalModepanel.RegisterData();

            if (menuView == null)
            {
                menuView = new MenuView();
                menuView.RegisterData();
            }
            if (gameWinView == null)
            {
                gameWinView = new GameWinView();
                gameWinView.RegisterData();
            }
            if (gameOverView == null)
            {
                gameOverView = new GameOverView();
                gameOverView.RegisterData();
            }
        }

        private void AddListener()
        {
            BusinessProvision.Instance.eventDispatcher.AddListener(CommonEventType.READY_START_PVE_GAME, this.GetPVEBattleParams);
            BusinessProvision.Instance.eventDispatcher.AddListener(CommonEventType.READY_START_PVP_GAME, this.GetPVPBattleParams);
        }

        private void GetPVEBattleParams()
        {
            this.isPVE = true;
            this.curBigLevel = MapServer.Instance.curBigLevel;
            this.curLevel = MapServer.Instance.curLevel;

            this.curStage = MapServer.Instance.mapModel.GetStage(this.curBigLevel, curLevel);
            this.curSingleMapInfo = MapServer.Instance.mapModel.GetSingleMapInfo(this.curBigLevel, curLevel);

            string path = "Level" + this.curBigLevel.ToString() + "_" + this.curLevel.ToString() + ".json";
            this.info = LoadLevelInfoFile(path);
        }

        private void GetPVPBattleParams()
        {

        }

        private void RemoveListener()
        {
            BusinessProvision.Instance.eventDispatcher.RemoveListener(CommonEventType.READY_START_PVE_GAME, this.GetPVEBattleParams);
            BusinessProvision.Instance.eventDispatcher.RemoveListener(CommonEventType.READY_START_PVP_GAME, this.GetPVPBattleParams);
        }

        public override void Dispose()
        {
            this.RemoveListener();
            base.Dispose();
        }

        //读取关卡文件解析json转化为LevelInfo对象
        public LevelInfo LoadLevelInfoFile(string fileName)
        {
            LevelInfo levelInfo = new LevelInfo();
            string filePath = Application.streamingAssetsPath + "/Json/Level/" + fileName;
            if (File.Exists(filePath))
            {
                StreamReader sr = new StreamReader(filePath);
                string jsonStr = sr.ReadToEnd();
                sr.Close();
                levelInfo = JsonMapper.ToObject<LevelInfo>(jsonStr);
                return levelInfo;
            }
            Debug.Log("文件加载失败，加载路径是" + filePath);
            return null;
        }
    }
}
