using LitJson;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>战斗参数服务</summary>
    public class BattleParamServer : BaseServer<BattleParamServer>
    {
        private NormalModelPanel normalModepanel;
        private MenuView menuView;
        private GameWinView gameWinView;
        private GameOverView gameOverView;

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
            base.Dispose();
        }

        public LevelInfo LoadLevelInfoFile(string fileName)
        {
            string jsonStr = GameJsonLoader.LoadLevelJsonText(fileName);
            if (!string.IsNullOrEmpty(jsonStr))
            {
                return JsonMapper.ToObject<LevelInfo>(jsonStr);
            }

            Debug.LogError("关卡 JSON 加载失败: " + fileName);
            return null;
        }
    }
}
