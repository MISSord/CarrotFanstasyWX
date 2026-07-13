using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 热更程序集入口契约。AOT 通过反射创建实现类，不直接引用热更类型。
    /// </summary>
    public interface IHotUpdateEntry
    {
        void Start(GameObject host);
        void Tick(float deltaTime);
        void ChangeState(GameState state);
        bool IsQuitRequested { get; }
    }
}
