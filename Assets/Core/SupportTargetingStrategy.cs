namespace DungeonTower.Core
{
    /// <summary>
    /// How an EnemySO picks WHICH ally to help, among whichever allies
    /// within TargetingRange actually need the support ability in
    /// question (see SupportNeed) — a separate question from whether to
    /// support at all. LowestHpPercent is the default: the most
    /// proportionally hurt ally, which behaves sensibly for a mixed
    /// party of different max-HP units.
    /// </summary>
    public enum SupportTargetingStrategy
    {
        LowestHpPercent,
        LowestHp,
        MostHarmfulStatuses,
        Nearest,
        Self
    }
}
