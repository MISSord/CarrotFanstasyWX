using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 进入战斗时由业务层（地图入口、场景加载等）传入的运行时参数。
    /// </summary>
    public sealed class BattleLaunchParams
    {
        /// <summary>战斗视图挂载的根物体（地图格子、塔、怪等实例化的父节点）。为空时会尝试当前场景的 gameObj，再退回新建占位物体。</summary>
        public GameObject BattleViewRoot;

        /// <summary>初始化完成后延迟若干秒再 StartGame（与原 BattleScene 行为一致）。≤0 则本帧立即 StartGame。</summary>
        public float StartGameDelaySeconds = 2f;

        /// <summary>
        /// 从场景切换字典解析可选参数。约定键：<c>battleViewRoot</c>（GameObject）、<c>startGameDelay</c>（秒，数值）。
        /// 使用 <see cref="object"/> 而非 dynamic，避免 Unity 下缺少 Microsoft.CSharp 运行时绑定导致的 CS0656。
        /// </summary>
        public static BattleLaunchParams FromDictionary(Dictionary<string, object> param)
        {
            BattleLaunchParams p = new BattleLaunchParams();
            if (param == null)
                return p;
            if (param.TryGetValue("battleViewRoot", out object rootObj) && rootObj is GameObject go)
                p.BattleViewRoot = go;
            if (param.TryGetValue("startGameDelay", out object delayObj) && delayObj != null &&
                TryToSingle(delayObj, out float delay))
                p.StartGameDelaySeconds = delay;
            return p;
        }

        private static bool TryToSingle(object value, out float result)
        {
            switch (value)
            {
                case float f:
                    result = f;
                    return true;
                case int i:
                    result = i;
                    return true;
                case double d:
                    result = (float)d;
                    return true;
                case long l:
                    result = l;
                    return true;
                default:
                    try
                    {
                        result = Convert.ToSingle(value);
                        return true;
                    }
                    catch
                    {
                        result = 0f;
                        return false;
                    }
            }
        }
    }
}
