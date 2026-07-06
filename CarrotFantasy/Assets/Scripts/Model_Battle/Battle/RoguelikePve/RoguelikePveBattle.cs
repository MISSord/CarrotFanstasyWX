namespace CarrotFantasy
{
    /// <summary>肉鸽模式 PVE</summary>
    public class RoguelikePveBattle : PveBattleBase
    {
        protected override PveBattleComponentSetup.Layout ComponentLayout =>
            PveBattleComponentSetup.Layout.Classic;
    }
}
