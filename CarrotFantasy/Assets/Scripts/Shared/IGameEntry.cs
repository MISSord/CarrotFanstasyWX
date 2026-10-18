using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 游戏业务入口契约：资源更新完成后由 <see cref="GameMain"/> 创建并驱动。
    /// <para>
    /// 保留接口是为了让启动壳只依赖契约、不依赖业务实现细节；
    /// 现在两者在同一个程序集里，直接 new 即可，不再需要反射定位。
    /// </para>
    /// </summary>
    public interface IGameEntry
    {
        void Start(GameObject host);

        void Tick(float deltaTime);

        void ChangeState(GameState state);

        bool IsQuitRequested { get; }
    }
}
