namespace CarrotFantasy
{
    /// <summary>怪物位移能力（折线 / 流场等），与具体 <see cref="BaseUnitComponent"/> 实现解耦。</summary>
    public interface IMonsterLocomotion
    {
        bool isReachCarrot { get; }

        Fix64 EndPointDistance { get; }

        void OnTick(Fix64 deltaTime);

        void ClearMovementState();
    }
}
