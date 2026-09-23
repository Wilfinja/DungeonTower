namespace DungeonTower.Core
{
    /// <summary>
    /// A consumable that heals on use — HP, MP, or both. Implemented by
    /// PotionSO (DungeonTower.Items) — Core never references
    /// ScriptableObject directly, so this stays engine-free.
    /// </summary>
    public interface IPotion : IConsumable
    {
        int HealHp { get; }
        int HealMp { get; }
    }
}
