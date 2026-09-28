namespace DungeonTower.Core
{
    /// <summary>
    /// How an EnemySO picks which enemy (hostile) unit to attack, among
    /// whatever's within its EnemySO.TargetingRange. Nearest is the old
    /// behavior and the default, so every EnemySO authored before this
    /// existed keeps acting exactly as it did. Ties within a strategy
    /// break by distance (see EnemyTargeting.Best) — the assassin's
    /// "lowest HP" pick still prefers the closer of two equally-hurt
    /// targets, for example.
    /// </summary>
    public enum EnemyTargetingStrategy
    {
        Nearest,
        LowestHp,
        LowestHpPercent,
        LowestPhysicalDefense,
        LowestMagicDefense,
        HighestPhysicalAttack,
        HighestMagicAttack
    }
}
