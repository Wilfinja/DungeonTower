namespace DungeonTower.Core
{
    /// <summary>
    /// The tactical job an enemy kit is meant to play within a room's
    /// composition — used both to tag an EnemySO and to bias encounter
    /// building toward a varied room instead of, say, five Tanks.
    /// </summary>
    public enum EnemyRole
    {
        Tank,
        Scout,
        Ranged,
        Caster,
        Boss,
        Controller,
        Assassin,
        Minion
    }
}
