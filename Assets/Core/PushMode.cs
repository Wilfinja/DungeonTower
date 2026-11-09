namespace DungeonTower.Core
{
    /// <summary>
    /// What a push or pull does when the target runs into something.
    /// APPEND-ONLY - serialized by value.
    ///
    /// Collide: the target stops at the first wall, door, object or unit in
    /// its way. Every tile of the push it did not get to travel becomes
    /// slam damage (IAbility.SlamDamagePerTile per tile, flat), dealt to the
    /// target and to whatever it hit - a wall or object only takes the
    /// target's half. Running into the caster during a pull just stops the
    /// target next to them, with no damage.
    /// </summary>
    public enum PushMode
    {
        Collide,
    }
}
