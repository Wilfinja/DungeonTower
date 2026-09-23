namespace DungeonTower.Core
{
    /// <summary>
    /// A consumable that casts a one-shot ability on use — carries the
    /// same IAbility a weapon would, so an offensive scroll resolves
    /// through the same attack/AoE machinery as a weapon swing.
    /// Implemented by ScrollSO (DungeonTower.Items) — Core never
    /// references ScriptableObject directly, so this stays engine-free.
    /// </summary>
    public interface IScroll : IConsumable
    {
        IAbility Ability { get; }
    }
}
