namespace DungeonTower.Core
{
    /// <summary>
    /// How a placed BattlefieldObject's Statuses (if any) actually get
    /// applied. See IAbility.SummonTriggerMode for the full picture.
    /// </summary>
    public enum SummonTriggerMode
    {
        None,
        OnRoundTick,
        OnEntry
    }
}
