using LitJson;
using System;
using System.IO;
using UnityEngine;

namespace CarrotFantasy
{
    //战斗参数服务，记录当前游戏进度
    public class BattleParamServer : BaseServer<BattleParamServer>
    {
        public SingleMapInfo curSingleMapInfo;
        public Stage curStage;
        public LevelInfo info;

        public int curBigLevel;
        public int curLevel;

        public bool isPVE = false;

        public override void LoadModule()
        {
            base.LoadModule();
            this.AddListener();
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

            String path = "Level" + this.curBigLevel.ToString() + "_" + this.curLevel.ToString() + ".json";
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
