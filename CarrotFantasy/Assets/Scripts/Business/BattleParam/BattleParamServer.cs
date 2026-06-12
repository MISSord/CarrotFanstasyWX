using LitJson;
using System.IO;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>战斗参数服务：仅持有当前 <see cref="CurrentPveParams"/> 与基准测试配置。</summary>
    public class BattleParamServer : BaseServer<BattleParamServer>
    {
        private NormalModelPanel normalModepanel;
        private MenuView menuView;
        private GameWinView gameWinView;
        private GameOverView gameOverView;

        /// <summary>最近一次开战参数，Model 层经 <see cref="BattleParamAccess"/> 读取。</summary>
        public PveModelBattleParams CurrentPveParams { get; private set; }

        /// <summary>基准战斗碰撞实现：true=网格版，false=暴力版。</summary>
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

        /// <summary>将开战参数写入本服务，供 Model_Battle 各组件 Init 时读取。</summary>
        public void ApplyPveParams(PveModelBattleParams launchParams)
        {
            if (launchParams == null)
            {
                return;
            }

            launchParams.EnsureLevelDataLoaded();
            this.CurrentPveParams = launchParams;
        }

        public void ClearPveParams()
        {
            this.CurrentPveParams = null;
        }

        /// <summary>若未走 <see cref="LoadModule"/>，战斗 UI 注册可延迟到此。</summary>
        public void EnsureBattleViewsLoaded()
        {
            if (this.normalModepanel == null)
            {
                this.InitBattleViews();
            }
        }

        public override void Dispose()
        {
            this.ClearPveParams();
            base.Dispose();
        }

        public LevelInfo LoadLevelInfoFile(string fileName)
        {
            string filePath = Application.streamingAssetsPath + "/Json/Level/" + fileName;
            if (File.Exists(filePath))
            {
                StreamReader sr = new StreamReader(filePath);
                string jsonStr = sr.ReadToEnd();
                sr.Close();
                return JsonMapper.ToObject<LevelInfo>(jsonStr);
            }

            Debug.Log("文件加载失败，加载路径是" + filePath);
            return null;
        }
    }
}
