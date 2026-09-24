namespace DungeonTower.Core
{
    /// <summary>
    /// A consumable used on oneself — carries the same IAbility a
    /// weapon or scroll would, typically Heal or Buff kind, so a
    /// potion's effect is authored exactly the same way as anything
    /// else that uses an ability. Implemented by PotionSO
    /// (DungeonTower.Items) — Core never references ScriptableObject
    /// directly, so this stays engine-free.
    /// </summary>
    public interface IPotion : IConsumable
    {
        // The potion's category (Health Potion, Mana Potion, ...) —
        // multiple strengths (Minor/Greater Health Potion) can share
        // one Group, same idea as IWeapon.Group.
        PotionId Group { get; }

        IAbility Ability { get; }
    }
}
